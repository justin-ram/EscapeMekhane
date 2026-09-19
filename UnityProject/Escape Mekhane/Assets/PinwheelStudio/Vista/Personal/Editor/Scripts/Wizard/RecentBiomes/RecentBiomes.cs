#if VISTA
using System;
using System.Collections.Generic;
using Pinwheel.Vista;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Most recently used biomes, persisted across sessions so the Home dashboard can list them. A biome
    /// lives in a scene, so each entry stores the scene path plus a GlobalObjectId that resolves the
    /// biome when its scene is loaded. Entries store references only, the biome (GlobalObjectId) and its
    /// graph (asset GUID), never captured names, so the row title is resolved live and stays correct after
    /// a rename. Entries whose scene is not loaded are still listed, with an option to open the scene.
    /// </summary>
    public static class RecentBiomes
    {
        [Serializable]
        public class Entry
        {
            public string scenePath;
            public string globalId;
            public string graphGuid;
        }

        [Serializable]
        private class Store
        {
            public List<Entry> entries = new List<Entry>();
        }

        private const string KEY = "Pinwheel.Vista.Wizard.RecentBiomes";
        private const int MAX_ENTRY = 8;

        /// <summary>Raised after the recent list actually changes, so views can refresh live.</summary>
        public static event Action changed;

        public static void Record(IBiome biome)
        {
            UnityEngine.Object obj = biome as UnityEngine.Object;
            if (biome == null || obj == null)
                return;

            GlobalObjectId gid = GlobalObjectId.GetGlobalObjectIdSlow(obj);
            Entry entry = new Entry
            {
                scenePath = biome.gameObject.scene.path,
                globalId = gid.ToString(),
                graphGuid = GetGraphGuid(biome),
            };

            Store store = Load();
            if (store.entries.Count > 0 && store.entries[0].globalId == entry.globalId)
            {
                // Already the most recent biome. Only rewrite when its graph reference moved on, for
                // example the user assigned or swapped the graph after creating a blank biome. Otherwise
                // skip rewriting EditorPrefs on every repeated graph edit.
                if (store.entries[0].graphGuid == entry.graphGuid && store.entries[0].scenePath == entry.scenePath)
                    return;
                store.entries[0] = entry;
                Save(store);
                changed?.Invoke();
                return;
            }
            store.entries.RemoveAll(e => e.globalId == entry.globalId);
            store.entries.Insert(0, entry);
            if (store.entries.Count > MAX_ENTRY)
            {
                store.entries.RemoveRange(MAX_ENTRY, store.entries.Count - MAX_ENTRY);
            }
            Save(store);
            changed?.Invoke();
        }

        public static List<Entry> Get()
        {
            return Load().entries;
        }

        /// <summary>
        /// Drops entries that can no longer be shown or acted on: an empty scene path, a scene file that
        /// no longer exists on disk, or a loaded scene whose biome object is gone. Entries whose scene is
        /// merely not loaded are kept, they still offer Load Scene. Persists silently (no <see cref="changed"/>
        /// event, so a caller refreshing on that event does not loop) and returns the surviving entries.
        /// </summary>
        public static List<Entry> CleanUpAndGet()
        {
            Store store = Load();
            int removed = store.entries.RemoveAll(e => !IsValid(e));
            if (removed > 0)
            {
                Save(store);
            }
            return store.entries;
        }

        public static bool IsSceneLoaded(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.scenePath))
                return false;
            Scene scene = SceneManager.GetSceneByPath(entry.scenePath);
            return scene.IsValid() && scene.isLoaded;
        }

        public static IBiome Resolve(Entry entry)
        {
            if (entry == null || !GlobalObjectId.TryParse(entry.globalId, out GlobalObjectId gid))
                return null;
            UnityEngine.Object obj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid);
            return obj == null ? null : obj as IBiome;
        }

        /// <summary>
        /// Live graph name for an entry, resolved from the stored graph asset GUID, so it stays correct
        /// after the graph is renamed and is available even when the biome's scene is not loaded. Empty
        /// when the entry has no graph or the graph asset no longer exists.
        /// </summary>
        public static string ResolveGraphName(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.graphGuid))
                return string.Empty;
            string path = AssetDatabase.GUIDToAssetPath(entry.graphGuid);
            return string.IsNullOrEmpty(path) ? string.Empty : System.IO.Path.GetFileNameWithoutExtension(path);
        }

        private static bool IsValid(Entry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.scenePath))
                return false;
            Scene scene = SceneManager.GetSceneByPath(entry.scenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                // Loaded, so we can tell for sure: a biome that no longer resolves has been deleted.
                return Resolve(entry) != null;
            }
            // Not loaded, keep only while the scene asset still exists on disk so Load Scene can work.
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.scenePath) != null;
        }

        private static string GetGraphGuid(IBiome biome)
        {
            if (biome is IProceduralBiome procedural && procedural.terrainGraph != null)
            {
                string path = AssetDatabase.GetAssetPath(procedural.terrainGraph);
                if (!string.IsNullOrEmpty(path))
                    return AssetDatabase.AssetPathToGUID(path);
            }
            return string.Empty;
        }

        private static Store Load()
        {
            string json = EditorPrefs.GetString(KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new Store();
            try
            {
                return JsonUtility.FromJson<Store>(json) ?? new Store();
            }
            catch
            {
                return new Store();
            }
        }

        private static void Save(Store store)
        {
            EditorPrefs.SetString(KEY, JsonUtility.ToJson(store));
        }
    }
}
#endif
