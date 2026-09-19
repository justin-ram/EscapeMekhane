#if VISTA
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Stores the user saved <see cref="TerrainGridConfig"/>s, persisted globally in EditorPrefs as JSON (the
    /// same pattern as RecentBiomes and NewsService). The built-in configs live in code on the Create Terrain
    /// page; this holds only the custom ones the user saves there. Global, not per project, since preferred
    /// grid sizes tend to be personal across projects. Raises <see cref="changed"/> after the list changes so
    /// an open view refreshes.
    /// </summary>
    public static class TerrainGridConfigs
    {
        // JsonUtility cannot serialize a top level list, so the list is wrapped in this holder.
        [Serializable]
        private class Store
        {
            public List<TerrainGridConfig> configs = new List<TerrainGridConfig>();
        }

        private const string KEY = "Pinwheel.Vista.Wizard.TerrainGridConfigs";
        private const int MAX = 16;

        /// <summary>Raised after the saved list changes, so an open view can refresh.</summary>
        public static event Action changed;

        /// <summary>The saved configs, newest first. Empty when none are saved.</summary>
        public static List<TerrainGridConfig> Get()
        {
            string json = EditorPrefs.GetString(KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new List<TerrainGridConfig>();
            try
            {
                Store store = JsonUtility.FromJson<Store>(json);
                return store != null && store.configs != null ? store.configs : new List<TerrainGridConfig>();
            }
            catch
            {
                return new List<TerrainGridConfig>();
            }
        }

        /// <summary>Save a config at the front. An equal config (same dimensions) already saved is moved to
        /// the front rather than duplicated, and the list is trimmed to MAX.</summary>
        public static void Add(TerrainGridConfig config)
        {
            if (config == null)
                return;
            List<TerrainGridConfig> configs = Get();
            configs.RemoveAll(c => c.Equals(config));
            configs.Insert(0, config);
            if (configs.Count > MAX)
                configs.RemoveRange(MAX, configs.Count - MAX);
            Save(configs);
        }

        /// <summary>Remove the saved config equal to the given one (by dimensions), if present.</summary>
        public static void Remove(TerrainGridConfig config)
        {
            if (config == null)
                return;
            List<TerrainGridConfig> configs = Get();
            if (configs.RemoveAll(c => c.Equals(config)) > 0)
                Save(configs);
        }

        private static void Save(List<TerrainGridConfig> configs)
        {
            EditorPrefs.SetString(KEY, JsonUtility.ToJson(new Store { configs = configs }));
            changed?.Invoke();
        }
    }
}
#endif
