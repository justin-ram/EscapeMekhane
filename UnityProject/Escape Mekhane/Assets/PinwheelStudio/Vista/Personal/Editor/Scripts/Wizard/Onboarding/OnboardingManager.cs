#if VISTA
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Drives first-run onboarding. After each domain load, once Vista initialization is complete, it
    /// opens the Wizard on Get Started while onboarding is still unacknowledged. Opening or closing the
    /// welcome does not change that state; the user must choose See How It Works or Skip.
    ///
    /// Ported from the Polaris GOnboardingManager. Vista has no user familiarity setting, so the
    /// new-user gate is omitted. State is stored per project, so a fresh project shows onboarding again.
    /// </summary>
    [InitializeOnLoad]
    public static class OnboardingManager
    {
        private const string SHOWN_KEY = "Pinwheel.Vista.Onboarding.Shown";

        static OnboardingManager()
        {
            // The static constructor runs early during editor load, while the editor is often still
            // compiling or importing assets, which is exactly the state a fresh project is in right
            // after importing Vista. Defer the decision to ProjectInitializer.RunWhenCompleted, which
            // runs once Vista is initialized and the editor is idle.
            ProjectInitializer.RunWhenCompleted(OnVistaInitialized);
        }

        private static void OnVistaInitialized()
        {
            if (!ShouldShowWizard())
                return;

            WizardWindow.ShowGetStartedPage();
        }

        /// <summary>Project scoped key so onboarding state does not leak between projects on the same machine.</summary>
        private static string ProjectShownKey => SHOWN_KEY + "." + PlayerSettings.productGUID;

        private static bool ShouldShowWizard()
        {
            if (Application.isBatchMode)
                return false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return false;
            if (EditorPrefs.GetBool(ProjectShownKey, false))
                return false;

            return true;
        }

        /// <summary>True once the user has acknowledged onboarding in this project.</summary>
        public static bool HasShownOnboarding()
        {
            return EditorPrefs.GetBool(ProjectShownKey, false);
        }

        /// <summary>Mark onboarding as acknowledged so it does not auto open again.</summary>
        public static void MarkOnboardingShown()
        {
            EditorPrefs.SetBool(ProjectShownKey, true);
        }

        /// <summary>Clear onboarding state for this project, so the next editor load shows it again. Used by the Vista Internal mimic first import menu.</summary>
        public static void ResetState()
        {
            EditorPrefs.DeleteKey(ProjectShownKey);
        }
    }
}
#endif
