#if VISTA
using Pinwheel.VistaEditor.Wizard;

namespace Pinwheel.VistaEditor.QuickActions
{
    /// <summary>
    /// Passed to a quick action when it runs. Carries the runtime context the action may need, today the
    /// hosting wizard window so navigation actions can push pages. Context free actions ignore it.
    /// </summary>
    public class VistaQuickActionContext
    {
        /// <summary>The wizard the action was launched from. May be null if launched without one.</summary>
        public WizardWindow Wizard { get; }

        public VistaQuickActionContext(WizardWindow wizard)
        {
            Wizard = wizard;
        }
    }
}
#endif
