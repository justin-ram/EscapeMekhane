#if VISTA
using System.Collections.Generic;
using UnityEditor;

namespace Pinwheel.VistaEditor.QuickActions
{
    /// <summary>
    /// Persists which quick actions are pinned, and in what order, as a flat list of ids in EditorPrefs
    /// (global per machine). Pure id storage, it does not know what the ids mean. Ids that no longer
    /// resolve (their action's module was removed after pinning) just sit here as strings and are skipped
    /// when rendering.
    /// </summary>
    public static class QuickActionPins
    {
        private const string KEY = "Pinwheel.Vista.QuickActions.Pinned";
        private const string SEPARATOR = "|";
        private const string LEGACY_DOCUMENTATION_ID = "vista.documentation";
        private const string DOCUMENTATION_ID = "vista.help.documentation";

        private static readonly HashSet<string> RETIRED_IDS = new HashSet<string>
        {
            "vista.new-biome",
            "vista.create-asset",
            "vista.tools",
            "vista.learn",
            "vista.discord",
        };

        /// <summary>The pinned ids in order. On first ever use (no saved value) seeds from <paramref name="defaultsIfUnset"/>.</summary>
        public static List<string> GetPinned(IReadOnlyList<string> defaultsIfUnset)
        {
            if (!EditorPrefs.HasKey(KEY))
            {
                Save(defaultsIfUnset);
            }
            return Load();
        }

        /// <summary>True if an id is safe to persist: non-empty and free of the reserved separator.</summary>
        public static bool CanStore(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && !id.Contains(SEPARATOR);
        }

        public static bool IsPinned(string id)
        {
            return Load().Contains(id);
        }

        public static void Pin(string id)
        {
            List<string> pinned = Load();
            if (!pinned.Contains(id))
            {
                pinned.Add(id);
                Save(pinned);
            }
        }

        public static void Unpin(string id)
        {
            List<string> pinned = Load();
            if (pinned.Remove(id))
            {
                Save(pinned);
            }
        }

        /// <summary>Replace the user's saved pins with the factory defaults, preserving default order.</summary>
        public static void ResetToDefaults()
        {
            Save(QuickActionDefaults.Pinned);
        }

        private static List<string> Load()
        {
            string stored = EditorPrefs.GetString(KEY, string.Empty);
            List<string> pinned = string.IsNullOrEmpty(stored)
                ? new List<string>()
                : new List<string>(stored.Split(new[] { SEPARATOR }, System.StringSplitOptions.RemoveEmptyEntries));

            bool changed = false;
            for (int i = pinned.Count - 1; i >= 0; i--)
            {
                string id = pinned[i];
                if (id == LEGACY_DOCUMENTATION_ID)
                {
                    if (pinned.Contains(DOCUMENTATION_ID))
                        pinned.RemoveAt(i);
                    else
                        pinned[i] = DOCUMENTATION_ID;
                    changed = true;
                }
                else if (RETIRED_IDS.Contains(id))
                {
                    pinned.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
                Save(pinned);

            return pinned;
        }

        private static void Save(IEnumerable<string> pinned)
        {
            EditorPrefs.SetString(KEY, string.Join(SEPARATOR, pinned));
        }
    }
}
#endif
