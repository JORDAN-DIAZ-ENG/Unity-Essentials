using System.Collections;
using UnityEngine;

namespace Essential.Audio
{
    /// <summary>
    /// The scene-side half of the audio system: owns the AudioSources, the voice
    /// pool and the music crossfade coroutines.
    ///
    /// It builds itself on first use, so a project never has to place a prefab or
    /// remember to add a manager to the first scene. Game code talks to
    /// <see cref="AudioManager"/> and should not need to touch this type.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class AudioRuntime : MonoBehaviour
    {
        const string GameObjectName = "[Essential.Audio]";

        static AudioRuntime _instance;
        static bool _quitting;

        AudioBank _bank;
        AudioSource[] _sfxVoices;
        int _nextVoice;

        // Two music sources so one can fade out while the other fades in.
        AudioSource _bgmA;
        AudioSource _bgmB;
        bool _usingA = true;

        BgmHandle _activeHandle;
        Coroutine _watchRoutine;
        Coroutine _fadeInRoutine;
        Coroutine _fadeOutRoutine;
        bool _bgmPaused;

        // The per-track volume the caller asked for, kept separate from the mixed
        // result so that changing master volume mid-track recomputes correctly.
        float _bgmScale = 1f;

        float BgmTargetVolume => BgmVolumeFor(_bgmScale);

        AudioSource ActiveBgm => _usingA ? _bgmA : _bgmB;

        internal AudioBank Bank
        {
            get => _bank;
            set
            {
                _bank = value;
                if (_bank != null) RebuildVoices();
            }
        }

        internal bool IsBgmPlaying
        {
            get
            {
                var source = ActiveBgm;
                return source != null && (source.isPlaying || _bgmPaused);
            }
        }

        // ------------------------------------------------------------------
        // Lifetime
        // ------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload can be switched off in Enter Play Mode settings, in
            // which case statics survive between sessions and would point at a
            // destroyed object. Clear them at the start of every play session.
            _instance = null;
            _quitting = false;
        }

        /// <summary>
        /// The runtime if it already exists, without building one. Queries and stop
        /// calls use this so that merely asking whether music is playing does not
        /// spawn the audio object.
        /// </summary>
        internal static AudioRuntime Existing => _instance;

        /// <summary>
        /// Returns the live runtime, creating it if this is the first sound of the
        /// session. Returns null while the application is shutting down, so that an
        /// OnDestroy handler playing a sound cannot resurrect it.
        /// </summary>
        internal static AudioRuntime Get()
        {
            if (_instance != null) return _instance;
            if (_quitting || !Application.isPlaying) return null;

            var host = new GameObject(GameObjectName);
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<AudioRuntime>();
            return _instance;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (_bank == null) _bank = AudioManager.Bank;

            BuildSources();
        }

        void OnApplicationQuit() => _quitting = true;

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void BuildSources()
        {
            _bgmA = CreateSource("BGM A");
            _bgmB = CreateSource("BGM B");
            RebuildVoices();
        }

        void RebuildVoices()
        {
            int wanted = _bank != null ? Mathf.Max(1, _bank.SfxVoiceCount) : 16;
            if (_sfxVoices != null && _sfxVoices.Length == wanted)
            {
                ApplyMixerGroups();
                return;
            }

            if (_sfxVoices != null)
            {
                foreach (var voice in _sfxVoices)
                {
                    if (voice != null) Destroy(voice.gameObject);
                }
            }

            _sfxVoices = new AudioSource[wanted];
            for (int i = 0; i < wanted; i++)
            {
                _sfxVoices[i] = CreateSource("SFX Voice " + i);
            }

            _nextVoice = 0;
            ApplyMixerGroups();
        }

        void ApplyMixerGroups()
        {
            if (_bank == null) return;

            if (_bgmA != null) _bgmA.outputAudioMixerGroup = _bank.BgmMixerGroup;
            if (_bgmB != null) _bgmB.outputAudioMixerGroup = _bank.BgmMixerGroup;

            if (_sfxVoices == null) return;
            foreach (var voice in _sfxVoices)
            {
                if (voice != null) voice.outputAudioMixerGroup = _bank.SfxMixerGroup;
            }
        }

        AudioSource CreateSource(string label)
        {
            // Each source gets its own child object so positioned sounds can move
            // independently and so the hierarchy stays readable while debugging.
            var child = new GameObject(label);
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }

        // ------------------------------------------------------------------
        // Sound effects
        // ------------------------------------------------------------------

        /// <summary>
        /// Grabs the next free voice, or steals the oldest one when every voice is
        /// busy. Round-robin stealing keeps the most recent sounds audible, which is
        /// what you want when a hundred pickups land on the same frame.
        /// </summary>
        AudioSource TakeVoice()
        {
            if (_sfxVoices == null || _sfxVoices.Length == 0) RebuildVoices();

            int count = _sfxVoices.Length;
            for (int offset = 0; offset < count; offset++)
            {
                int index = (_nextVoice + offset) % count;
                var candidate = _sfxVoices[index];
                if (candidate != null && !candidate.isPlaying)
                {
                    _nextVoice = (index + 1) % count;
                    return candidate;
                }
            }

            var stolen = _sfxVoices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % count;
            if (stolen != null) stolen.Stop();
            return stolen;
        }

        internal AudioSource PlaySfx(AudioEntry entry, float volumeScale, int clipIndex, Vector3? position, bool loop)
        {
            var clip = entry.GetClip(clipIndex);
            if (clip == null) return null;

            var voice = TakeVoice();
            if (voice == null) return null;

            voice.clip = clip;
            voice.volume = SfxVolumeFor(entry, volumeScale);
            voice.pitch = entry.PickPitch();
            voice.loop = loop;

            if (position.HasValue)
            {
                voice.transform.position = position.Value;
                voice.spatialBlend = 1f;
            }
            else
            {
                voice.transform.localPosition = Vector3.zero;
                voice.spatialBlend = 0f;
            }

            voice.Play();
            return voice;
        }

        // Volume comes from AudioManager rather than straight off the bank asset, so
        // that a slider changed during play is respected without editing the asset.
        static float SfxVolumeFor(AudioEntry entry, float volumeScale)
        {
            return Mathf.Clamp01(AudioManager.MasterVolume * AudioManager.SfxVolume
                                 * entry.Volume * volumeScale);
        }

        static float BgmVolumeFor(float volumeScale)
        {
            return Mathf.Clamp01(AudioManager.MasterVolume * AudioManager.BgmVolume * volumeScale);
        }

        // ------------------------------------------------------------------
        // Music
        // ------------------------------------------------------------------

        internal BgmHandle PlayBgm(AudioEntry entry, bool loop, float volumeScale, int clipIndex, float fadeIn, float fadeOut)
        {
            var clip = entry.GetClip(clipIndex);
            if (clip == null) return null;

            // The outgoing track is cut short rather than finishing, so its handle
            // completes as "not natural". Chained callbacks fire either way.
            StopRoutines();
            var outgoing = ActiveBgm;
            if (_activeHandle != null) _activeHandle.Complete(false);

            if (fadeOut > 0f && outgoing != null && outgoing.isPlaying)
            {
                _fadeOutRoutine = StartCoroutine(FadeOutAndStop(outgoing, fadeOut));
            }
            else if (outgoing != null)
            {
                outgoing.Stop();
            }

            _usingA = !_usingA;
            var incoming = ActiveBgm;

            _bgmScale = volumeScale * entry.Volume;
            _bgmPaused = false;

            incoming.clip = clip;
            incoming.loop = loop;
            incoming.pitch = entry.PickPitch();
            incoming.volume = fadeIn > 0f ? 0f : BgmTargetVolume;
            incoming.Play();

            if (fadeIn > 0f)
            {
                _fadeInRoutine = StartCoroutine(FadeTo(incoming, BgmTargetVolume, fadeIn));
            }

            var handle = new BgmHandle(entry.Key);
            _activeHandle = handle;

            // A looping track never ends on its own, so there is nothing to watch.
            if (!loop) _watchRoutine = StartCoroutine(WatchForEnd(incoming, clip, handle));

            return handle;
        }

        internal void StopBgm(float fadeOut)
        {
            StopRoutines();

            var source = ActiveBgm;
            if (source != null)
            {
                if (fadeOut > 0f && source.isPlaying)
                {
                    _fadeOutRoutine = StartCoroutine(FadeOutAndStop(source, fadeOut));
                }
                else
                {
                    source.Stop();
                }
            }

            _bgmPaused = false;

            if (_activeHandle != null)
            {
                _activeHandle.Complete(false);
                _activeHandle = null;
            }
        }

        internal void PauseBgm()
        {
            var source = ActiveBgm;
            if (source == null || !source.isPlaying) return;

            source.Pause();
            _bgmPaused = true;
        }

        internal void ResumeBgm()
        {
            if (!_bgmPaused) return;

            _bgmPaused = false;
            var source = ActiveBgm;
            if (source != null) source.UnPause();
        }

        /// <summary>
        /// Re-applies the mixed music volume after a global volume change. Skipped
        /// while a fade-in is running, because the fade owns the volume until it
        /// lands on its target.
        /// </summary>
        internal void RefreshBgmVolume()
        {
            var source = ActiveBgm;
            if (_fadeInRoutine == null && source != null) source.volume = BgmTargetVolume;
        }

        void StopRoutines()
        {
            if (_watchRoutine != null) { StopCoroutine(_watchRoutine); _watchRoutine = null; }
            if (_fadeInRoutine != null) { StopCoroutine(_fadeInRoutine); _fadeInRoutine = null; }
        }

        IEnumerator WatchForEnd(AudioSource source, AudioClip clip, BgmHandle handle)
        {
            // Give Play() a frame to take effect before trusting isPlaying.
            yield return null;

            while (source != null && source.clip == clip && (source.isPlaying || _bgmPaused))
            {
                yield return null;
            }

            _watchRoutine = null;

            // Only complete if this track is still the current one. A replacement
            // will have completed the handle itself.
            if (_activeHandle == handle)
            {
                _activeHandle = null;
                handle.Complete(true);
            }
        }

        IEnumerator FadeTo(AudioSource source, float target, float duration)
        {
            float start = source.volume;
            float elapsed = 0f;

            while (elapsed < duration && source != null)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, target, elapsed / duration);
                yield return null;
            }

            if (source != null) source.volume = target;
            _fadeInRoutine = null;
        }

        IEnumerator FadeOutAndStop(AudioSource source, float duration)
        {
            // Fading a source that is not the active one, so this deliberately does
            // not clear _fadeInRoutine ownership of the incoming track.
            float start = source.volume;
            float elapsed = 0f;

            while (elapsed < duration && source != null)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, 0f, elapsed / duration);
                yield return null;
            }

            if (source != null)
            {
                source.volume = 0f;
                source.Stop();
            }

            _fadeOutRoutine = null;
        }
    }
}
