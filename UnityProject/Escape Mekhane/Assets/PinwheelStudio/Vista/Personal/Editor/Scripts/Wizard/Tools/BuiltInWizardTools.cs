#if VISTA
using System;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Always-compiled catalog entries. Higher-edition modules bind their implementations at editor
    /// initialization time, leaving the corresponding Personal-edition tile visible but unavailable.
    /// </summary>
    internal static class BuiltInWizardTools
    {
        internal static event Action<VistaToolContext> openSplinesSetup;
        internal static event Action<VistaToolContext> openMicroSplatSetup;

        [VistaWizardTool]
        private static WizardTool SplinesSetup()
        {
            return new WizardTool(
                "pinwheel.vista.splines-setup",
                VistaToolCategories.SetupAndIntegration,
                "Splines Setup",
                "Connect scene splines to Vista terrain graphs.",
                context => openSplinesSetup?.Invoke(context),
                () => openSplinesSetup != null
                    ? ToolAvailability.Available()
                    : ToolAvailability.Unavailable("Available in Vista Pro"),
                0,
                "res:Vista/Textures/SplineIcon",
                true);
        }

        [VistaWizardTool]
        private static WizardTool MicroSplatSetup()
        {
            return new WizardTool(
                "pinwheel.vista.microsplat-setup",
                VistaToolCategories.SetupAndIntegration,
                "MicroSplat Setup",
                "Connect MicroSplat to a Vista Manager in the current scene.",
                context => openMicroSplatSetup?.Invoke(context),
                () => openMicroSplatSetup != null
                    ? ToolAvailability.Available()
                    : ToolAvailability.Unavailable("Requires MicroSplat and Vista Indie or Pro"),
                10,
                "",
                true);
        }

        [VistaWizardTool]
        private static WizardTool MultiTerrainManagement()
        {
            return new WizardTool(
                "pinwheel.vista.multi-terrain-management",
                VistaToolCategories.Utilities,
                "Multi-Terrain Management",
                "Discover and open tools for managing multiple Unity or Polaris terrains.",
                context => context.window.PushPage(new MultiTerrainManagementPage()),
                order: 0,
                opensWizardPage: true);
        }

        [VistaWizardTool]
        private static WizardTool TerrainLayerDiffuseColor()
        {
            return new WizardTool(
                "pinwheel.vista.terrain-layer-diffuse-color",
                VistaToolCategories.Utilities,
                "Terrain Layer Diffuse Color",
                "Generate a representative diffuse-color texture for a Terrain Layer.",
                context => TerrainLayerDiffuseColorWindow.ShowWindow(),
                order: 0);
        }

        [VistaWizardTool]
        private static WizardTool SessionViewer()
        {
            return new WizardTool(
                "pinwheel.vista.session-viewer",
                VistaToolCategories.Utilities,
                "Session Viewer",
                "Inspect Vista generation sessions, timings, warnings, and failures.",
                context => Pinwheel.Vista.Diagnostics.VistaDebuggerWindow.Open(),
                order: 10);
        }
    }
}
#endif
