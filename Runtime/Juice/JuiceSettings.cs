using UnityEngine;

namespace Essential.Juice
{
    /// <summary>
    /// Tuning for every juice effect. Optional: when no asset exists at
    /// <see cref="ResourcePath"/>, <see cref="Juice"/> runs on an in-memory instance
    /// holding the defaults below, so a project gets shake and hitstop with zero
    /// setup and only creates the asset once it wants to tune them.
    /// </summary>
    [CreateAssetMenu(fileName = "JuiceSettings", menuName = "Essential/Juice Settings")]
    public sealed class JuiceSettings : ScriptableObject
    {
        /// <summary>
        /// Where the runtime looks for the settings. Relative to any Resources
        /// folder, so the asset belongs at <c>Assets/Resources/Essential/JuiceSettings.asset</c>.
        /// </summary>
        public const string ResourcePath = "Essential/JuiceSettings";

        [Header("Shake Presets")]
        [Tooltip("Trauma added by ShakeStrength.Light. Trauma caps at 1.")]
        [Range(0f, 1f)]
        [SerializeField] float _lightTrauma = 0.25f;
        [Range(0f, 1f)]
        [SerializeField] float _mediumTrauma = 0.5f;
        [Range(0f, 1f)]
        [SerializeField] float _heavyTrauma = 0.8f;

        [Header("Shake Feel")]
        [Tooltip("Trauma lost per real-time second. 1 means a full-trauma shake settles in one second.")]
        [Min(0.01f)]
        [SerializeField] float _traumaDecayPerSecond = 1.2f;

        [Tooltip("Shake amount is trauma raised to this power. 2 makes small hits subtle and big hits violent; 1 is linear.")]
        [Range(1f, 4f)]
        [SerializeField] float _traumaExponent = 2f;

        [Tooltip("How fast the shake jitters, in noise cycles per second.")]
        [Min(0.1f)]
        [SerializeField] float _shakeFrequencyHz = 20f;

        [Header("Perspective Cameras")]
        [Tooltip("Largest positional offset at full trauma, in world units along the camera's own axes.")]
        [Min(0f)]
        [SerializeField] float _perspectiveMaxOffset = 0.1f;

        [Tooltip("Largest rotation at full trauma, in degrees: x is pitch, y is yaw, z is roll.")]
        [SerializeField] Vector3 _perspectiveMaxAngles = new Vector3(2f, 2f, 4f);

        [Header("Orthographic Cameras")]
        [Tooltip("Largest offset at full trauma, as a fraction of the camera's orthographicSize, so the shake looks the same at any zoom level.")]
        [Range(0f, 0.5f)]
        [SerializeField] float _orthographicMaxOffsetFraction = 0.06f;

        [Tooltip("Largest roll at full trauma, in degrees.")]
        [Min(0f)]
        [SerializeField] float _orthographicMaxRollDegrees = 2f;

        [Header("Positional Shake (ShakeAt)")]
        [Tooltip("Within this distance of the camera, ShakeAt adds its full trauma. Orthographic cameras measure in the view plane.")]
        [Min(0f)]
        [SerializeField] float _fullStrengthDistance = 5f;

        [Tooltip("At or beyond this distance, ShakeAt adds nothing. Between the two, trauma fades linearly.")]
        [Min(0f)]
        [SerializeField] float _silentDistance = 30f;

        [Header("Kick")]
        [Tooltip("How fast a kicked camera springs back, in oscillations per second.")]
        [Min(0.1f)]
        [SerializeField] float _kickFrequencyHz = 6f;

        [Tooltip("How quickly a kick stops wobbling. Low values overshoot and bounce; values near 1 return without bouncing.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] float _kickDampingRatio = 0.4f;

        [Header("Hitstop")]
        [Tooltip("Seconds of real time frozen by HitstopStrength.Light.")]
        [Range(0f, 1f)]
        [SerializeField] float _lightHitstopSeconds = 0.04f;
        [Range(0f, 1f)]
        [SerializeField] float _mediumHitstopSeconds = 0.08f;
        [Range(0f, 1f)]
        [SerializeField] float _heavyHitstopSeconds = 0.15f;

        [Tooltip("Time scale during a hitstop, multiplied onto Juice.TimeScale. 0 is a full freeze; a little above 0 gives a heavy slow-motion stutter instead.")]
        [Range(0f, 1f)]
        [SerializeField] float _hitstopTimeScale = 0f;

        [Header("Slow Motion")]
        [Tooltip("Real-time seconds SlowMotion takes to ease back to full speed when no recovery time is passed.")]
        [Min(0f)]
        [SerializeField] float _slowMotionRecoverSeconds = 0.4f;

        [Header("Punch (PunchScale, Squash, PunchPosition, PunchRotation)")]
        [Tooltip("How fast punched objects wobble, in oscillations per second. Higher feels snappier.")]
        [Min(0.1f)]
        [SerializeField] float _punchFrequencyHz = 7f;

        [Tooltip("How quickly a punch stops wobbling. Low values jiggle like jelly; values near 1 pop once and settle.")]
        [Range(0.05f, 0.95f)]
        [SerializeField] float _punchDampingRatio = 0.25f;

        [Header("Hit Flash")]
        [SerializeField] Color _flashColor = Color.white;

        [Tooltip("Real-time seconds a flash lasts. One or two frames' worth reads as a hit; longer reads as a status effect.")]
        [Range(0.01f, 1f)]
        [SerializeField] float _flashSeconds = 0.08f;

        [Header("Screen Effects (URP only)")]
        [Tooltip("Pulse post-processing with trauma, so shakes also smear and darken the screen. Needs URP, and Post Processing ticked on the camera.")]
        [SerializeField] bool _screenEffectsEnabled = true;

        [Tooltip("Chromatic aberration intensity at full trauma. 0 leaves it alone.")]
        [Range(0f, 1f)]
        [SerializeField] float _maxChromaticAberration = 0.6f;

        [Tooltip("Vignette intensity at full trauma. 0 leaves it alone.")]
        [Range(0f, 1f)]
        [SerializeField] float _maxVignette = 0.35f;

        [Tooltip("Lens distortion at full trauma. Negative pinches the image, positive bulges it. 0 leaves it alone.")]
        [Range(-1f, 1f)]
        [SerializeField] float _maxLensDistortion = 0f;

        public float TraumaDecayPerSecond => _traumaDecayPerSecond;
        public float TraumaExponent => _traumaExponent;
        public float ShakeFrequencyHz => _shakeFrequencyHz;
        public float PerspectiveMaxOffset => _perspectiveMaxOffset;
        public Vector3 PerspectiveMaxAngles => _perspectiveMaxAngles;
        public float OrthographicMaxOffsetFraction => _orthographicMaxOffsetFraction;
        public float OrthographicMaxRollDegrees => _orthographicMaxRollDegrees;
        public float FullStrengthDistance => _fullStrengthDistance;
        public float SilentDistance => _silentDistance;
        public float KickFrequencyHz => _kickFrequencyHz;
        public float KickDampingRatio => _kickDampingRatio;
        public float HitstopTimeScale => _hitstopTimeScale;
        public float SlowMotionRecoverSeconds => _slowMotionRecoverSeconds;
        public float PunchFrequencyHz => _punchFrequencyHz;
        public float PunchDampingRatio => _punchDampingRatio;
        public Color FlashColor => _flashColor;
        public float FlashSeconds => _flashSeconds;
        public bool ScreenEffectsEnabled => _screenEffectsEnabled;
        public float MaxChromaticAberration => _maxChromaticAberration;
        public float MaxVignette => _maxVignette;
        public float MaxLensDistortion => _maxLensDistortion;

        public float TraumaFor(ShakeStrength strength)
        {
            switch (strength)
            {
                case ShakeStrength.Light: return _lightTrauma;
                case ShakeStrength.Heavy: return _heavyTrauma;
                default: return _mediumTrauma;
            }
        }

        public float SecondsFor(HitstopStrength strength)
        {
            switch (strength)
            {
                case HitstopStrength.Light: return _lightHitstopSeconds;
                case HitstopStrength.Heavy: return _heavyHitstopSeconds;
                default: return _mediumHitstopSeconds;
            }
        }

        void OnValidate()
        {
            if (_silentDistance < _fullStrengthDistance) _silentDistance = _fullStrengthDistance;
        }
    }
}
