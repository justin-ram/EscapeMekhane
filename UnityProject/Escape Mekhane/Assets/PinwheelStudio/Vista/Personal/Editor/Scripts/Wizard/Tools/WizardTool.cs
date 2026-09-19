#if VISTA
using System;
using UnityEngine;

namespace Pinwheel.VistaEditor.Wizard
{
    public sealed class WizardTool
    {
        public string id { get; }
        public string category { get; }
        public string label { get; }
        public string description { get; }
        public int order { get; }
        public Texture icon { get; }
        public bool opensWizardPage { get; }

        private readonly Func<ToolAvailability> m_getAvailability;
        private readonly Action<VistaToolContext> m_execute;

        public WizardTool(
            string id,
            string category,
            string label,
            string description,
            Action<VistaToolContext> execute,
            Func<ToolAvailability> getAvailability = null,
            int order = 0,
            string iconPath = "",
            bool opensWizardPage = false)
        {
            this.id = id;
            this.category = string.IsNullOrWhiteSpace(category) ? VistaToolCategories.Utilities : category;
            this.label = label;
            this.description = description ?? string.Empty;
            this.order = order;
            this.opensWizardPage = opensWizardPage;
            icon = string.IsNullOrWhiteSpace(iconPath) ? null : EditorCommon.LoadIcon(iconPath);
            m_getAvailability = getAvailability;
            m_execute = execute;
        }

        public ToolAvailability GetAvailability()
        {
            if (m_execute == null)
                return ToolAvailability.Unavailable("This tool is unavailable.");
            return m_getAvailability != null ? m_getAvailability() : ToolAvailability.Available();
        }

        public void Execute(VistaToolContext context)
        {
            if (GetAvailability().isAvailable)
                m_execute(context);
        }
    }
}
#endif
