#if VISTA
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Publishes progress for long-running Vista operations without depending on an editor UI.
    /// </summary>
    public static class ProgressNotifications
    {
        public delegate void BeginHandler(Guid progressId, string name);
        public delegate void ReportHandler(Guid progressId, float progress, string description);
        public delegate void EndHandler(Guid progressId);

        public static event BeginHandler began;
        public static event ReportHandler reported;
        public static event EndHandler ended;

        private static readonly HashSet<Guid> s_operations = new HashSet<Guid>();

        public static Guid Begin(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A progress operation name is required.", nameof(name));

            Guid progressId = Guid.NewGuid();
            s_operations.Add(progressId);
            InvokeSafely(began, callback => callback(progressId, name));
            return progressId;
        }

        public static void Report(Guid progressId, float progress, string description = null)
        {
            if (!s_operations.Contains(progressId))
                return;

            float normalizedProgress = Mathf.Clamp01(progress);
            InvokeSafely(reported, callback => callback(progressId, normalizedProgress, description));
        }

        public static void Report(Guid progressId, int current, int total, string description = null)
        {
            float normalizedProgress = total > 0 ? (float)current / total : 0;
            Report(progressId, normalizedProgress, description);
        }

        public static void End(Guid progressId)
        {
            if (!s_operations.Remove(progressId))
                return;

            InvokeSafely(ended, callback => callback(progressId));
        }

        private static void InvokeSafely<T>(T handlers, Action<T> invoke) where T : Delegate
        {
            if (handlers == null)
                return;

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    invoke((T)handler);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
#endif
