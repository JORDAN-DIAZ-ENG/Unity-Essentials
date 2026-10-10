using UnityEngine;

namespace Essential.Juice
{
    /// <summary>
    /// Hitstop and slow motion, as one multiplier applied on top of the game's own
    /// <see cref="Juice.TimeScale"/>. Both run on real time, since game time is
    /// exactly what they are bending.
    /// </summary>
    internal sealed class TimeEffects
    {
        float _hitstopRemainingSeconds;

        bool _isSlowMotionActive;
        float _slowMotionScale = 1f;
        float _slowMotionHoldRemainingSeconds;
        float _recoverFromScale = 1f;
        float _recoverDurationSeconds;
        float _recoverElapsedSeconds;

        public bool IsHitstopActive => _hitstopRemainingSeconds > 0f;
        public bool IsSlowMotionActive => _isSlowMotionActive;

        /// <summary>What these effects multiply the game's time scale by right now.</summary>
        public float Multiplier(JuiceSettings settings)
        {
            float hitstop = IsHitstopActive ? settings.HitstopTimeScale : 1f;
            return SlowMotionMultiplier() * hitstop;
        }

        /// <summary>
        /// Starts or extends a hitstop. Overlapping hitstops do not add up: the
        /// freeze lasts until the longest one requested has run out, so a burst of
        /// hits in one frame freezes once rather than for their combined length.
        /// </summary>
        public void StartHitstop(float seconds)
        {
            _hitstopRemainingSeconds = Mathf.Max(_hitstopRemainingSeconds, seconds);
        }

        public void EndHitstop()
        {
            _hitstopRemainingSeconds = 0f;
        }

        /// <summary>
        /// Drops to <paramref name="timeScale"/> at once, holds it, then eases back
        /// to full speed. A new slow motion replaces one already running rather
        /// than stacking, so the newest moment always gets its full hold.
        /// </summary>
        public void StartSlowMotion(float timeScale, float holdSeconds, float recoverSeconds)
        {
            _isSlowMotionActive = true;
            _slowMotionScale = timeScale;
            _slowMotionHoldRemainingSeconds = holdSeconds;
            _recoverFromScale = timeScale;
            _recoverDurationSeconds = recoverSeconds;
            _recoverElapsedSeconds = 0f;
        }

        /// <summary>Skips the rest of the hold and eases back from wherever the scale is now.</summary>
        public void EndSlowMotion(float recoverSeconds)
        {
            if (!_isSlowMotionActive) return;

            _recoverFromScale = SlowMotionMultiplier();
            _slowMotionHoldRemainingSeconds = 0f;
            _recoverDurationSeconds = recoverSeconds;
            _recoverElapsedSeconds = 0f;

            if (recoverSeconds <= 0f) _isSlowMotionActive = false;
        }

        public void Clear()
        {
            _hitstopRemainingSeconds = 0f;
            _isSlowMotionActive = false;
        }

        /// <summary>Advances both clocks. Returns true if the multiplier may have changed.</summary>
        public bool Tick(float deltaSeconds)
        {
            bool wasHitstopActive = IsHitstopActive;
            if (wasHitstopActive) _hitstopRemainingSeconds = Mathf.Max(0f, _hitstopRemainingSeconds - deltaSeconds);

            bool hitstopChanged = wasHitstopActive != IsHitstopActive;
            return TickSlowMotion(deltaSeconds) || hitstopChanged;
        }

        bool TickSlowMotion(float deltaSeconds)
        {
            if (!_isSlowMotionActive) return false;

            if (_slowMotionHoldRemainingSeconds > 0f)
            {
                _slowMotionHoldRemainingSeconds -= deltaSeconds;
                return false;
            }

            _recoverElapsedSeconds += deltaSeconds;
            if (_recoverElapsedSeconds >= _recoverDurationSeconds) _isSlowMotionActive = false;

            return true;
        }

        float SlowMotionMultiplier()
        {
            if (!_isSlowMotionActive) return 1f;
            if (_slowMotionHoldRemainingSeconds > 0f) return _slowMotionScale;
            if (_recoverDurationSeconds <= 0f) return 1f;

            float progress = Mathf.Clamp01(_recoverElapsedSeconds / _recoverDurationSeconds);
            return Mathf.Lerp(_recoverFromScale, 1f, Mathf.SmoothStep(0f, 1f, progress));
        }
    }
}
