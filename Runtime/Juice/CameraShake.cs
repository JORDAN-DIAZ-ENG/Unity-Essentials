using UnityEngine;

namespace Essential.Juice
{
    /// <summary>
    /// The shake model, kept free of scene objects so the maths can be read on its
    /// own. Two independent parts are summed:
    ///
    /// - Trauma: a 0..1 value that hits add to and time drains. The visible shake
    ///   is trauma raised to a power and multiplied by smooth noise, so repeated
    ///   hits build up and then settle naturally.
    /// - Kick: a damped spring knocked in one direction, for recoil and for hits
    ///   that come from somewhere. Noise cannot say "from the left"; a kick can.
    /// </summary>
    internal sealed class CameraShake
    {
        // Rows of the 2D Perlin field, spaced far apart so every axis jitters
        // independently instead of all six moving in lockstep.
        const float NoiseRowX = 0f;
        const float NoiseRowY = 17.3f;
        const float NoiseRowZ = 41.9f;
        const float NoiseRowPitch = 63.1f;
        const float NoiseRowYaw = 88.7f;
        const float NoiseRowRoll = 112.4f;

        // Matches the spring's own cap, so trauma and kick advance by the same
        // amount after a hitch.
        const float MaxTickSeconds = 0.1f;

        float _trauma;
        float _noiseTime;
        DampedSpring _kick;

        public float Trauma => _trauma;

        public bool IsResting => _trauma <= 0f && _kick.IsResting;

        public void AddTrauma(float amount)
        {
            _trauma = Mathf.Clamp01(_trauma + amount);
        }

        /// <summary>
        /// Launches the kick so its first peak lands <paramref name="distance"/>
        /// world units along <paramref name="direction"/>.
        /// </summary>
        public void AddKick(Vector3 direction, float distance, JuiceSettings settings)
        {
            if (direction == Vector3.zero) return;

            _kick.Kick(direction.normalized * distance, settings.KickFrequencyHz, settings.KickDampingRatio);
        }

        public void Clear()
        {
            _trauma = 0f;
            _noiseTime = 0f;
            _kick.Clear();
        }

        public void Tick(float deltaSeconds, JuiceSettings settings)
        {
            TickTrauma(Mathf.Min(deltaSeconds, MaxTickSeconds), settings);
            _kick.Tick(deltaSeconds, settings.KickFrequencyHz, settings.KickDampingRatio);
        }

        /// <summary>
        /// The shake pose for this frame, to be layered on top of wherever the game
        /// put the camera. Orthographic cameras only move in their view plane and
        /// roll, since pitch and yaw read as skew rather than shake in 2D.
        /// </summary>
        public void Evaluate(Camera camera, JuiceSettings settings, float intensity,
                             out Vector3 worldOffset, out Quaternion localRotation)
        {
            float shake = Mathf.Pow(_trauma, settings.TraumaExponent) * intensity;
            float noiseTime = _noiseTime * settings.ShakeFrequencyHz;
            var cameraTransform = camera.transform;

            Vector3 localOffset;
            Vector3 kick = _kick.Offset * intensity;

            if (camera.orthographic)
            {
                float maxOffset = settings.OrthographicMaxOffsetFraction * camera.orthographicSize;
                localOffset = new Vector3(Noise(NoiseRowX, noiseTime), Noise(NoiseRowY, noiseTime), 0f) * (maxOffset * shake);
                localRotation = Quaternion.Euler(0f, 0f, Noise(NoiseRowRoll, noiseTime) * settings.OrthographicMaxRollDegrees * shake);
                kick = Vector3.ProjectOnPlane(kick, cameraTransform.forward);
            }
            else
            {
                var maxAngles = settings.PerspectiveMaxAngles;
                localOffset = new Vector3(Noise(NoiseRowX, noiseTime), Noise(NoiseRowY, noiseTime), Noise(NoiseRowZ, noiseTime))
                              * (settings.PerspectiveMaxOffset * shake);
                localRotation = Quaternion.Euler(Noise(NoiseRowPitch, noiseTime) * maxAngles.x * shake,
                                                 Noise(NoiseRowYaw, noiseTime) * maxAngles.y * shake,
                                                 Noise(NoiseRowRoll, noiseTime) * maxAngles.z * shake);
            }

            worldOffset = cameraTransform.rotation * localOffset + kick;
        }

        void TickTrauma(float deltaSeconds, JuiceSettings settings)
        {
            if (_trauma <= 0f)
            {
                // Restarting the noise clock while idle keeps it small, since Perlin
                // sampling loses precision as its input grows over a long session.
                _noiseTime = 0f;
                return;
            }

            _noiseTime += deltaSeconds;
            _trauma = Mathf.Max(0f, _trauma - settings.TraumaDecayPerSecond * deltaSeconds);
        }

        static float Noise(float row, float time) => Mathf.PerlinNoise(row, time) * 2f - 1f;
    }
}
