#if VISTA
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Wizard
{
    public static class WizardToolRegistry
    {
        private static List<WizardTool> s_tools;

        public static IReadOnlyList<WizardTool> All()
        {
            EnsureBuilt();
            return s_tools;
        }

        public static IEnumerable<IGrouping<string, WizardTool>> AllByCategory()
        {
            EnsureBuilt();
            return s_tools.GroupBy(t => t.category);
        }

        private static void EnsureBuilt()
        {
            if (s_tools != null)
                return;

            s_tools = new List<WizardTool>();
            HashSet<string> seenIds = new HashSet<string>();

            foreach (MethodInfo method in TypeCache.GetMethodsWithAttribute<VistaWizardToolAttribute>())
            {
                if (!IsValid(method))
                {
                    Debug.LogWarning($"[VistaWizardTool] Skipped {Describe(method)}: method must be static, parameterless, and return WizardTool.");
                    continue;
                }

                WizardTool tool = method.Invoke(null, null) as WizardTool;
                if (tool == null)
                {
                    Debug.LogWarning($"[VistaWizardTool] Skipped {Describe(method)}: provider returned null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(tool.id))
                {
                    Debug.LogWarning($"[VistaWizardTool] Skipped {Describe(method)}: id must not be empty.");
                    continue;
                }
                if (!seenIds.Add(tool.id))
                {
                    Debug.LogWarning($"[VistaWizardTool] Duplicate id '{tool.id}' on {Describe(method)}, ignored.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(tool.label))
                {
                    Debug.LogWarning($"[VistaWizardTool] Skipped {Describe(method)}: label must not be empty.");
                    continue;
                }

                s_tools.Add(tool);
            }

            s_tools.Sort((a, b) =>
            {
                int category = string.CompareOrdinal(a.category, b.category);
                if (category != 0) return category;
                int order = a.order.CompareTo(b.order);
                if (order != 0) return order;
                return string.CompareOrdinal(a.label, b.label);
            });
        }

        private static bool IsValid(MethodInfo method)
        {
            return method.IsStatic &&
                method.GetParameters().Length == 0 &&
                method.ReturnType == typeof(WizardTool);
        }

        private static string Describe(MethodInfo method)
        {
            return (method.DeclaringType != null ? method.DeclaringType.FullName + "." : string.Empty) + method.Name;
        }
    }
}
#endif
