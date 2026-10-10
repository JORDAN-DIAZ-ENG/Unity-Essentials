using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Essential.Juice
{
    /// <summary>
    /// The juice API: screen shake, hitstop, slow motion, hit flashes and punches,
    /// a static call away, with no manager to place in the scene:
    ///
    /// <code>
    /// Juice.Shake(ShakeStrength.Medium);
    /// Juice.ShakeAt(ShakeStrength.Heavy, explosion.position);
    /// Juice.Kick(-transform.forward, 0.2f);
    /// Juice.Hitstop(HitstopStrength.Light);
    /// Juice.SlowMotion(0.2f, holdSeconds: 0.6f);
    /// Juice.Flash(enemy.gameObject);
    /// Juice.Squash(player.visual, 0.3f);
    /// </code>
    ///
    /// Juice owns <see cref="Time.timeScale"/> so that a hitstop can never restore
    /// the wrong value over a pause menu. Games pause and slow down through
    /// <see cref="TimeScale"/> instead of writing Time.timeScale directly.
    /// </summary>
    public static class Juice
    {
        // Unity rejects time scales above this.
        const float MaxTimeScale = 100f;

        static JuiceSettings _settings;
        static bool _settingsResolved;

        static Camera _cameraOverride;
        static Camera _mainCamera;
        static bool _sceneHookInstalled;

        // Player-facing preferences, deliberately not stored on the settings asset:
        // those are the game's tuning, these are one player's comfort choices.
        static float _shakeIntensity = 1f;
        static bool _hitstopEnabled = true;

        static float _timeScale = 1f;

        /// <summary>
        /// The active settings. Resolved from Resources on first use, falling back
        /// to built-in defaults when no asset exists. Assign to swap tuning at
        /// runtime, or assign null to go back to the Resources asset.
        /// </summary>
        public static JuiceSettings Settings
        {
            get
            {
                if (!_settingsResolved) ResolveSettings();
                return _settings;
            }
            set
            {
                _settings = value;
                _settingsResolved = value != null;
            }
        }

        /// <summary>
        /// The camera that shakes. Defaults to <see cref="UnityEngine.Camera.main"/>,
        /// re-found after every scene load. Assign to shake a different camera;
        /// assign null to go back to the main camera.
        /// </summary>
        public static Camera Camera
        {
            get
            {
                if (_cameraOverride != null) return _cameraOverride;
                return MainCamera;
            }
            set => _cameraOverride = value;
        }

        // ------------------------------------------------------------------
        // Player preferences
        // ------------------------------------------------------------------

        /// <summary>
        /// Scales every shake and kick, for a "screen shake" accessibility slider.
        /// 0 turns shake off entirely. Persisting this to PlayerPrefs is the game's job.
        /// </summary>
        public static float ShakeIntensity
        {
            get => _shakeIntensity;
            set => _shakeIntensity = Mathf.Clamp01(value);
        }

        /// <summary>
        /// Turns hitstop on or off, for players who find freeze frames disorienting.
        /// Turning it off mid-freeze ends the freeze immediately.
        /// </summary>
        public static bool HitstopEnabled
        {
            get => _hitstopEnabled;
            set
            {
                _hitstopEnabled = value;
                if (!value) EndHitstop();
            }
        }

        // ------------------------------------------------------------------
        // Time
        // ------------------------------------------------------------------

        /// <summary>
        /// The game's own time scale: set 0 to pause, 0.5 for a lasting slow-motion
        /// power-up. Hitstop and <see cref="SlowMotion(float, float)"/> multiply on
        /// top of this and hand it back intact when they end. Write this rather
        /// than <see cref="Time.timeScale"/>, which Juice overwrites whenever one
        /// of its time effects starts, changes or ends.
        /// </summary>
        public static float TimeScale
        {
            get => _timeScale;
            set
            {
                _timeScale = Mathf.Clamp(value, 0f, MaxTimeScale);
                ApplyTimeScale();
            }
        }

        /// <summary>True while a hitstop is holding time.</summary>
        public static bool IsHitstopActive
        {
            get
            {
                var runtime = JuiceRuntime.Existing;
                return runtime != null && runtime.TimeEffects.IsHitstopActive;
            }
        }

        /// <summary>True while a slow motion is holding or easing back.</summary>
        public static bool IsSlowMotionActive
        {
            get
            {
                var runtime = JuiceRuntime.Existing;
                return runtime != null && runtime.TimeEffects.IsSlowMotionActive;
            }
        }

        /// <summary>
        /// Freezes time briefly on impact. The length is tuned per strength on
        /// <see cref="JuiceSettings"/>.
        /// </summary>
        public static void Hitstop(HitstopStrength strength)
        {
            Hitstop(Settings.SecondsFor(strength));
        }

        /// <summary>
        /// Freezes time for this many real-time seconds. A hitstop requested while
        /// one is running extends it to whichever ends later; they do not add up.
        /// </summary>
        public static void Hitstop(float seconds)
        {
            if (!_hitstopEnabled || seconds <= 0f) return;

            var runtime = JuiceRuntime.Get();
            if (runtime == null) return;

            runtime.TimeEffects.StartHitstop(seconds);
            ApplyTimeScale();
        }

        /// <summary>Ends a running hitstop early, if there is one.</summary>
        public static void EndHitstop()
        {
            var runtime = JuiceRuntime.Existing;
            if (runtime == null) return;

            runtime.TimeEffects.EndHitstop();
            ApplyTimeScale();
        }

        /// <summary>
        /// Drops time to <paramref name="timeScale"/> at once, holds it for
        /// <paramref name="holdSeconds"/> of real time, then eases back to full
        /// speed over the recovery time set on <see cref="JuiceSettings"/>. Use it
        /// for final kills, near misses and parries. A new call replaces one
        /// already running.
        /// </summary>
        public static void SlowMotion(float timeScale, float holdSeconds)
        {
            SlowMotion(timeScale, holdSeconds, Settings.SlowMotionRecoverSeconds);
        }

        /// <summary>
        /// <see cref="SlowMotion(float, float)"/> with an explicit recovery time:
        /// 0 snaps straight back to full speed, longer values let the moment linger.
        /// </summary>
        public static void SlowMotion(float timeScale, float holdSeconds, float recoverSeconds)
        {
            var runtime = JuiceRuntime.Get();
            if (runtime == null) return;

            runtime.TimeEffects.StartSlowMotion(Mathf.Clamp01(timeScale), Mathf.Max(0f, holdSeconds), Mathf.Max(0f, recoverSeconds));
            ApplyTimeScale();
        }

        /// <summary>
        /// Cuts a slow motion's hold short and eases back to full speed from
        /// wherever it is now. Pass 0 to snap straight back.
        /// </summary>
        public static void EndSlowMotion(float recoverSeconds = 0f)
        {
            var runtime = JuiceRuntime.Existing;
            if (runtime == null) return;

            runtime.TimeEffects.EndSlowMotion(Mathf.Max(0f, recoverSeconds));
            ApplyTimeScale();
        }

        // ------------------------------------------------------------------
        // Shake
        // ------------------------------------------------------------------

        /// <summary>
        /// Current trauma, 0 to 1. Useful for driving other effects off the same
        /// value, like a vignette or a controller rumble.
        /// </summary>
        public static float Trauma
        {
            get
            {
                var runtime = JuiceRuntime.Existing;
                return runtime != null ? runtime.Shake.Trauma : 0f;
            }
        }

        /// <summary>
        /// Shakes the camera. Shakes stack: several hits in a row build up trauma
        /// and the shake grows, then everything settles as trauma drains away.
        /// </summary>
        public static void Shake(ShakeStrength strength)
        {
            AddTrauma(Settings.TraumaFor(strength));
        }

        /// <summary>
        /// Shakes the camera, scaled down by how far <paramref name="position"/> is
        /// from it, so a distant explosion rumbles and a close one rattles. Use this
        /// for anything that happens at a place rather than to the player.
        /// </summary>
        public static void ShakeAt(ShakeStrength strength, Vector3 position)
        {
            AddTraumaAt(Settings.TraumaFor(strength), position);
        }

        /// <summary>
        /// Adds raw trauma, for when the presets are too coarse, such as shake
        /// scaled by damage dealt. Trauma caps at 1.
        /// </summary>
        public static void AddTrauma(float trauma)
        {
            if (trauma <= 0f) return;

            var runtime = JuiceRuntime.Get();
            if (runtime != null) runtime.Shake.AddTrauma(trauma);
        }

        /// <summary>The raw-trauma twin of <see cref="ShakeAt"/>.</summary>
        public static void AddTraumaAt(float trauma, Vector3 position)
        {
            AddTrauma(trauma * DistanceFalloff(position));
        }

        /// <summary>
        /// Knocks the camera along <paramref name="direction"/> and lets it spring
        /// back: a single push rather than a rattle. Use it for recoil (kick
        /// opposite the aim) or to show where a hit came from.
        /// </summary>
        /// <param name="direction">World-space direction. It is normalized, so only its heading matters.</param>
        /// <param name="distance">How far the camera travels at the peak of the push, in world units.</param>
        public static void Kick(Vector3 direction, float distance)
        {
            if (distance <= 0f) return;

            var runtime = JuiceRuntime.Get();
            if (runtime != null) runtime.Shake.AddKick(direction, distance, Settings);
        }

        /// <summary>
        /// Stops all shake and kick at once, for cutscenes and scene transitions
        /// where leftover shake would look like a bug.
        /// </summary>
        public static void StopShake()
        {
            var runtime = JuiceRuntime.Existing;
            if (runtime != null) runtime.Shake.Clear();
        }

        // ------------------------------------------------------------------
        // Hit flash
        // ------------------------------------------------------------------

        /// <summary>
        /// Flashes every mesh and sprite renderer on <paramref name="target"/> and
        /// its children in the color and for the length set on <see cref="JuiceSettings"/>.
        /// The quickest way to show that something got hit.
        /// </summary>
        public static void Flash(GameObject target)
        {
            var settings = Settings;
            Flash(target, settings.FlashColor, settings.FlashSeconds);
        }

        /// <summary>
        /// Flashes <paramref name="target"/> in a chosen color, such as red for
        /// damage or green for healing. Runs on real time, so it holds through a
        /// hitstop. Flashing again while lit changes the color and extends the flash.
        /// </summary>
        /// <remarks>
        /// The flash swaps the renderers' materials and swaps them back when it ends,
        /// so a material the game assigns mid-flash is overwritten by the original.
        /// </remarks>
        public static void Flash(GameObject target, Color color, float seconds)
        {
            if (target == null || seconds <= 0f) return;

            var runtime = JuiceRuntime.Get();
            if (runtime == null) return;

            target.GetComponentsInChildren(FlashableBuffer);
            foreach (var renderer in FlashableBuffer)
            {
                if (IsFlashable(renderer)) runtime.Flashes.Start(renderer, color, seconds);
            }

            FlashableBuffer.Clear();
        }

        // ------------------------------------------------------------------
        // Punch
        // ------------------------------------------------------------------

        /// <summary>
        /// Pops <paramref name="target"/>'s scale up by <paramref name="amount"/>
        /// (0.2 is 20% bigger at the peak) and lets it wobble back. Good for
        /// pickups, button presses and anything that just got hit.
        /// </summary>
        /// <remarks>
        /// Punches, squashes and stretches write the transform, so aim them at a
        /// visual child rather than a Rigidbody, and at a transform an Animator
        /// does not also drive. Every punch on one transform layers together.
        /// </remarks>
        public static void PunchScale(Transform target, float amount)
        {
            PunchScale(target, Vector3.one * amount);
        }

        /// <summary>Pops scale per axis, for punches that should bulge one way more than another.</summary>
        public static void PunchScale(Transform target, Vector3 amount)
        {
            var punch = PunchFor(target);
            if (punch != null) punch.Scale.Kick(amount, Settings.PunchFrequencyHz, Settings.PunchDampingRatio);
        }

        /// <summary>
        /// Squashes <paramref name="target"/> along its local up axis by
        /// <paramref name="amount"/> (0.3 is 30% shorter), bulging the other two
        /// axes to keep its volume, then springs back. Landings and impacts.
        /// </summary>
        public static void Squash(Transform target, float amount)
        {
            PunchScale(target, new Vector3(amount * 0.5f, -amount, amount * 0.5f));
        }

        /// <summary>
        /// The opposite of <see cref="Squash"/>: taller and thinner, then springs
        /// back. Jumps, launches and anything snapping upwards.
        /// </summary>
        public static void Stretch(Transform target, float amount)
        {
            Squash(target, -amount);
        }

        /// <summary>
        /// Knocks <paramref name="target"/> <paramref name="distance"/> world units
        /// along <paramref name="direction"/> and springs it back, without moving
        /// where the game thinks it is. Shows which way a hit came from.
        /// </summary>
        public static void PunchPosition(Transform target, Vector3 direction, float distance)
        {
            if (direction == Vector3.zero) return;

            var punch = PunchFor(target);
            if (punch != null) punch.Position.Kick(direction.normalized * distance, Settings.PunchFrequencyHz, Settings.PunchDampingRatio);
        }

        /// <summary>
        /// Twists <paramref name="target"/> by up to <paramref name="localEulerDegrees"/>
        /// and wobbles it back. For 2D and UI, a z-only twist such as
        /// <c>new Vector3(0, 0, 15)</c> is the classic jiggle.
        /// </summary>
        public static void PunchRotation(Transform target, Vector3 localEulerDegrees)
        {
            var punch = PunchFor(target);
            if (punch != null) punch.Rotation.Kick(localEulerDegrees, Settings.PunchFrequencyHz, Settings.PunchDampingRatio);
        }

        /// <summary>Stops every punch on <paramref name="target"/> and puts it back to rest at once.</summary>
        public static void StopPunches(Transform target)
        {
            var runtime = JuiceRuntime.Existing;
            if (runtime != null) runtime.Punches.Stop(target);
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        static readonly List<Renderer> FlashableBuffer = new List<Renderer>();

        // Domain reload can be switched off in Enter Play Mode settings, in which
        // case statics survive between play sessions. Every static field above
        // must be restored to its declared initial value here.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            if (_sceneHookInstalled) SceneManager.sceneLoaded -= HandleSceneLoaded;

            _settings = null;
            _settingsResolved = false;
            _cameraOverride = null;
            _mainCamera = null;
            _sceneHookInstalled = false;
            _shakeIntensity = 1f;
            _hitstopEnabled = true;
            _timeScale = 1f;
        }

        internal static void ApplyTimeScale()
        {
            var runtime = JuiceRuntime.Existing;
            float effects = runtime != null ? runtime.TimeEffects.Multiplier(Settings) : 1f;
            Time.timeScale = _timeScale * effects;
        }

        static TransformPunch PunchFor(Transform target)
        {
            if (target == null) return null;

            var runtime = JuiceRuntime.Get();
            return runtime != null ? runtime.Punches.For(target) : null;
        }

        // Particles, trails and lines turning into solid white blobs reads as a
        // rendering bug, not a hit, so only the object's own body flashes.
        static bool IsFlashable(Renderer renderer)
        {
            return renderer is MeshRenderer || renderer is SkinnedMeshRenderer || renderer is SpriteRenderer;
        }

        static Camera MainCamera
        {
            get
            {
                // Camera.main is a tagged search, so it is cached. A scene load
                // destroys the old camera, which the null check catches, but it can
                // also bring in a new MainCamera while the old one survives, so the
                // cache is also dropped on every load.
                if (!_sceneHookInstalled)
                {
                    SceneManager.sceneLoaded += HandleSceneLoaded;
                    _sceneHookInstalled = true;
                }

                if (_mainCamera == null) _mainCamera = UnityEngine.Camera.main;
                return _mainCamera;
            }
        }

        static void HandleSceneLoaded(Scene scene, LoadSceneMode mode) => _mainCamera = null;

        static float DistanceFalloff(Vector3 position)
        {
            var camera = Camera;
            if (camera == null) return 1f;

            var toPosition = position - camera.transform.position;

            // A 2D camera sits some distance back from the plane the game happens
            // on, and that depth says nothing about how close an event feels.
            if (camera.orthographic) toPosition = Vector3.ProjectOnPlane(toPosition, camera.transform.forward);

            var settings = Settings;
            float distance = toPosition.magnitude;
            if (distance >= settings.SilentDistance) return 0f;

            return 1f - Mathf.InverseLerp(settings.FullStrengthDistance, settings.SilentDistance, distance);
        }

        static void ResolveSettings()
        {
            _settingsResolved = true;
            _settings = Resources.Load<JuiceSettings>(JuiceSettings.ResourcePath);
            if (_settings != null) return;

            // No asset is a supported setup, not an error: run on the defaults the
            // field initializers declare. DontSave keeps the editor from flagging it
            // as a leaked object when play mode ends.
            _settings = ScriptableObject.CreateInstance<JuiceSettings>();
            _settings.name = "JuiceSettings (Defaults)";
            _settings.hideFlags = HideFlags.DontSave;
        }
    }
}
