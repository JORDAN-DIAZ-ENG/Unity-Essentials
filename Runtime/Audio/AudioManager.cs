using UnityEngine;

namespace Essential.Audio
{
    /// <summary>
    /// The audio API. Everything a game needs is a static call away, with no
    /// manager to place in the scene and no Instance to null-check:
    ///
    /// <code>
    /// AudioManager.PlaySfx(Sfx.Heal);
    /// AudioManager.PlaySfxAt(Sfx.FootstepsGrass, transform.position);
    /// AudioManager.PlayBgm(Bgm.BossTheme, fadeIn: 1f)
    ///             .Finished += StartNextTrack;
    /// </code>
    ///
    /// The Sfx and Bgm constants are generated into the game's own scripts folder
    /// from the bank's source folders, so the package never has to know about them.
    /// </summary>
    public static class AudioManager
    {
        static AudioBank _bank;
        static bool _bankResolved;

        // Live volume, seeded from the bank but never written back to it. Editing
        // the asset at runtime would leave a jam project's tuning permanently
        // altered by whatever the last playtest's volume slider was set to.
        static float _masterVolume = 1f;
        static float _sfxVolume = 1f;
        static float _bgmVolume = 1f;
        static bool _volumesSeeded;

        /// <summary>
        /// The active bank. Resolved from Resources on first use; assign it
        /// directly to swap banks at runtime or to inject a fixture in a test.
        /// </summary>
        public static AudioBank Bank
        {
            get
            {
                if (!_bankResolved) ResolveBank();
                return _bank;
            }
            set
            {
                _bank = value;
                _bankResolved = true;

                var runtime = AudioRuntime.Existing;
                if (runtime != null) runtime.Bank = value;
            }
        }

        /// <summary>True once a bank has been found and audio calls will do something.</summary>
        public static bool IsReady => Bank != null;

        /// <summary>True while a music track is playing or paused.</summary>
        public static bool IsBgmPlaying
        {
            get
            {
                var runtime = AudioRuntime.Existing;
                return runtime != null && runtime.IsBgmPlaying;
            }
        }

        // ------------------------------------------------------------------
        // Volume
        // ------------------------------------------------------------------

        /// <summary>
        /// Overall volume, multiplied into both music and effects. Changes take
        /// effect on the currently playing music immediately and on effects as they
        /// are triggered. Persisting this to PlayerPrefs is the game's job.
        /// </summary>
        public static float MasterVolume
        {
            get { EnsureVolumes(); return _masterVolume; }
            set { EnsureVolumes(); _masterVolume = Mathf.Clamp01(value); RefreshVolume(); }
        }

        /// <summary>Volume for sound effects only.</summary>
        public static float SfxVolume
        {
            get { EnsureVolumes(); return _sfxVolume; }
            set { EnsureVolumes(); _sfxVolume = Mathf.Clamp01(value); }
        }

        /// <summary>Volume for music only.</summary>
        public static float BgmVolume
        {
            get { EnsureVolumes(); return _bgmVolume; }
            set { EnsureVolumes(); _bgmVolume = Mathf.Clamp01(value); RefreshVolume(); }
        }

        // ------------------------------------------------------------------
        // Sound effects
        // ------------------------------------------------------------------

        /// <summary>
        /// Plays a sound effect on a pooled voice, picking a random clip and a
        /// random pitch inside the range configured on the bank entry.
        /// </summary>
        /// <param name="sfx">A generated constant such as <c>Sfx.Heal</c>, or a serialized <see cref="SfxRef"/>.</param>
        /// <param name="volume">Multiplied on top of the entry's own volume trim.</param>
        /// <param name="clipIndex">Which variant to play. Leave at -1 to pick at random.</param>
        /// <returns>The voice the sound is playing on, or null if it could not play.</returns>
        public static AudioSource PlaySfx(SfxRef sfx, float volume = 1f, int clipIndex = -1)
        {
            return PlaySfxInternal(sfx, volume, clipIndex, null, loop: false);
        }

        /// <summary>
        /// Plays a sound effect positioned in the world, so it pans and attenuates
        /// with distance. Use this for anything that happens at a place rather than
        /// to the player.
        /// </summary>
        public static AudioSource PlaySfxAt(SfxRef sfx, Vector3 position, float volume = 1f, int clipIndex = -1)
        {
            return PlaySfxInternal(sfx, volume, clipIndex, position, loop: false);
        }

        /// <summary>
        /// Starts a looping sound effect, for things like an engine hum or a
        /// charging beam. The caller owns the returned source and is responsible for
        /// calling <see cref="StopSfx"/> on it.
        /// </summary>
        public static AudioSource PlaySfxLooping(SfxRef sfx, float volume = 1f, int clipIndex = -1)
        {
            return PlaySfxInternal(sfx, volume, clipIndex, null, loop: true);
        }

        /// <summary>Stops a source returned by one of the PlaySfx calls. Safe to call with null.</summary>
        public static void StopSfx(AudioSource source)
        {
            if (source == null) return;

            source.Stop();
            source.loop = false;
            source.clip = null;
        }

        static AudioSource PlaySfxInternal(SfxRef sfx, float volume, int clipIndex, Vector3? position, bool loop)
        {
            var entry = ResolveSfx(sfx);
            if (entry == null) return null;

            var runtime = AudioRuntime.Get();
            if (runtime == null) return null;

            return runtime.PlaySfx(entry, volume, clipIndex, position, loop);
        }

        // ------------------------------------------------------------------
        // Music
        // ------------------------------------------------------------------

        /// <summary>
        /// Plays a music track, replacing whatever was playing. Music loops by
        /// default, since that is what a track usually wants; pass
        /// <c>loop: false</c> for stingers and use the returned handle to chain.
        /// </summary>
        /// <param name="bgm">A generated constant such as <c>Bgm.BossTheme</c>, or a serialized <see cref="BgmRef"/>.</param>
        /// <param name="loop">Leave null to use the entry's own default.</param>
        /// <param name="fadeIn">Seconds to fade the new track up from silence.</param>
        /// <param name="fadeOut">Seconds to fade the outgoing track down. Overlaps with fadeIn, giving a crossfade.</param>
        /// <param name="clipIndex">Which variant to play. Leave at -1 to pick at random.</param>
        /// <returns>A handle that reports when the track ends, or null if it could not play.</returns>
        public static BgmHandle PlayBgm(BgmRef bgm, bool? loop = null, float volume = 1f,
                                        float fadeIn = 0f, float fadeOut = 0f, int clipIndex = -1)
        {
            var entry = ResolveBgm(bgm);
            if (entry == null) return null;

            var runtime = AudioRuntime.Get();
            if (runtime == null) return null;

            return runtime.PlayBgm(entry, loop ?? entry.Loop, volume, clipIndex, fadeIn, fadeOut);
        }

        /// <summary>Stops the current track, optionally fading it out first.</summary>
        public static void StopBgm(float fadeOut = 0f)
        {
            var runtime = AudioRuntime.Existing;
            if (runtime != null) runtime.StopBgm(fadeOut);
        }

        /// <summary>Pauses the current track where it is. Pair with <see cref="ResumeBgm"/>.</summary>
        public static void PauseBgm()
        {
            var runtime = AudioRuntime.Existing;
            if (runtime != null) runtime.PauseBgm();
        }

        /// <summary>Resumes a track paused by <see cref="PauseBgm"/>.</summary>
        public static void ResumeBgm()
        {
            var runtime = AudioRuntime.Existing;
            if (runtime != null) runtime.ResumeBgm();
        }

        // ------------------------------------------------------------------
        // Lookup
        // ------------------------------------------------------------------

        /// <summary>
        /// Fetches a bank entry, for code that wants clip length or clip count
        /// rather than to play something. Returns null and warns for unknown keys.
        /// </summary>
        public static AudioEntry ResolveSfx(SfxRef sfx)
        {
            var bank = Bank;
            if (bank == null)
            {
                WarnNoBank();
                return null;
            }

            var entry = bank.GetSfx(sfx.Key);
            if (entry == null)
            {
                Debug.LogWarning($"[Essential.Audio] No sound effect named '{sfx.Key}' in the bank. " +
                                 "Rebuild the bank if you just added the folder.");
                return null;
            }

            if (!entry.HasClips)
            {
                Debug.LogWarning($"[Essential.Audio] Sound effect '{sfx.Key}' has no clips.");
                return null;
            }

            return entry;
        }

        /// <summary>The music-side twin of <see cref="ResolveSfx"/>.</summary>
        public static AudioEntry ResolveBgm(BgmRef bgm)
        {
            var bank = Bank;
            if (bank == null)
            {
                WarnNoBank();
                return null;
            }

            var entry = bank.GetBgm(bgm.Key);
            if (entry == null)
            {
                Debug.LogWarning($"[Essential.Audio] No music track named '{bgm.Key}' in the bank. " +
                                 "Rebuild the bank if you just added the folder.");
                return null;
            }

            if (!entry.HasClips)
            {
                Debug.LogWarning($"[Essential.Audio] Music track '{bgm.Key}' has no clips.");
                return null;
            }

            return entry;
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _bank = null;
            _bankResolved = false;
            _volumesSeeded = false;
        }

        static void ResolveBank()
        {
            _bankResolved = true;
            _bank = Resources.Load<AudioBank>(AudioBank.ResourcePath);
        }

        static void EnsureVolumes()
        {
            if (_volumesSeeded) return;

            var bank = Bank;
            if (bank == null) return;

            _masterVolume = bank.MasterVolume;
            _sfxVolume = bank.SfxVolume;
            _bgmVolume = bank.BgmVolume;
            _volumesSeeded = true;
        }

        static void RefreshVolume()
        {
            var runtime = AudioRuntime.Existing;
            if (runtime != null) runtime.RefreshBgmVolume();
        }

        static void WarnNoBank()
        {
            Debug.LogWarning("[Essential.Audio] No AudioBank found at Resources/" + AudioBank.ResourcePath +
                             ". Run Tools > Essential > Audio > Set Up Audio to create one.");
        }
    }
}
