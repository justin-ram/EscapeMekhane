#if VISTA
using System;
using System.Reflection;
using Pinwheel.VistaEditor.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
#if GRIFFIN
using Pinwheel.Griffin.GroupTool;
#endif

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Discovery bridge for the multi-terrain tools already owned by Unity Terrain Tools and Polaris.
    /// It deliberately opens or creates those tools instead of reproducing their management UI.
    /// </summary>
    public sealed class MultiTerrainManagementPage : HubPage
    {
        private const string TERRAIN_TOOLS_PACKAGE = "com.unity.terrain-tools";
        private const string TERRAIN_TOOLBOX_MENU = "Window/Terrain/Terrain Toolbox";
        private const string TERRAIN_TOOLBOX_TYPE =
            "UnityEditor.TerrainTools.TerrainToolboxWindow, Unity.TerrainTools.Editor";
        private const string TERRAIN_TOOLBOX_MODE_FIELD = "m_SelectedMode";
        private const int TERRAIN_SETTINGS_MODE = 1;
        private const string POLARIS_GROUP_MENU = "GameObject/3D Object/Polaris/Group Tool";

        private VisualElement m_sections;

        public override string title => "Multi-Terrain Management";

        protected override HubNavId HighlightedNav => HubNavId.Tools;

        protected override void BuildContent(VisualElement content)
        {
            content.Add(VistaUI.Label(
                "Use the terrain system's own multi-terrain workflow. Vista only helps you find and open it.").P1());

            m_sections = VistaUI.Column();
            content.Add(m_sections);
            BuildSections();
        }

        public override void OnWindowFocus(WizardWindow host)
        {
            Rebuild();
        }

        private void Rebuild()
        {
            if (m_sections == null)
                return;

            m_sections.Clear();
            BuildSections();
        }

        private void BuildSections()
        {
            BuildPolarisSection();
            m_sections.Add(VistaUI.Spacer(8));
            BuildUnityTerrainSection();
        }

        private void BuildUnityTerrainSection()
        {
            VisualElement section = VistaUI.Column();
            section.Add(VistaUI.Label("Unity Terrain").H3());
            section.Add(VistaUI.Label(
                "Use Terrain Toolbox > Terrain Settings to apply Terrain settings across multiple Unity Terrain tiles.").P1());

            if (IsPackageInstalled(TERRAIN_TOOLS_PACKAGE))
            {
                section.Add(VistaUI.Clickable(
                    "Open Terrain Toolbox",
                    () => InvokeAndRecord(OpenTerrainSettings)).Chip());
            }
            else
            {
                section.Add(VistaUI.Clickable(
                    "Install with Package Manager",
                    () => InvokeAndRecord(
                        () => UnityEditor.PackageManager.UI.Window.Open(TERRAIN_TOOLS_PACKAGE))).Chip());
            }

            m_sections.Add(section);
        }

        private void BuildPolarisSection()
        {
            VisualElement section = VistaUI.Column();
            section.Add(VistaUI.Label("Polaris Terrain").H3());
            section.Add(VistaUI.Label(
                "Polaris Group Tool lets you create, select, and edit a group of Polaris terrains from one Inspector.").P1());

#if !GRIFFIN
                section.Add(VistaUI.Clickable("Learn About Polaris", () =>
                {
                    InvokeAndRecord(() => Application.OpenURL(Links.POLARIS));
                }).Chip());
                m_sections.Add(section);
                return;
#else

            GTerrainGroup group = FindPolarisGroupTool();
            VisualElement row = VistaUI.Row();
            ObjectField field = new ObjectField()
            {
                objectType = typeof(GTerrainGroup),
                allowSceneObjects = true,
                value = group,
            };
            field.style.flexGrow = 1;
            field.SetEnabled(false);
            row.Add(field);

            if (group == null)
            {
                row.Add(VistaUI.Clickable(
                    "Create Group Tool",
                    () => InvokeAndRecord(CreatePolarisGroupTool)).Chip());
            }
            else
            {
                row.Add(VistaUI.Clickable("Select", () =>
                {
                    InvokeAndRecord(() =>
                    {
                        Selection.activeObject = group;
                        EditorGUIUtility.PingObject(group);
                    });
                }).Chip());
            }

            section.Add(row);
            m_sections.Add(section);
#endif
        }

        private static void OpenTerrainSettings()
        {
            if (!EditorApplication.ExecuteMenuItem(TERRAIN_TOOLBOX_MENU))
            {
                Debug.LogWarning("Terrain Toolbox menu item was not found.");
                return;
            }

            EditorApplication.delayCall += SelectTerrainSettingsTab;
        }

        private static void SelectTerrainSettingsTab()
        {
            Type windowType = Type.GetType(TERRAIN_TOOLBOX_TYPE);
            if (windowType == null)
                return;

            FieldInfo modeField = windowType.GetField(
                TERRAIN_TOOLBOX_MODE_FIELD,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (modeField == null || !modeField.FieldType.IsEnum)
                return;

            EditorWindow window = EditorWindow.GetWindow(windowType);
            modeField.SetValue(window, Enum.ToObject(modeField.FieldType, TERRAIN_SETTINGS_MODE));
            window.Repaint();
        }

        private void CreatePolarisGroupTool()
        {
            if (!EditorApplication.ExecuteMenuItem(POLARIS_GROUP_MENU))
            {
                Debug.LogWarning("Polaris Group Tool menu item was not found.");
                return;
            }

            Rebuild();
        }

        private static bool IsPackageInstalled(string packageName)
        {
            foreach (UnityEditor.PackageManager.PackageInfo package in
                UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
            {
                if (package.name == packageName)
                    return true;
            }
            return false;
        }

        private static void InvokeAndRecord(Action interaction)
        {
            interaction?.Invoke();
            SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.UTIL_TOOL_INTERACTION);
        }

#if GRIFFIN
        private static GTerrainGroup FindPolarisGroupTool()
        {
            foreach (GTerrainGroup group in Resources.FindObjectsOfTypeAll<GTerrainGroup>())
            {
                if (group != null &&
                    group.gameObject.scene.IsValid() &&
                    !EditorUtility.IsPersistent(group))
                {
                    return group;
                }
            }
            return null;
        }
#endif
    }
}
#endif
