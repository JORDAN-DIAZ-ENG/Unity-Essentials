using System;
using UnityEngine;

namespace Essential.Audio
{
    /// <summary>
    /// One named sound in an <see cref="AudioBank"/>.
    ///
    /// An entry owns every clip found in its source folder. Playing the entry picks
    /// one of those clips at random, which is what makes a folder of five footstep
    /// recordings behave like a single "FootstepsGrass" sound.
    ///
    /// The tuning fields are authored by hand in the inspector and are deliberately
    /// preserved when the bank is regenerated.
    /// </summary>
    [Serializable]
    public class AudioEntry
    {
        [Tooltip("Stable id. Matches the source folder name. Written by the generator - editing it by hand will break call sites.")]
        [SerializeField] internal string _key;

        [Tooltip("Every clip in the source folder. One is chosen at random each time this sound plays.")]
        [SerializeField] internal AudioClip[] _clips = Array.Empty<AudioClip>();

        [Header("Tuning (survives regeneration)")]
        [Tooltip("Baked-in volume trim for this sound, so call sites don't have to pass a magic number.")]
        [Range(0f, 2f)]
        [SerializeField] internal float _volume = 1f;

        [Tooltip("Pitch is randomised inside this range on every play. Set both ends to 1 to disable variation.")]
        [SerializeField] internal Vector2 _pitchRange = new Vector2(1f, 1f);

        [Tooltip("Default loop behaviour. Play calls can still override it.")]
        [SerializeField] internal bool _loop;

        /// <summary>The id this entry is addressed by, e.g. <c>"FootstepsGrass"</c>.</summary>
        public string Key => _key;

        /// <summary>Baked-in volume trim, applied on top of the volume passed to the play call.</summary>
        public float Volume => _volume;

        /// <summary>Default loop behaviour for this sound.</summary>
        public bool Loop => _loop;

        /// <summary>How many clips this entry can choose between.</summary>
        public int ClipCount => _clips == null ? 0 : _clips.Length;

        public bool HasClips => ClipCount > 0;

        /// <summary>Picks one of the entry's clips at random. Null when the entry is empty.</summary>
        public AudioClip PickClip()
        {
            if (!HasClips) return null;
            return _clips.Length == 1
                ? _clips[0]
                : _clips[UnityEngine.Random.Range(0, _clips.Length)];
        }

        /// <summary>
        /// Picks a specific clip by index, for sounds where the variant matters
        /// (a boss theme's intro/loop/outro, say). A negative index means "random".
        /// An out of range index returns null rather than throwing.
        /// </summary>
        public AudioClip GetClip(int index)
        {
            if (index < 0) return PickClip();
            if (!HasClips || index >= _clips.Length) return null;
            return _clips[index];
        }

        /// <summary>Rolls a pitch from the entry's pitch range.</summary>
        public float PickPitch()
        {
            return Mathf.Approximately(_pitchRange.x, _pitchRange.y)
                ? _pitchRange.x
                : UnityEngine.Random.Range(_pitchRange.x, _pitchRange.y);
        }
    }
}
