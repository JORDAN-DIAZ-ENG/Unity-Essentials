using UnityEngine;

namespace Essential.Juice
{
    /// <summary>
    /// Spring-driven offsets layered on one transform: a position knock, a rotation
    /// wobble and a scale pop or squash. The offsets are applied after all game
    /// code has run for the frame and removed before any runs the next, so the
    /// game only ever reads and writes the transform's real pose. An object can be
    /// punched while its own script moves it.
    /// </summary>
    internal sealed class TransformPunch
    {
        // Keeps a deep squash or an overshooting spring from flattening an axis to
        // zero or flipping it negative, which would turn the object inside out.
        const float MinScaleMultiplier = 0.05f;

        public DampedSpring Position;
        public DampedSpring Rotation;
        public DampedSpring Scale;

        bool _isApplied;
        Vector3 _savedLocalPosition;
        Quaternion _savedLocalRotation;
        Vector3 _savedLocalScale;

        public Transform Target { get; private set; }

        public bool IsResting => Position.IsResting && Rotation.IsResting && Scale.IsResting;

        public void Begin(Transform target)
        {
            Target = target;
            Position.Clear();
            Rotation.Clear();
            Scale.Clear();
            _isApplied = false;
        }

        public void Tick(float deltaSeconds, JuiceSettings settings)
        {
            float frequency = settings.PunchFrequencyHz;
            float damping = settings.PunchDampingRatio;

            Position.Tick(deltaSeconds, frequency, damping);
            Rotation.Tick(deltaSeconds, frequency, damping);
            Scale.Tick(deltaSeconds, frequency, damping);
        }

        public void Apply()
        {
            if (_isApplied || Target == null) return;

            _savedLocalPosition = Target.localPosition;
            _savedLocalRotation = Target.localRotation;
            _savedLocalScale = Target.localScale;
            _isApplied = true;

            var scale = Scale.Offset;
            var multiplier = new Vector3(Mathf.Max(MinScaleMultiplier, 1f + scale.x),
                                         Mathf.Max(MinScaleMultiplier, 1f + scale.y),
                                         Mathf.Max(MinScaleMultiplier, 1f + scale.z));

            Target.position += Position.Offset;
            Target.localRotation = _savedLocalRotation * Quaternion.Euler(Rotation.Offset);
            Target.localScale = Vector3.Scale(_savedLocalScale, multiplier);
        }

        public void Restore()
        {
            if (!_isApplied) return;

            _isApplied = false;
            if (Target == null) return;

            // Writing back the saved pose rather than undoing the offset means float
            // error can never accumulate into a slow drift.
            Target.localPosition = _savedLocalPosition;
            Target.localRotation = _savedLocalRotation;
            Target.localScale = _savedLocalScale;
        }
    }
}
