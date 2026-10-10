using UnityEngine;

namespace Essential.Juice
{
    /// <summary>
    /// Fires any mix of juice without writing a script for it. Hook
    /// <see cref="Play"/> to a UnityEvent such as a button's onClick or a health
    /// component's damaged event, or set <see cref="_playOn"/> so an explosion
    /// prefab shakes the screen the moment it spawns.
    /// </summary>
    [AddComponentMenu("Essential/Juice/Juice Trigger")]
    public sealed class JuiceTrigger : MonoBehaviour
    {
        /// <summary>Which lifecycle moment fires the effects, if any.</summary>
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

        /// <summary>Which scale punch, if any, the trigger plays on its visual.</summary>
        public enum ScalePunch
        {
            None,
            Pop,
            Squash,
            Stretch
        }

        [Tooltip("When the effects fire. Leave on Manual to drive them from a UnityEvent or from code.")]
        [SerializeField] TriggerMoment _playOn = TriggerMoment.Manual;

        [Tooltip("What flashes and punches. Leave empty to use this object. Point it at a visual child when this object has a Rigidbody or an Animator.")]
        [SerializeField] Transform _visual;

        [Header("Shake")]
        [SerializeField] bool _shake = true;
        [SerializeField] ShakeStrength _shakeStrength = ShakeStrength.Medium;

        [Tooltip("Fade the shake with this object's distance from the camera, so far-off events rumble less.")]
        [SerializeField] bool _shakeFromHere;

        [Header("Camera Kick")]
        [SerializeField] bool _kick;

        [Tooltip("Direction to push the camera, in this object's local space.")]
        [SerializeField] Vector3 _kickDirection = Vector3.back;

        [Tooltip("How far the camera travels at the peak of the push, in world units.")]
        [Min(0f)]
        [SerializeField] float _kickDistance = 0.2f;

        [Header("Hitstop")]
        [SerializeField] bool _hitstop;
        [SerializeField] HitstopStrength _hitstopStrength = HitstopStrength.Light;

        [Header("Slow Motion")]
        [SerializeField] bool _slowMotion;

        [Range(0f, 1f)]
        [SerializeField] float _slowMotionTimeScale = 0.2f;

        [Tooltip("Real-time seconds to hold the slow motion before easing back.")]
        [Min(0f)]
        [SerializeField] float _slowMotionHoldSeconds = 0.5f;

        [Header("Flash")]
        [Tooltip("Flash the visual and its children in the color and length set on JuiceSettings.")]
        [SerializeField] bool _flash;

        [Header("Punch")]
        [SerializeField] ScalePunch _scalePunch = ScalePunch.None;

        [Tooltip("How far the scale punch goes at its peak: 0.2 is 20%.")]
        [Range(0f, 1f)]
        [SerializeField] float _scalePunchAmount = 0.2f;

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
            // Screen-level effects run on the persistent juice runtime, so they
            // outlive this object and a destruction shake plays out in full. Flash
            // and punch target this object, so they are skipped: it is going away.
            if (_playOn == TriggerMoment.OnDestroy) PlayScreenEffects();
        }

        /// <summary>Fires every enabled effect. Safe to wire directly to a UnityEvent.</summary>
        public void Play()
        {
            PlayScreenEffects();
            PlayObjectEffects();
        }

        void PlayScreenEffects()
        {
            if (_shake) PlayShake();
            if (_kick) Juice.Kick(transform.TransformDirection(_kickDirection), _kickDistance);
            if (_hitstop) Juice.Hitstop(_hitstopStrength);
            if (_slowMotion) Juice.SlowMotion(_slowMotionTimeScale, _slowMotionHoldSeconds);
        }

        void PlayObjectEffects()
        {
            var visual = _visual != null ? _visual : transform;

            if (_flash) Juice.Flash(visual.gameObject);
            PlayScalePunch(visual);
        }

        void PlayShake()
        {
            if (_shakeFromHere)
                Juice.ShakeAt(_shakeStrength, transform.position);
            else
                Juice.Shake(_shakeStrength);
        }

        void PlayScalePunch(Transform visual)
        {
            switch (_scalePunch)
            {
                case ScalePunch.Pop: Juice.PunchScale(visual, _scalePunchAmount); break;
                case ScalePunch.Squash: Juice.Squash(visual, _scalePunchAmount); break;
                case ScalePunch.Stretch: Juice.Stretch(visual, _scalePunchAmount); break;
            }
        }
    }
}
