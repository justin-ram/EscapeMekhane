#if VISTA
using System;

namespace Pinwheel.VistaEditor.QuickActions
{
    /// <summary>
    /// Marks a static method as a quick action, surfaced in the wizard's Home dashboard. Core Vista and
    /// any referencing assembly declare actions the same way, the method is discovered automatically.
    ///
    /// The method must be static and take a single <see cref="VistaQuickActionContext"/> parameter:
    /// <code>[VistaQuickAction("my.action", "Tools", "Do Thing")] static void DoThing(VistaQuickActionContext ctx) { }</code>
    ///
    /// Availability is the action's own responsibility, guard it with a compile symbol if it depends on
    /// an optional module. If it does not compile, it is simply not there to discover.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class VistaQuickActionAttribute : Attribute
    {
        /// <summary>Bucket an action falls into when it does not specify a category (or specifies a blank one).</summary>
        public const string DefaultCategory = "General";

        /// <summary>Stable, namespaced unique id. This is what the pin store remembers, so it must outlive method renames.</summary>
        public string Id { get; }

        /// <summary>Display label.</summary>
        public string Label { get; }

        /// <summary>Longer explanation, shown as a tooltip when the label alone is not informative enough. Optional.</summary>
        public string Description { get; }

        /// <summary>Grouping used to find the action in the manage menu. Falls back to <see cref="DefaultCategory"/> when blank.</summary>
        public string Category { get; }

        /// <summary>Default ordering within a category.</summary>
        public int Order { get; }

        /// <summary>
        /// Optional icon reference. Use "res:path" for Resources.Load, "default:ComponentType" for
        /// Unity's component icon, or an Assets-relative asset path. Blank uses the shared default icon.
        /// </summary>
        public string IconPath { get; }

        public VistaQuickActionAttribute(string id, string category, string label, string description = "", int order = 0, string iconPath = "")
        {
            Id = id;
            Category = string.IsNullOrWhiteSpace(category) ? DefaultCategory : category;
            Label = label;
            Description = description ?? "";
            Order = order;
            IconPath = iconPath ?? "";
        }
    }
}
#endif
