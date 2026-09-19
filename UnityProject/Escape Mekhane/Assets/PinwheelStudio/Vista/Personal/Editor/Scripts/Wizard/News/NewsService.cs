#if VISTA
using System;
using System.Collections;
using System.Collections.Generic;
using Pinwheel.Vista;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Fetches the remote news feed for the Home dashboard "News" section. The feed is fetched at most
    /// once per day and the last payload is cached in EditorPrefs, so Home renders instantly and works
    /// offline; a successful fetch that changes the payload raises <see cref="changed"/> so an open view
    /// refreshes. Any failure leaves the cache untouched, the section simply shows the last items (or none).
    /// </summary>
    public static class NewsService
    {
        [Serializable]
        public class Item
        {
            public string id;
            public string title;
            public string summary;
            public string url;
            public string date;
            public string tag;
        }

        [Serializable]
        private class ItemList
        {
            public List<Item> items = new List<Item>();
        }

        private const string ENDPOINT = "https://api.pinwheelstud.io/vista/news";
        private const string CACHE_KEY = "Pinwheel.Vista.Wizard.News.Cache";
        private const string DATE_KEY = "Pinwheel.Vista.Wizard.News.LastFetch";
        private const int LIMIT = 10;

        /// <summary>Raised after a fetch stores a new payload, so an open view can refresh.</summary>
        public static event Action changed;

        /// <summary>The cached items, parsed from the stored payload. Empty when nothing is cached yet.</summary>
        public static List<Item> Get()
        {
            return Parse(EditorPrefs.GetString(CACHE_KEY, string.Empty));
        }

        /// <summary>Kick a fetch at most once per day. Cheap to call on every Home open.</summary>
        public static void EnsureFetched()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (EditorPrefs.GetString(DATE_KEY, string.Empty) == today)
                return;
            // Stamp up front so repeated opens in one day do not refetch, even if the request fails.
            EditorPrefs.SetString(DATE_KEY, today);
            CoroutineUtility.StartCoroutine(IFetch());
        }

        private static IEnumerator IFetch()
        {
            string url = ENDPOINT
                + "?product=vista"
                + "&edition=" + UnityWebRequest.EscapeURL(Edition())
                + "&version=" + UnityWebRequest.EscapeURL(Version())
                + "&limit=" + LIMIT;

            UnityWebRequest request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
                yield break;

            string raw = request.downloadHandler.text;
            raw = raw == null ? string.Empty : raw.Trim();
            // Only accept a JSON array, so an error page or junk never overwrites a good cache.
            if (!raw.StartsWith("["))
                yield break;
            if (raw == EditorPrefs.GetString(CACHE_KEY, string.Empty))
                yield break;

            EditorPrefs.SetString(CACHE_KEY, raw);
            changed?.Invoke();
        }

        private static List<Item> Parse(string rawArrayJson)
        {
            if (string.IsNullOrEmpty(rawArrayJson))
                return new List<Item>();
            try
            {
                // JsonUtility cannot read a top level array, so wrap it in an object first.
                ItemList list = JsonUtility.FromJson<ItemList>("{\"items\":" + rawArrayJson + "}");
                return list != null && list.items != null ? list.items : new List<Item>();
            }
            catch
            {
                return new List<Item>();
            }
        }

        private static string Edition()
        {
            return EditorCommon.IsProEdition() ? "pro"
                : EditorCommon.IsIndieEdition() ? "indie"
                : "personal";
        }

        private static string Version()
        {
            // major is stored times 1000 (3000 -> 3), so a clean "3.2.0" for the server's version compare.
            return (VersionInfo.major / 1000) + "." + VersionInfo.minor + "." + VersionInfo.patch;
        }
    }
}
#endif
