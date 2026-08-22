using System;

namespace Essential.Audio
{
    /// <summary>
    /// Returned by <see cref="AudioManager.PlayBgm"/> so callers can react when a
    /// track ends. Useful for stingers that hand off to the next track: play the
    /// "you won" jingle, then start the level theme when it finishes.
    ///
    /// The handle completes when the track finishes naturally, and also when it is
    /// stopped or replaced by another track, so a chained callback can never be
    /// left waiting forever.
    /// </summary>
    public sealed class BgmHandle
    {
        /// <summary>
        /// Raised once, on the main thread, when the track stops for any reason.
        /// Subscribing after completion invokes the callback immediately.
        /// </summary>
        public event Action Finished
        {
            add
            {
                if (IsDone) { value?.Invoke(); return; }
                _finished += value;
            }
            remove => _finished -= value;
        }

        Action _finished;

        /// <summary>The track this handle was created for.</summary>
        public string Key { get; }

        /// <summary>True once the track has stopped, been replaced, or been cancelled.</summary>
        public bool IsDone { get; private set; }

        /// <summary>True when the track ran to its natural end rather than being cut short.</summary>
        public bool CompletedNaturally { get; private set; }

        internal BgmHandle(string key) => Key = key;

        internal void Complete(bool naturally)
        {
            if (IsDone) return;

            IsDone = true;
            CompletedNaturally = naturally;

            var callback = _finished;
            _finished = null;
            callback?.Invoke();
        }
    }
}
