using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Essential.Juice.URP
{
    /// <summary>
    /// Pulses post-processing with camera trauma: chromatic aberration, vignette
    /// and lens distortion rise with each hit and settle as the shake does, so they
    /// always match the shake without being tuned separately.
    ///
    /// Attaches itself to the juice runtime when URP is installed, and does nothing
    /// otherwise. It works through a high-priority global Volume that it creates
    /// for itself, blended in by trauma, so the scene's own volumes are never edited.
    /// Scaled by <see cref="Juice.ShakeIntensity"/>, because players who turn shake
    /// down for comfort want these effects down too.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class JuiceScreenEffects : MonoBehaviour
    {
        // Far above any volume a game is likely to author, so the pulse wins the blend.
        const float VolumePriority = 1000f;

        Volume _volume;
        VolumeProfile _profile;
        ChromaticAberration _chromaticAberration;
        Vignette _vignette;
        LensDistortion _lensDistortion;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Register()
        {
            // AfterAssembliesLoaded runs after the core's SubsystemRegistration reset,
            // so this subscription is never wiped. The -= first keeps it single when
            // domain reload is disabled and this runs again in the same domain.
            JuiceRuntime.Created -= Attach;
            JuiceRuntime.Created += Attach;

            var existing = JuiceRuntime.Existing;
            if (existing != null) Attach(existing);
        }

        static void Attach(JuiceRuntime runtime)
        {
            if (runtime.GetComponent<JuiceScreenEffects>() == null) runtime.gameObject.AddComponent<JuiceScreenEffects>();
        }

        void Awake()
        {
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "Juice Screen Effects";
            _profile.hideFlags = HideFlags.DontSave;

            _chromaticAberration = _profile.Add<ChromaticAberration>();
            _vignette = _profile.Add<Vignette>();
            _lensDistortion = _profile.Add<LensDistortion>();

            _volume = gameObject.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = VolumePriority;
            _volume.sharedProfile = _profile;
            _volume.weight = 0f;
            _volume.enabled = false;
        }

        void OnDestroy()
        {
            if (_profile != null) Destroy(_profile);
        }

        void Update()
        {
            var settings = Juice.Settings;
            float weight = settings.ScreenEffectsEnabled
                ? Mathf.Pow(Juice.Trauma, settings.TraumaExponent) * Juice.ShakeIntensity
                : 0f;

            // A disabled volume costs nothing in the blend, which is the common case.
            _volume.enabled = weight > 0f;
            if (!_volume.enabled) return;

            ApplyTargets(settings);
            _volume.weight = weight;
        }

        // Re-applied while active so tuning edits show up during play. A zero
        // target leaves that effect to the scene's own volumes instead of blending
        // it towards zero.
        void ApplyTargets(JuiceSettings settings)
        {
            SetTarget(_chromaticAberration.intensity, settings.MaxChromaticAberration);
            SetTarget(_vignette.intensity, settings.MaxVignette);
            SetTarget(_lensDistortion.intensity, settings.MaxLensDistortion);
        }

        static void SetTarget(VolumeParameter<float> parameter, float target)
        {
            parameter.overrideState = !Mathf.Approximately(target, 0f);
            parameter.value = target;
        }
    }
}
