#if VISTA
using Pinwheel.Vista.Diagnostics;

namespace Pinwheel.VistaEditor.QuickActions.Actions
{
    /// <summary>
    /// Shortcuts to Vista editor utilities.
    /// </summary>
    public static class UtilityQuickActions
    {
        [VistaQuickAction(
            "vista.utilities.session-viewer",
            "Utilities",
            "Session Viewer",
            "Open the Vista diagnostics session viewer.",
            0)]
        private static void OpenSessionViewer(VistaQuickActionContext ctx)
        {
            VistaDebuggerWindow.Open();
        }
    }
}
#endif
