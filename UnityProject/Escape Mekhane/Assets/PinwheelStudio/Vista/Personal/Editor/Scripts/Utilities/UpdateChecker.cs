#if VISTA
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Graphics;
using UnityEngine.Networking;
using UnityEditor;
using System;

namespace Pinwheel.VistaEditor
{
    public class UpdateChecker
    {
        private const string ENDPOINT = "https://api.pinwheelstud.io/vista/version-info";

        [System.Serializable]
        private class VersionResponse
        {
            public int major;
            public int minor;
            public int patch;
        }

        private static readonly string PREF_PREFIX = "vista-check-update-";

        internal static bool CheckedToday()
        {
            string dateString = DateTime.Now.ToString("yyyy-MM-dd");
            return EditorPrefs.HasKey(PREF_PREFIX + dateString);
        }

        internal static void CheckForUpdate()
        {
            ProjectInitializer.RunWhenCompleted(() =>
            {
                if (CheckedToday())
                    return;

                string dateString = DateTime.Now.ToString("yyyy-MM-dd");
                EditorPrefs.SetBool(PREF_PREFIX + dateString, true);
                CoroutineUtility.StartCoroutine(ICheckForUpdate());
            });
        }

        private static IEnumerator ICheckForUpdate()
        {
            string url = ENDPOINT
                + "?version=" + UnityWebRequest.EscapeURL(VersionInfo.versionLabel)
                + "&edition=" + UnityWebRequest.EscapeURL(GetEdition());
            UnityWebRequest r = UnityWebRequest.Get(url);
            r.timeout = 10;
            yield return r.SendWebRequest();
            if (r.result == UnityWebRequest.Result.Success)
            {
                VersionResponse response = new VersionResponse();
                EditorJsonUtility.FromJsonOverwrite(r.downloadHandler.text, response);

                if (IsNewerVersion(response))
                {
                    Debug.Log($"VISTA: New version {response.major / 1000}.{response.minor}.{response.patch} is available, please update using the Package Manager.");
                }
            }
        }

        private static bool IsNewerVersion(VersionResponse response)
        {
            if (response.major != VersionInfo.major)
                return response.major > VersionInfo.major;
            if (response.minor != VersionInfo.minor)
                return response.minor > VersionInfo.minor;
            return response.patch > VersionInfo.patch;
        }

        private static string GetEdition()
        {
            if (ProjectInitializer.isVistaProInstalled)
                return "pro";
            if (ProjectInitializer.isVistaIndieInstalled)
                return "indie";
            return "personal";
        }

    }
}
#endif
