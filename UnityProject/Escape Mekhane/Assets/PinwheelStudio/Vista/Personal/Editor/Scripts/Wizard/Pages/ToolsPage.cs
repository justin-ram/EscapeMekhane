#if VISTA
using System.Collections.Generic;
using System.Linq;
using Pinwheel.VistaEditor.UIElements;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>Discoverable catalog of self-registered Vista and third-party tools.</summary>
    public class ToolsPage : HubPage, INavLevelPage
    {
        private readonly List<ToolTile> m_tiles = new List<ToolTile>();

        public override string title => "Tools";

        protected override HubNavId HighlightedNav => HubNavId.Tools;

        protected override void BuildTitleStripActions(VisualElement container)
        {
            VisualElement availability = VistaUI.Row();
            availability.AddToClassList("wizard-tool-availability");
            availability.Add(VistaUI.Label("Tool availability varies between editions.").Faded());
            container.Add(availability);
        }

        protected override void BuildContent(VisualElement content)
        {
            m_tiles.Clear();

            bool hasTools = false;
            foreach (IGrouping<string, WizardTool> group in WizardToolRegistry.AllByCategory())
            {
                hasTools = true;
                content.Add(new SectionHeader(group.Key));

                VisualElement grid = new VisualElement();
                grid.AddToClassList("wizard-tool-grid");
                foreach (WizardTool tool in group)
                {
                    ToolTile tile = new ToolTile(tool, host);
                    m_tiles.Add(tile);
                    grid.Add(tile.element);
                }
                content.Add(grid);
            }

            if (!hasTools)
                content.Add(VistaUI.Label("No tools are registered.").P1().Faded());

            RefreshAvailability();
        }

        public override void OnWindowFocus(WizardWindow host)
        {
            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            foreach (ToolTile tile in m_tiles)
                tile.Refresh();
        }

        private sealed class ToolTile
        {
            private readonly WizardTool m_tool;
            private readonly WizardWindow m_host;

            public ActionTile element { get; }

            public ToolTile(WizardTool tool, WizardWindow host)
            {
                m_tool = tool;
                m_host = host;
                element = new ActionTile(tool.label, tool.icon, Execute, tool.opensWizardPage);
            }

            public void Refresh()
            {
                ToolAvailability availability = m_tool.GetAvailability();
                element.SetEnabled(availability.isAvailable);
                element.tooltip = availability.isAvailable || string.IsNullOrWhiteSpace(availability.reason)
                    ? m_tool.description
                    : string.IsNullOrWhiteSpace(m_tool.description)
                        ? availability.reason
                        : m_tool.description + "\n\n" + availability.reason;
            }

            private void Execute()
            {
                m_tool.Execute(new VistaToolContext(m_host));
            }
        }
    }
}
#endif
