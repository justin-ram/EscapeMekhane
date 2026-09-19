#if VISTA
using System;
using System.Collections.Generic;
using Pinwheel.Vista;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor
{
    /// <summary>
    /// Adapts small, general Vista runtime utility signals to Unity Editor services.
    /// </summary>
    [InitializeOnLoad]
    public static class VistaGeneralEditorService
    {
        private static readonly Dictionary<Guid, int> s_progressIds = new Dictionary<Guid, int>();

        static VistaGeneralEditorService()
        {
            ProgressNotifications.began += OnProgressBegan;
            ProgressNotifications.reported += OnProgressReported;
            ProgressNotifications.ended += OnProgressEnded;
            AssemblyReloadEvents.beforeAssemblyReload += FinishAllProgress;
            EditorApplication.quitting += FinishAllProgress;
        }

        private static void OnProgressBegan(Guid progressId, string name)
        {
            try
            {
                int editorProgressId = Progress.Start(name);
                s_progressIds.Add(progressId, editorProgressId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void OnProgressReported(Guid progressId, float progress, string description)
        {
            if (!s_progressIds.TryGetValue(progressId, out int editorProgressId))
                return;

            try
            {
                Progress.Report(editorProgressId, progress, description);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void OnProgressEnded(Guid progressId)
        {
            if (!s_progressIds.TryGetValue(progressId, out int editorProgressId))
                return;

            try
            {
                Progress.Finish(editorProgressId);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                s_progressIds.Remove(progressId);
            }
        }

        private static void FinishAllProgress()
        {
            foreach (int editorProgressId in s_progressIds.Values)
            {
                try
                {
                    Progress.Finish(editorProgressId);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
            s_progressIds.Clear();
        }
    }
}
#endif
