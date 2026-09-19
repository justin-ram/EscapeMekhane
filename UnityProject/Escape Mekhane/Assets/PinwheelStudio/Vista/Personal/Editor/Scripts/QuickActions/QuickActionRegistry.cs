#if VISTA
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.QuickActions
{
    /// <summary>
    /// Discovers and looks up quick actions. Scans every assembly via TypeCache for methods tagged with
    /// <see cref="VistaQuickActionAttribute"/>, validates them, and caches the resulting descriptors.
    /// Pure discovery, it does not persist pins or render anything.
    /// </summary>
    public static class QuickActionRegistry
    {
        private const string DEFAULT_ICON_PATH = "res:Vista/Textures/QuickActionIcon";

        private static List<QuickAction> s_actions;
        private static Texture s_defaultIcon;

        /// <summary>All discovered actions, sorted by category, order, then label.</summary>
        public static IReadOnlyList<QuickAction> All()
        {
            EnsureBuilt();
            return s_actions;
        }

        /// <summary>The action with this id, or null if none is present (for example its module is not installed).</summary>
        public static QuickAction Find(string id)
        {
            EnsureBuilt();
            for (int i = 0; i < s_actions.Count; i++)
            {
                if (s_actions[i].Id == id)
                    return s_actions[i];
            }
            return null;
        }

        /// <summary>Actions grouped by category, for the manage menu.</summary>
        public static IEnumerable<IGrouping<string, QuickAction>> AllByCategory()
        {
            EnsureBuilt();
            return s_actions.GroupBy(a => a.Category);
        }

        private static void EnsureBuilt()
        {
            // Static state resets on every domain reload, so this rebuilds against the current assemblies.
            if (s_actions != null)
                return;

            s_actions = new List<QuickAction>();
            HashSet<string> seenIds = new HashSet<string>();

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<VistaQuickActionAttribute>())
            {
                if (!IsValid(method, out string reason))
                {
                    Debug.LogWarning($"[VistaQuickAction] Skipped {Describe(method)}: {reason}");
                    continue;
                }

                VistaQuickActionAttribute attr = method.GetCustomAttribute<VistaQuickActionAttribute>();
                if (!QuickActionPins.CanStore(attr.Id))
                {
                    Debug.LogWarning($"[VistaQuickAction] Invalid id '{attr.Id}' on {Describe(method)}: an id must be non-empty and must not contain the reserved separator.");
                    continue;
                }
                if (!seenIds.Add(attr.Id))
                {
                    Debug.LogWarning($"[VistaQuickAction] Duplicate id '{attr.Id}' on {Describe(method)}, ignored.");
                    continue;
                }

                MethodInfo captured = method;
                Action<VistaQuickActionContext> invoke = ctx => captured.Invoke(null, new object[] { ctx });
                Texture icon = ResolveIcon(attr);
                s_actions.Add(new QuickAction(attr.Id, attr.Label, attr.Description, attr.Category, attr.Order, icon, invoke));
            }

            s_actions.Sort((a, b) =>
            {
                int byCategory = string.CompareOrdinal(a.Category, b.Category);
                if (byCategory != 0) return byCategory;
                int byOrder = a.Order.CompareTo(b.Order);
                if (byOrder != 0) return byOrder;
                return string.CompareOrdinal(a.Label, b.Label);
            });
        }

        private static Texture ResolveIcon(VistaQuickActionAttribute attr)
        {
            if (!string.IsNullOrWhiteSpace(attr.IconPath))
                return EditorCommon.LoadIcon(attr.IconPath);

            if (s_defaultIcon == null)
                s_defaultIcon = EditorCommon.LoadIcon(DEFAULT_ICON_PATH);
            return s_defaultIcon;
        }

        private static bool IsValid(MethodInfo method, out string reason)
        {
            if (!method.IsStatic)
            {
                reason = "method must be static";
                return false;
            }
            if (method.ReturnType != typeof(void))
            {
                reason = "method must return void";
                return false;
            }
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length != 1 || parameters[0].ParameterType != typeof(VistaQuickActionContext))
            {
                reason = "method must take a single VistaQuickActionContext parameter";
                return false;
            }
            reason = null;
            return true;
        }

        private static string Describe(MethodInfo method)
        {
            return (method.DeclaringType != null ? method.DeclaringType.FullName + "." : "") + method.Name;
        }
    }
}
#endif
