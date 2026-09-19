#if VISTA
using Pinwheel.VistaEditor.UIElements;
using Pinwheel.VistaEditor.Wizard;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.QuickActions
{
    /// <summary>
    /// Displays the pinned quick actions as a wrapping grid of tiles (label plus description). Display
    /// only, the host owns the Manage control and calls <see cref="Refresh"/> after pins change. The view
    /// itself is the grid container.
    /// </summary>
    public class QuickActionsView : VisualElement
    {
        private readonly WizardWindow m_window;

        public QuickActionsView(WizardWindow window)
        {
            m_window = window;
            AddToClassList("quick-action-grid");
            Refresh();
        }

        /// <summary>Rebuild the tiles from the current pin state. Call after pins change.</summary>
        public void Refresh()
        {
            Clear();
            foreach (string id in QuickActionPins.GetPinned(QuickActionDefaults.Pinned))
            {
                QuickAction action = QuickActionRegistry.Find(id);
                if (action == null)
                    continue; // pinned but not present (for example its module is not installed)

                QuickAction captured = action;
                // A quick action commits an action, so it is a Clickable, not a NavCard. Title only tile,
                // the description shows on hover via the tooltip.
                ActionTile tile = new ActionTile(
                    action.Label,
                    action.Icon,
                    () =>
                    {
                        captured.Invoke(new VistaQuickActionContext(m_window));
                        SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.QUICK_ACTION_INVOKED);
                    });
                tile.tooltip = action.Description;
                Add(tile);
            }
        }
    }
}
#endif
