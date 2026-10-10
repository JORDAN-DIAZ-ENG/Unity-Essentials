using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.Rendering;

namespace Essential.Juice
{
    /// <summary>
    /// The scene-side half of the juice system: ticks every effect on real time,
    /// lays the shake onto the camera while it renders, and lays punches onto
    /// transforms between the game's last script and the next frame's first one.
    ///
    /// In both cases the offset exists only while nothing in the game can see
    /// it. Follow scripts, Cinemachine, raycasts and movement code all read and
    /// write the real pose, so juice never fights the game for a transform.
    ///
    /// It builds itself on first use. Game code talks to <see cref="Juice"/> and
    /// should not need to touch this type.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class JuiceRuntime : MonoBehaviour
    {
        const string GameObjectName = "[Essential.Juice]";

        static JuiceRuntime _instance;
        static bool _quitting;

        /// <summary>
        /// Raised once per runtime, right after it is built, so optional modules
        /// such as the URP screen effects can attach themselves to it without the
        /// core assembly knowing they exist.
        /// </summary>
        internal static Action<JuiceRuntime> Created;

        readonly CameraShake _shake = new CameraShake();
        readonly TimeEffects _time = new TimeEffects();
        readonly TransformPunches _punches = new TransformPunches();
        readonly HitFlashes _flashes = new HitFlashes();

        // The camera currently carrying a shake offset, and the pose to put back.
        Transform _displacedCamera;
        Vector3 _restoreCameraPosition;
        Quaternion _restoreCameraRotation;

        internal CameraShake Shake => _shake;
        internal TimeEffects TimeEffects => _time;
        internal TransformPunches Punches => _punches;
        internal HitFlashes Flashes => _flashes;

        // ------------------------------------------------------------------
        // Lifetime
        // ------------------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            // Domain reload can be switched off in Enter Play Mode settings, in
            // which case statics survive between sessions and would point at a
            // destroyed object. Clear them at the start of every play session.
            // Created is deliberately left alone: optional modules subscribe to it
            // in a later initialization phase, so their subscriptions are always fresh.
            _instance = null;
            _quitting = false;
            UninstallPunchHooks();
        }

        /// <summary>
        /// The runtime if it already exists, without building one. Queries use this
        /// so that merely asking about trauma does not spawn the juice object.
        /// </summary>
        internal static JuiceRuntime Existing => _instance;

        /// <summary>
        /// Returns the live runtime, creating it on first use. Returns null while the
        /// application is shutting down, so an OnDestroy handler that shakes the
        /// camera cannot resurrect it.
        /// </summary>
        internal static JuiceRuntime Get()
        {
            if (_instance != null) return _instance;
            if (_quitting || !Application.isPlaying) return null;

            var host = new GameObject(GameObjectName);
            DontDestroyOnLoad(host);
            _instance = host.AddComponent<JuiceRuntime>();
            Created?.Invoke(_instance);
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
        }

        void OnEnable()
        {
            // Scriptable render pipelines (URP, HDRP) raise the RenderPipelineManager
            // events; the built-in pipeline raises the Camera ones. Each pipeline only
            // raises its own pair, so subscribing to both never double-applies.
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
            Camera.onPreCull += HandlePreCull;
            Camera.onPostRender += HandlePostRender;

            InstallPunchHooks();
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            Camera.onPreCull -= HandlePreCull;
            Camera.onPostRender -= HandlePostRender;

            UninstallPunchHooks();

            RestoreCamera();
            _punches.StopAll();
            _flashes.StopAll();

            // Hand time back to the game. A runtime destroyed mid-hitstop would
            // otherwise leave the game frozen with nothing left to unfreeze it.
            _time.Clear();
            Juice.ApplyTimeScale();
        }

        void OnApplicationQuit() => _quitting = true;

        void OnDestroy()
        {
            _flashes.DestroyMaterials();
            if (_instance == this) _instance = null;
        }

        // ------------------------------------------------------------------
        // Ticking
        // ------------------------------------------------------------------

        void Update()
        {
            // Normally a no-op. If a render was interrupted before its end event,
            // the offset would otherwise be baked into the camera permanently.
            RestoreCamera();

            // Real time throughout: hitstop and slow motion have to keep counting
            // while game time is bent, and a frozen frame that still shakes, flashes
            // and squashes is what sells an impact.
            float deltaSeconds = Time.unscaledDeltaTime;
            var settings = Juice.Settings;

            _shake.Tick(deltaSeconds, settings);
            _punches.Tick(deltaSeconds, settings);
            _flashes.Tick(deltaSeconds);

            if (_time.Tick(deltaSeconds)) Juice.ApplyTimeScale();
        }

        // ------------------------------------------------------------------
        // Punch application
        // ------------------------------------------------------------------

        // Punches go on at the start of PostLateUpdate, after every Update and
        // LateUpdate but before skinning and rendering, and come off at the start of
        // EarlyUpdate, before physics and scripts. No game code ever runs while a
        // punch is applied.
        struct ApplyPunchesHook { }
        struct RestorePunchesHook { }

        static void ApplyPunches()
        {
            if (_instance != null) _instance._punches.ApplyAll();
        }

        static void RestorePunches()
        {
            if (_instance != null) _instance._punches.RestoreAll();
        }

        static void InstallPunchHooks()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (ContainsHook(loop, typeof(ApplyPunchesHook))) return;

            InsertFirst(ref loop, typeof(UnityEngine.PlayerLoop.PostLateUpdate),
                        new PlayerLoopSystem { type = typeof(ApplyPunchesHook), updateDelegate = ApplyPunches });
            InsertFirst(ref loop, typeof(UnityEngine.PlayerLoop.EarlyUpdate),
                        new PlayerLoopSystem { type = typeof(RestorePunchesHook), updateDelegate = RestorePunches });
            PlayerLoop.SetPlayerLoop(loop);
        }

        static void UninstallPunchHooks()
        {
            var loop = PlayerLoop.GetCurrentPlayerLoop();
            if (!ContainsHook(loop, typeof(ApplyPunchesHook))) return;

            RemoveHook(ref loop, typeof(ApplyPunchesHook));
            RemoveHook(ref loop, typeof(RestorePunchesHook));
            PlayerLoop.SetPlayerLoop(loop);
        }

        static bool ContainsHook(PlayerLoopSystem loop, Type hook)
        {
            if (loop.type == hook) return true;
            if (loop.subSystemList == null) return false;

            foreach (var child in loop.subSystemList)
            {
                if (ContainsHook(child, hook)) return true;
            }

            return false;
        }

        static void InsertFirst(ref PlayerLoopSystem loop, Type phase, PlayerLoopSystem system)
        {
            var phases = loop.subSystemList;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != phase) continue;

                var children = new List<PlayerLoopSystem> { system };
                if (phases[i].subSystemList != null) children.AddRange(phases[i].subSystemList);
                phases[i].subSystemList = children.ToArray();
                return;
            }
        }

        static void RemoveHook(ref PlayerLoopSystem loop, Type hook)
        {
            var phases = loop.subSystemList;
            for (int i = 0; i < phases.Length; i++)
            {
                var children = phases[i].subSystemList;
                if (children == null) continue;

                int index = Array.FindIndex(children, child => child.type == hook);
                if (index < 0) continue;

                var remaining = new List<PlayerLoopSystem>(children);
                remaining.RemoveAt(index);
                phases[i].subSystemList = remaining.ToArray();
            }
        }

        // ------------------------------------------------------------------
        // Camera displacement
        // ------------------------------------------------------------------

        void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera) => DisplaceCamera(camera);
        void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera) => RestoreCamera();
        void HandlePreCull(Camera camera) => DisplaceCamera(camera);
        void HandlePostRender(Camera camera) => RestoreCamera();

        void DisplaceCamera(Camera camera)
        {
            if (_shake.IsResting) return;

            // Scene view, UI and overlay cameras render through the same events.
            if (camera != Juice.Camera) return;

            RestoreCamera();

            _shake.Evaluate(camera, Juice.Settings, Juice.ShakeIntensity,
                            out Vector3 worldOffset, out Quaternion localRotation);

            var cameraTransform = camera.transform;
            _displacedCamera = cameraTransform;
            _restoreCameraPosition = cameraTransform.localPosition;
            _restoreCameraRotation = cameraTransform.localRotation;

            cameraTransform.position += worldOffset;
            cameraTransform.localRotation *= localRotation;
        }

        void RestoreCamera()
        {
            if (_displacedCamera == null) return;

            // Writing back the saved pose rather than subtracting the offset means
            // float error can never accumulate into a slow drift.
            _displacedCamera.localPosition = _restoreCameraPosition;
            _displacedCamera.localRotation = _restoreCameraRotation;
            _displacedCamera = null;
        }
    }
}
