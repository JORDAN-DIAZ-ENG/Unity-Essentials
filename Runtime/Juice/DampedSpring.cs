using UnityEngine;

namespace Essential.Juice
{
    /// <summary>
    /// A three-axis damped spring pulled back towards zero. Every "knock it and
    /// let it settle" effect is one of these: the camera kick, and position,
    /// rotation and scale punches on transforms.
    /// </summary>
    internal struct DampedSpring
    {
        // Integrated with semi-implicit Euler, which goes unstable once the step is
        // large relative to the spring's period. Substepping at a fixed size keeps
        // it stable at any frame rate.
        const float MaxStepSeconds = 1f / 120f;

        // A frame longer than this (a scene load, a breakpoint) is treated as this
        // long, so a spring does not lurch from one huge integration step.
        const float MaxTickSeconds = 0.1f;

        const float RestingThreshold = 0.0001f;

        public Vector3 Offset;
        public Vector3 Velocity;

        public bool IsResting =>
            Offset.sqrMagnitude < RestingThreshold * RestingThreshold &&
            Velocity.sqrMagnitude < RestingThreshold * RestingThreshold;

        /// <summary>
        /// Launches the spring so its first peak lands at <paramref name="peak"/>.
        /// Kicks stack: a second kick adds to whatever the spring is already doing.
        /// </summary>
        public void Kick(Vector3 peak, float frequencyHz, float dampingRatio)
        {
            float angularFrequency = 2f * Mathf.PI * frequencyHz;
            Velocity += peak * (angularFrequency / FirstPeakFraction(dampingRatio));
        }

        public void Tick(float deltaSeconds, float frequencyHz, float dampingRatio)
        {
            if (IsResting)
            {
                Clear();
                return;
            }

            float angularFrequency = 2f * Mathf.PI * frequencyHz;
            float stiffness = angularFrequency * angularFrequency;
            float damping = 2f * dampingRatio * angularFrequency;

            float remaining = Mathf.Min(deltaSeconds, MaxTickSeconds);
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, MaxStepSeconds);
                var acceleration = -stiffness * Offset - damping * Velocity;
                Velocity += acceleration * step;
                Offset += Velocity * step;
                remaining -= step;
            }
        }

        public void Clear()
        {
            Offset = Vector3.zero;
            Velocity = Vector3.zero;
        }

        // An underdamped spring launched from rest at speed v first peaks at
        // (v / angularFrequency) times this factor. Dividing it back out lets callers
        // ask for a peak instead of guessing at a launch speed.
        static float FirstPeakFraction(float dampingRatio)
        {
            float dampedRatio = Mathf.Sqrt(1f - dampingRatio * dampingRatio);
            return Mathf.Exp(-dampingRatio / dampedRatio * Mathf.Atan(dampedRatio / dampingRatio));
        }
    }
}
