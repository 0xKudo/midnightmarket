using System;
using System.Collections.Generic;
using UnityEngine;

namespace ArmsFair.Network
{
    /// <summary>
    /// Queues actions from background threads and executes them on the Unity main thread each frame.
    /// Attach to the same persistent GameObject as GameClient.
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static readonly Queue<Action> _queue = new Queue<Action>();
        private static readonly object _lock = new object();

        public static void Enqueue(Action action)
        {
            lock (_lock) _queue.Enqueue(action);
        }

        private void Update()
        {
            while (true)
            {
                Action action;
                lock (_lock)
                {
                    if (_queue.Count == 0) break;
                    action = _queue.Dequeue();
                }
                try   { action(); }
                catch (Exception ex) { Debug.LogError($"[Dispatcher] {ex}"); }
            }
        }
    }
}
