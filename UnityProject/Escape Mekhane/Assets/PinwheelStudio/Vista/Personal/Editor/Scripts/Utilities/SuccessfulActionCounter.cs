#if VISTA
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor
{
    /// <summary>
    /// Stores successful Vista editor actions as local, per-user engagement state.
    /// Callers are responsible for reporting only completed operations and for
    /// choosing a stable action id.
    /// </summary>
    public static class SuccessfulActionCounter
    {
        private const int CURRENT_SCHEMA_VERSION = 1;
        private const string PREF_KEY = "Pinwheel.Vista.SuccessfulActions.v1";

        /// <summary>
        /// Stable action ids shared by recorders and counter consumers.
        /// </summary>
        public static class ActionKeys
        {
            public const string VISTA_MANAGER_CREATED = "vista-manager.create";
            public const string TERRAIN_GRID_CREATED = "terrain-grid.create";
            public const string BIOME_CREATED_FROM_TEMPLATE = "biome.create-from-template";
            public const string TERRAIN_SEAL_CREATED = "terrain-seal.create";
            public const string GENERATE_ALL = "generate-all";
            public const string QUICK_ACTION_INVOKED = "quick-action.invoke";
            public const string QUICK_ACTIONS_MANAGED = "quick-actions.manage";
            public const string RECENT_BIOMES_INTERACTED = "recent-biomes.interact";
            public const string ASSET_CREATED = "asset.create";
            public const string UTIL_TOOL_INTERACTION = "util-tool.interact";
            public const string VISTA_MANAGER_INSPECTOR_INTERACTION = "vista-manager-inspector.interact";
            public const string BIOME_INSPECTOR_INTERACTION = "biome-inspector.interact";
            public const string ASSET_INSPECTOR_INTERACTION = "asset-inspector.interact";
            public const string GRAPH_EDITOR_INTERACTION = "graph-editor.interact";
            public const string GRAPH_SAVED = "graph.save";
        }

        /// <summary>
        /// Raised after an action is recorded or the stored state is reset.
        /// </summary>
        public static event Action changed;

        /// <summary>
        /// Raised after one action is recorded, with the action id and updated state.
        /// </summary>
        public static event Action<string, SuccessfulActionSnapshot> recorded;

        /// <summary>
        /// Records one successful completion for <paramref name="actionId"/>.
        /// </summary>
        /// <param name="actionId">
        /// A stable, non-empty semantic id such as "terrain.create".
        /// </param>
        public static void Record(string actionId)
        {
            actionId = ValidateActionId(actionId);

            Store store = Load();
            ActionEntry entry = FindEntry(store, actionId);
            if (entry == null)
            {
                entry = new ActionEntry
                {
                    id = actionId
                };
                store.actions.Add(entry);
            }

            long nowUtcTicks = DateTime.UtcNow.Ticks;
            if (store.firstSuccessUtcTicks == 0)
                store.firstSuccessUtcTicks = nowUtcTicks;

            store.lastSuccessUtcTicks = nowUtcTicks;
            entry.lastSuccessUtcTicks = nowUtcTicks;
            entry.count = IncrementWithoutOverflow(entry.count);
            store.totalCount = IncrementWithoutOverflow(store.totalCount);

            Save(store);
            recorded?.Invoke(actionId, CreateSnapshot(store));
            changed?.Invoke();
        }

        /// <summary>
        /// Returns the lifetime number of recorded successful actions.
        /// </summary>
        public static int GetTotalCount()
        {
            return Load().totalCount;
        }

        /// <summary>
        /// Returns the lifetime count for one action id.
        /// </summary>
        public static int GetCount(string actionId)
        {
            actionId = ValidateActionId(actionId);
            ActionEntry entry = FindEntry(Load(), actionId);
            return entry != null ? entry.count : 0;
        }

        /// <summary>
        /// Returns an immutable copy of the current counter state.
        /// </summary>
        public static SuccessfulActionSnapshot GetSnapshot()
        {
            return CreateSnapshot(Load());
        }

        private static SuccessfulActionSnapshot CreateSnapshot(Store store)
        {
            SuccessfulActionCount[] actions = new SuccessfulActionCount[store.actions.Count];
            for (int i = 0; i < store.actions.Count; ++i)
            {
                ActionEntry entry = store.actions[i];
                actions[i] = new SuccessfulActionCount(
                    entry.id,
                    entry.count,
                    ToNullableUtcDateTime(entry.lastSuccessUtcTicks));
            }

            return new SuccessfulActionSnapshot(
                store.totalCount,
                ToNullableUtcDateTime(store.firstSuccessUtcTicks),
                ToNullableUtcDateTime(store.lastSuccessUtcTicks),
                actions);
        }

        /// <summary>
        /// Deletes all locally stored successful-action progress.
        /// </summary>
        public static void Reset()
        {
            EditorPrefs.DeleteKey(PREF_KEY);
            changed?.Invoke();
        }

        private static string ValidateActionId(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId))
                throw new ArgumentException("A successful action id cannot be null, empty, or whitespace.", nameof(actionId));

            return actionId.Trim();
        }

        private static int IncrementWithoutOverflow(int value)
        {
            return value < int.MaxValue ? value + 1 : int.MaxValue;
        }

        private static ActionEntry FindEntry(Store store, string actionId)
        {
            for (int i = 0; i < store.actions.Count; ++i)
            {
                if (string.Equals(store.actions[i].id, actionId, StringComparison.Ordinal))
                    return store.actions[i];
            }

            return null;
        }

        private static Store Load()
        {
            string json = EditorPrefs.GetString(PREF_KEY, string.Empty);
            if (string.IsNullOrEmpty(json))
                return new Store();

            try
            {
                Store store = JsonUtility.FromJson<Store>(json);
                if (store == null || store.schemaVersion != CURRENT_SCHEMA_VERSION)
                    return new Store();

                Normalize(store);
                return store;
            }
            catch (ArgumentException)
            {
                return new Store();
            }
        }

        private static void Normalize(Store store)
        {
            if (store.actions == null)
                store.actions = new List<ActionEntry>();

            int normalizedTotal = 0;
            HashSet<string> knownIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = store.actions.Count - 1; i >= 0; --i)
            {
                ActionEntry entry = store.actions[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.id) ||
                    entry.count <= 0 ||
                    !knownIds.Add(entry.id))
                {
                    store.actions.RemoveAt(i);
                    continue;
                }

                if (int.MaxValue - normalizedTotal < entry.count)
                    normalizedTotal = int.MaxValue;
                else
                    normalizedTotal += entry.count;
            }

            store.totalCount = normalizedTotal;
        }

        private static void Save(Store store)
        {
            EditorPrefs.SetString(PREF_KEY, JsonUtility.ToJson(store));
        }

        private static DateTime? ToNullableUtcDateTime(long ticks)
        {
            if (ticks <= 0 || ticks > DateTime.MaxValue.Ticks)
                return null;

            return new DateTime(ticks, DateTimeKind.Utc);
        }

        [Serializable]
        private class Store
        {
            public int schemaVersion = CURRENT_SCHEMA_VERSION;
            public int totalCount;
            public long firstSuccessUtcTicks;
            public long lastSuccessUtcTicks;
            public List<ActionEntry> actions = new List<ActionEntry>();
        }

        [Serializable]
        private class ActionEntry
        {
            public string id;
            public int count;
            public long lastSuccessUtcTicks;
        }
    }

    /// <summary>
    /// A read-only copy of all successful-action counter state.
    /// </summary>
    public sealed class SuccessfulActionSnapshot
    {
        private readonly SuccessfulActionCount[] m_actions;

        public int totalCount { get; }
        public int distinctActionCount => m_actions.Length;
        public DateTime? firstSuccessUtc { get; }
        public DateTime? lastSuccessUtc { get; }
        public IReadOnlyList<SuccessfulActionCount> actions => m_actions;

        internal SuccessfulActionSnapshot(
            int totalCount,
            DateTime? firstSuccessUtc,
            DateTime? lastSuccessUtc,
            SuccessfulActionCount[] actions)
        {
            this.totalCount = totalCount;
            this.firstSuccessUtc = firstSuccessUtc;
            this.lastSuccessUtc = lastSuccessUtc;
            m_actions = actions ?? new SuccessfulActionCount[0];
        }
    }

    /// <summary>
    /// A read-only count and last-success timestamp for one action id.
    /// </summary>
    public sealed class SuccessfulActionCount
    {
        public string actionId { get; }
        public int count { get; }
        public DateTime? lastSuccessUtc { get; }

        internal SuccessfulActionCount(string actionId, int count, DateTime? lastSuccessUtc)
        {
            this.actionId = actionId;
            this.count = count;
            this.lastSuccessUtc = lastSuccessUtc;
        }
    }

}
#endif
