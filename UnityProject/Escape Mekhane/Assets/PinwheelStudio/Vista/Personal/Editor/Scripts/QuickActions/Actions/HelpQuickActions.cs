#if VISTA
using System;
using System.Reflection;
using Pinwheel.VistaEditor.QuickActions;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Help
{
    /// <summary>
    /// Quick actions owned by the post-purchase help resources feature set.
    /// </summary>
    public static class HelpQuickActions
    {
        private const string DEMO_SCENES_GUID = "25ca33b9a62b6954facbbf94e3af5139";

        [VistaQuickAction(
            "vista.help.documentation", 
            "Help", 
            "Documentation", 
            "Open the online Vista documentation.", 
            0,
            "res:Vista/Textures/DocumentationIcon")]
        private static void Documentation(VistaQuickActionContext ctx)
        {
            Application.OpenURL(Links.DOC);
        }

        [VistaQuickAction(
            "vista.help.tutorials",
            "Help",
            "Tutorials",
            "Watch Vista tutorials on YouTube.",
            1,
            "res:Vista/Textures/YoutubeIcon")]
        private static void Tutorials(VistaQuickActionContext ctx)
        {
            Application.OpenURL(Links.YOUTUBE);
        }

        [VistaQuickAction(
            "vista.help.contact", 
            "Help", 
            "Contact", 
            "Open the contact page on the Pinwheel website.", 
            2,
            "res:Vista/Textures/EmailIcon")]
        private static void Contact(VistaQuickActionContext ctx)
        {
            Application.OpenURL(Links.CONTACT_PAGE);
        }

        [VistaQuickAction(
            "vista.help.locate-demo-scenes",
            "Help",
            "Locate Demo Scenes",
            "Locate Vista's demo scenes in the Project window.",
            3,
            "default:SceneAsset")]
        private static void LocateDemoScenes(VistaQuickActionContext ctx)
        {
            string path = AssetDatabase.GUIDToAssetPath(DEMO_SCENES_GUID);
            if (!AssetDatabase.IsValidFolder(path))
            {
                Debug.LogWarning($"Vista demo scenes folder was not found.");
                return;
            }

            DefaultAsset folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            OpenFolder(folder);
        }

        private static void OpenFolder(DefaultAsset folder)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = folder;

            // Unity exposes selection and pinging publicly, but not Project Browser folder navigation.
            // Use the editor's internal browser method when available and retain select/ping as a safe
            // fallback for Unity versions where that internal API changes.
            Type browserType = typeof(ProjectWindowUtil).Assembly.GetType("UnityEditor.ProjectBrowser");
            MethodInfo showFolder = browserType?.GetMethod(
                "ShowFolderContents",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(bool) },
                null);

            if (browserType != null && showFolder != null)
            {
                try
                {
                    EditorWindow browser = EditorWindow.GetWindow(browserType);
                    showFolder.Invoke(browser, new object[] { folder.GetInstanceID(), true });
                    browser.Repaint();
                    return;
                }
                catch
                {
                    
                }
            }

        }
    }
}
#endif
