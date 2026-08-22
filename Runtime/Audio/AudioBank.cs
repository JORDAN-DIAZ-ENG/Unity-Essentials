using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Essential.Audio
{
    /// <summary>
    /// The project's audio database: every sound, addressed by a stable string key.
    ///
    /// The bank is a plain asset rather than a scene object, so nothing has to be
    /// dragged into a scene for audio to work. <see cref="AudioManager"/> finds it
    /// through <see cref="Resources"/> at <see cref="ResourcePath"/>.
    ///
    /// Keys come from folder names under the source folders and are turned into
    /// compile-time constants by the editor generator, so call sites read
    /// <c>AudioManager.PlaySfx(Sfx.FootstepsGrass)</c> with full autocomplete.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioBank", menuName = "Essential/Audio Bank")]
    public sealed class AudioBank : ScriptableObject
    {
        /// <summary>
        /// Where the runtime looks for the active bank. Relative to any Resources
        /// folder, so the asset belongs at <c>Assets/Resources/Essential/AudioBank.asset</c>.
        /// </summary>
        public const string ResourcePath = "Essential/AudioBank";

        [SerializeField] internal AudioEntry[] _sfx = new AudioEntry[0];
        [SerializeField] internal AudioEntry[] _bgm = new AudioEntry[0];

        [Header("Mixing")]
        [Range(0f, 1f)]
        [SerializeField] internal float _masterVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] internal float _sfxVolume = 1f;
        [Range(0f, 1f)]
        [SerializeField] internal float _bgmVolume = 1f;

        [Tooltip("How many sound effects can overlap before the oldest one is cut off.")]
        [Range(1, 64)]
        [SerializeField] internal int _sfxVoiceCount = 16;

        [Tooltip("Optional. Leave empty to play straight out to the default mixer.")]
        [SerializeField] internal AudioMixerGroup _sfxMixerGroup;
        [SerializeField] internal AudioMixerGroup _bgmMixerGroup;

        [Header("Generation")]
        [Tooltip("Folder scanned for sound effects. Each subfolder becomes one sound; loose clips become one sound each.")]
        [SerializeField] internal string _sfxSourceFolder = "Assets/AudioBank/SFX";

        [Tooltip("Folder scanned for music. Each subfolder becomes one track; loose clips become one track each.")]
        [SerializeField] internal string _bgmSourceFolder = "Assets/AudioBank/BGM";

        [Tooltip("Where the generated Sfx/Bgm constant classes are written. Must live under Assets.")]
        [SerializeField] internal string _generatedScriptFolder = "Assets/Essential/Generated";

        [Tooltip("Optional namespace for the generated classes. Leave empty to put Sfx and Bgm in the global namespace.")]
        [SerializeField] internal string _generatedNamespace = "";

        [Tooltip("Rebuild automatically when clips are added to or removed from the source folders.")]
        [SerializeField] internal bool _rebuildOnAudioImport = true;

        Dictionary<string, AudioEntry> _sfxLookup;
        Dictionary<string, AudioEntry> _bgmLookup;

        public IReadOnlyList<AudioEntry> SfxEntries => _sfx;
        public IReadOnlyList<AudioEntry> BgmEntries => _bgm;

        public float MasterVolume => _masterVolume;
        public float SfxVolume => _sfxVolume;
        public float BgmVolume => _bgmVolume;
        public int SfxVoiceCount => _sfxVoiceCount;
        public AudioMixerGroup SfxMixerGroup => _sfxMixerGroup;
        public AudioMixerGroup BgmMixerGroup => _bgmMixerGroup;

        /// <summary>
        /// Looks up a sound effect. Returns null for unknown keys; callers are
        /// expected to warn rather than throw, because a missing sound should never
        /// take down a jam build mid-playtest.
        /// </summary>
        public AudioEntry GetSfx(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureLookups();
            return _sfxLookup.TryGetValue(key, out var entry) ? entry : null;
        }

        /// <summary>Looks up a music track. Returns null for unknown keys.</summary>
        public AudioEntry GetBgm(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            EnsureLookups();
            return _bgmLookup.TryGetValue(key, out var entry) ? entry : null;
        }

        void OnEnable() => InvalidateLookups();

        void OnValidate() => InvalidateLookups();

        internal void InvalidateLookups()
        {
            _sfxLookup = null;
            _bgmLookup = null;
        }

        void EnsureLookups()
        {
            if (_sfxLookup != null) return;

            _sfxLookup = BuildLookup(_sfx);
            _bgmLookup = BuildLookup(_bgm);
        }

        static Dictionary<string, AudioEntry> BuildLookup(AudioEntry[] entries)
        {
            var lookup = new Dictionary<string, AudioEntry>(
                entries == null ? 0 : entries.Length);

            if (entries == null) return lookup;

            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry._key)) continue;
                lookup[entry._key] = entry;
            }

            return lookup;
        }
    }
}
