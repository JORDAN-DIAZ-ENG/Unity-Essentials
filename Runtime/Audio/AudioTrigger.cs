using UnityEngine;

namespace Essential.Audio
{
    /// <summary>
    /// Plays a sound without writing a script for it. Drop it on a button and hook
    /// <see cref="Play"/> to onClick, or set <see cref="_playOn"/> so a pickup makes
    /// its noise the moment it spawns.
    ///
    /// The sound field draws as a searchable dropdown of everything in the bank.
    /// </summary>
    [AddComponentMenu("Essential/Audio/Audio Trigger")]
    public sealed class AudioTrigger : MonoBehaviour
    {
        /// <summary>Which lifecycle moment fires the sound, if any.</summary>
        public enum TriggerMoment
        {
            /// <summary>Nothing automatic. Call <see cref="Play"/> yourself.</summary>
            Manual,
            Awake,
            Start,
            OnEnable,
            OnDisable,
            OnDestroy
        }

        [SerializeField] SfxRef _sound;

        [Tooltip("When the sound fires. Leave on Manual to drive it from a UnityEvent or from code.")]
        [SerializeField] TriggerMoment _playOn = TriggerMoment.Manual;

        [Range(0f, 2f)]
        [SerializeField] float _volume = 1f;

        [Tooltip("Play at this object's position so the sound pans and attenuates with distance.")]
        [SerializeField] bool _positional;

        /// <summary>The sound this trigger plays. Assign to swap it at runtime.</summary>
        public SfxRef Sound
        {
            get => _sound;
            set => _sound = value;
        }

        void Awake()
        {
            if (_playOn == TriggerMoment.Awake) Play();
        }

        void Start()
        {
            if (_playOn == TriggerMoment.Start) Play();
        }

        void OnEnable()
        {
            if (_playOn == TriggerMoment.OnEnable) Play();
        }

        void OnDisable()
        {
            if (_playOn == TriggerMoment.OnDisable) Play();
        }

        void OnDestroy()
        {
            // A sound fired here outlives this object because the pooled voice lives
            // on the persistent audio runtime, so destruction stingers work.
            if (_playOn == TriggerMoment.OnDestroy) Play();
        }

        /// <summary>Fires the sound. Safe to wire directly to a UnityEvent.</summary>
        public void Play()
        {
            if (_sound.IsEmpty) return;

            if (_positional)
                AudioManager.PlaySfxAt(_sound, transform.position, _volume);
            else
                AudioManager.PlaySfx(_sound, _volume);
        }
    }
}
