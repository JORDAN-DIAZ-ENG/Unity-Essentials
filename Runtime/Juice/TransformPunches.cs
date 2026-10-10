using System.Collections.Generic;
using UnityEngine;

namespace Essential.Juice
{
    /// <summary>
    /// Every transform currently being punched. Entries are pooled and dropped as
    /// soon as their springs settle, so only objects actually wobbling cost anything.
    /// </summary>
    internal sealed class TransformPunches
    {
        readonly List<TransformPunch> _active = new List<TransformPunch>();
        readonly Stack<TransformPunch> _pool = new Stack<TransformPunch>();

        public TransformPunch For(Transform target)
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Target == target) return _active[i];
            }

            var punch = _pool.Count > 0 ? _pool.Pop() : new TransformPunch();
            punch.Begin(target);
            _active.Add(punch);
            return punch;
        }

        public void Tick(float deltaSeconds, JuiceSettings settings)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var punch = _active[i];
                if (punch.Target != null) punch.Tick(deltaSeconds, settings);

                if (punch.Target == null || punch.IsResting) Release(i);
            }
        }

        public void ApplyAll()
        {
            for (int i = 0; i < _active.Count; i++) _active[i].Apply();
        }

        public void RestoreAll()
        {
            for (int i = 0; i < _active.Count; i++) _active[i].Restore();
        }

        public void Stop(Transform target)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (_active[i].Target == target) Release(i);
            }
        }

        public void StopAll()
        {
            for (int i = _active.Count - 1; i >= 0; i--) Release(i);
        }

        void Release(int index)
        {
            var punch = _active[index];

            // A stop can arrive between apply and restore, for example from OnGUI,
            // so the offset is taken off before the entry is forgotten.
            punch.Restore();

            _active.RemoveAt(index);
            _pool.Push(punch);
        }
    }
}
