#if VISTA
using System;
using UnityEngine;

namespace Pinwheel.VistaEditor.QuickActions
{
    /// <summary>
    /// A discovered quick action, the runtime representation of a method tagged with
    /// <see cref="VistaQuickActionAttribute"/>. A plain value, it does not know or care whether it is
    /// core or third party, or whether it is pinned.
    /// </summary>
    public class QuickAction
    {
        public string Id { get; }
        public string Label { get; }
        public string Description { get; }
        public string Category { get; }
        public int Order { get; }
        public Texture Icon { get; }

        private readonly Action<VistaQuickActionContext> m_invoke;

        public QuickAction(string id, string label, string description, string category, int order, Texture icon, Action<VistaQuickActionContext> invoke)
        {
            Id = id;
            Label = label;
            Description = description;
            Category = category;
            Order = order;
            Icon = icon;
            m_invoke = invoke;
        }

        public void Invoke(VistaQuickActionContext context)
        {
            m_invoke?.Invoke(context);
        }
    }
}
#endif
