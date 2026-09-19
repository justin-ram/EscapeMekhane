#if VISTA
using System;
using Pinwheel.Vista;
using Pinwheel.VistaEditor.UIElements;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// The Status section on the Home dashboard. Since the wizard only renders when the project compiled,
    /// this is not a hard gate, it is a "works best with Vista" report: it confirms what is good, warns
    /// what is suboptimal, and flags what is unsupported. Three groups: Environment (capability the GPU /
    /// editor must provide), Dependencies (always present, shown as reassurance), and Integrations (status
    /// when installed, a quiet cross sell when not). Display only, the hosting page owns the section header.
    /// </summary>
    public class SystemStatusView : VisualElement
    {
        private enum Level { Ok, Warn, Error, Info }

        public SystemStatusView()
        {
            Build();
        }

        public void Refresh()
        {
            Clear();
            Build();
        }

        private void Build()
        {
            VisualElement environment = AddColumn("Environment");
            Level osLevel = EditorOSLevel(out string osInfo, out string osTooltip);
            AddRow(environment, osLevel, "Editor OS", osInfo, osTooltip);
            GraphicsDeviceType graphicsApi = SystemInfo.graphicsDeviceType;
            AddRow(environment, GraphicsApiLevel(graphicsApi), "Graphics API", graphicsApi.ToString(),
                "Supports Direct3D11, Direct3D12, Vulkan, or Metal");
            bool compute = SystemInfo.supportsComputeShaders;
            AddRow(environment, compute ? Level.Ok : Level.Error, "Compute Shaders", null,
                compute ? "Supported" : "Not supported. Vista needs compute shaders to generate terrain");
            AddRow(environment, UnityVersionLevel(), "Unity", Application.unityVersion,
                "Vista requires Unity 2022.3 LTS or newer");
            string renderPipeline = RenderPipelineName();
            AddRow(environment, renderPipeline == "URP" ? Level.Ok : Level.Warn, "Render Pipeline", renderPipeline,
                "Vista is render pipeline independent, but sample scenes are made for URP.");

            VisualElement dependencies = AddColumn("Dependencies");
            AddDependency(dependencies, "Searcher", "UnityEditor.Searcher.Searcher", "com.unity.searcher");
            AddDependency(dependencies, "Editor Coroutines", "Unity.EditorCoroutines.Editor.EditorCoroutineUtility", "com.unity.editorcoroutines");
            AddDependency(dependencies, "Mathematics", "Unity.Mathematics.math", "com.unity.mathematics");

            VisualElement integrations = AddColumn("Integrations");
            // Polaris is detected by its type, not the GRIFFIN define, so a stale define left behind after
            // uninstalling Polaris does not read as "installed".
            AddIntegration(integrations, "Polaris", TypeExists("Pinwheel.Griffin.GStylizedTerrain"), Links.POLARIS);
            // MicroSplat is third party, so its absence is a plain status, not a Pinwheel cross sell.
            // Its Vista integration ships with Indie and Pro, not Personal.
            AddMicroSplatIntegration(integrations);
            // Spline providers are optional and only relevant to the Pro Splines feature, so they show only
            // for Pro and read as neutral (not an error) when absent. Any one provider is enough.
            if (EditorCommon.IsProEdition())
            {
                AddSplineProvider(integrations, "Unity Splines", "UnityEngine.Splines.SplineContainer");
                AddSplineProvider(integrations, "Dreamteck Splines", "Dreamteck.Splines.SplineComputer");
                AddSplineProvider(integrations, "Curvy Splines", "FluffyUnderware.Curvy.CurvySpline");
                AddSplineProvider(integrations, "Bezier Solution", "BezierSolution.BezierSpline");
            }

            VisualElement columns = VistaUI.Row(environment, dependencies, integrations);
            columns.AddToClassList("status-columns");
            Add(columns);
        }

        // --- Columns and rows ---

        private VisualElement AddColumn(string title)
        {
            Label label = VistaUI.Label(title).H3();

            VisualElement column = VistaUI.Column(label);
            column.AddToClassList("status-column");
            return column;
        }

        // A row reads "Label: Info" on the left with the status dot pushed to the right (the dot's
        // margin-left: auto does the pushing). The fuller detail goes on the row tooltip. When onClick is
        // set the whole row is clickable and the label reads as a link (used by the cross sell rows).
        private void AddRow(VisualElement column, Level level, string label, string info, string tooltip, Action onClick = null)
        {
            Label labelElement = VistaUI.Label(string.IsNullOrEmpty(info) ? label : label + ":");
            labelElement.AddToClassList("status-row__label");

            Label infoElement = null;
            if (!string.IsNullOrEmpty(info))
            {
                infoElement = VistaUI.Label(info).Faded();
                infoElement.AddToClassList("status-row__info");
            }

            VisualElement dot = new VisualElement();
            dot.AddToClassList("status-row__dot");
            dot.AddToClassList("status-row__dot--" + LevelClass(level));

            // VistaUI.Row skips null children, so a row without an info value is fine.
            VisualElement row = VistaUI.Row(labelElement, infoElement, dot);
            row.AddToClassList("status-row");
            if (!string.IsNullOrEmpty(tooltip))
                row.tooltip = tooltip;
            if (onClick != null)
            {
                labelElement.Link();
                row.AddToClassList("status-row--link");
                row.AddManipulator(new Clickable(onClick));
            }

            column.Add(row);
        }

        private void AddDependency(VisualElement column, string label, string typeName, string packageId)
        {
            bool present = TypeExists(typeName);
            AddRow(column, present ? Level.Ok : Level.Error, label, null,
                (present ? "Installed" : "Missing") + " (" + packageId + ")");
        }

        private void AddIntegration(VisualElement column, string label, bool installed, string getUrl)
        {
            if (installed)
                AddRow(column, Level.Ok, label, null, label + " is installed and integrated");
            else if (!string.IsNullOrEmpty(getUrl))
                AddRow(column, Level.Info, label, null, "Not installed. Get " + label + " on the Asset Store", () =>
                {
                    NetUtils.TrackClick(label, UILocation.Wizard_Home);
                    Application.OpenURL(getUrl);
                });
            else
                AddRow(column, Level.Info, label, null, "Not installed");
        }

        private void AddMicroSplatIntegration(VisualElement column)
        {
            if (EditorCommon.IsPersonalEdition())
            {
                AddRow(column, Level.Info, "MicroSplat", null,
                    IsMicroSplatInstalled
                        ? "MicroSplat is installed. Vista integration is available in Vista Indie or Pro"
                        : "MicroSplat integration is available in Vista Indie or Pro");
                return;
            }

            AddIntegration(column, "MicroSplat", IsMicroSplatInstalled, null);
        }

        // Optional spline providers for the Pro Splines feature. Detected by their main component type, the
        // same one each evaluator's [RequireComponent] uses, since the provider's VISTA_*_SPLINE symbol is
        // local to the Splines assembly and not visible here. Any one is enough, so absent is neutral.
        private void AddSplineProvider(VisualElement column, string label, string typeName)
        {
            bool present = TypeExists(typeName);
            AddRow(column, present ? Level.Ok : Level.Info, label, null,
                present
                    ? "Installed, usable as a spline provider for Vista Splines"
                    : "Optional spline provider for the Vista Splines feature");
        }

        // --- Detection ---

        // Warn below Unity 2022.3, using Unity's own version define so there is nothing to parse.
        private static Level UnityVersionLevel()
        {
#if UNITY_2022_3_OR_NEWER
            return Level.Ok;
#else
            return Level.Warn;
#endif
        }

        private static Level GraphicsApiLevel(GraphicsDeviceType api)
        {
            switch (api)
            {
                case GraphicsDeviceType.Direct3D11:
                case GraphicsDeviceType.Direct3D12:
                case GraphicsDeviceType.Vulkan:
                case GraphicsDeviceType.Metal:
                    return Level.Ok;
                default:
                    return Level.Error;
            }
        }

        private static Level EditorOSLevel(out string info, out string tooltip)
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    info = "Windows";
                    tooltip = "Windows, fully supported";
                    return Level.Ok;
                case RuntimePlatform.OSXEditor:
                    info = "macOS";
                    tooltip = "macOS, fully supported";
                    return Level.Ok;
                case RuntimePlatform.LinuxEditor:
                    info = "Linux";
                    tooltip = "Linux is not supported. Vista is tested on Windows and Mac";
                    return Level.Error;
                default:
                    info = Application.platform.ToString();
                    tooltip = "Untested editor platform";
                    return Level.Error;
            }
        }

        private static string RenderPipelineName()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline
                : GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null)
                return "Built-in";
            string typeName = pipeline.GetType().Name;
            if (typeName.Contains("Universal"))
                return "URP";
            if (typeName.Contains("HDRenderPipeline") || typeName.Contains("HighDefinition"))
                return "HDRP";
            return typeName;
        }

        private static bool TypeExists(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetType(fullName) != null)
                    return true;
            }
            return false;
        }

        private static bool IsMicroSplatInstalled =>
#if __MICROSPLAT__
            true;
#else
            false;
#endif

        private static string LevelClass(Level level)
        {
            switch (level)
            {
                case Level.Ok: return "ok";
                case Level.Warn: return "warn";
                case Level.Error: return "error";
                default: return "info";
            }
        }
    }
}
#endif
