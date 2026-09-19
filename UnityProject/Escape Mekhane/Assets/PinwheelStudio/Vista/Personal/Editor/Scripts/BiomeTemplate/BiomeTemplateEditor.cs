#if VISTA
using System.IO;
using System.Reflection;
using Pinwheel.Vista;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor
{
    /// <summary>
    /// Inspector for <see cref="BiomeTemplate"/>. Adds a one click "Capture Screenshot from Scene View" button
    /// under the default fields: it grabs the current Scene View content as a JPG saved beside the template and
    /// assigns it as the gallery thumbnail. Whatever the Scene View shows is captured, so the author frames the
    /// shot by navigating and toggles gizmos on or off to taste beforehand.
    /// </summary>
    [CustomEditor(typeof(BiomeTemplate))]
    public class BiomeTemplateEditor : Editor
    {
        private static readonly string[] EXCLUDED_PROPERTIES = new string[]
        {
            "m_Script",
            "m_sortingNumber"
        };

        private static readonly PropertyInfo INSPECTOR_MODE_PROPERTY = typeof(SerializedObject).GetProperty(
            "inspectorMode",
            BindingFlags.Instance | BindingFlags.NonPublic);

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawPropertiesExcluding(serializedObject, EXCLUDED_PROPERTIES);

            if (IsDebugInspector())
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_sortingNumber"));
            }
            bool propertiesChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            if (propertiesChanged)
            {
                RecordInspectorInteraction();
            }

            EditorGUILayout.Space();

            bool hasSceneView = SceneView.lastActiveSceneView != null;
            using (new EditorGUI.DisabledScope(!hasSceneView))
            {
                if (GUILayout.Button("Capture Screenshot from Scene View"))
                {
                    CaptureScreenshot();
                    RecordInspectorInteraction();
                }
            }
            if (!hasSceneView)
            {
                EditorGUILayout.HelpBox("Open a Scene View to capture a screenshot.", MessageType.Info);
            }
        }

        private static void RecordInspectorInteraction()
        {
            SuccessfulActionCounter.Record(
                SuccessfulActionCounter.ActionKeys.ASSET_INSPECTOR_INTERACTION);
        }

        private bool IsDebugInspector()
        {
            if (INSPECTOR_MODE_PROPERTY == null)
            {
                return false;
            }

            object inspectorMode = INSPECTOR_MODE_PROPERTY.GetValue(serializedObject);
            return inspectorMode != null && inspectorMode.ToString() == "Debug";
        }

        // Capture the Scene View, save it beside the template as a new "<name>_Screenshot*.jpg" asset, and assign
        // that new texture to the template's screenshot field without overwriting any previous capture.
        private void CaptureScreenshot()
        {
            BiomeTemplate template = (BiomeTemplate)target;

            Texture2D captured = ScreenshotCaptureUtility.CaptureSceneView();
            if (captured == null)
            {
                return;
            }

            string templatePath = AssetDatabase.GetAssetPath(template);
            string folder = Path.GetDirectoryName(templatePath).Replace('\\', '/');
            string baseJpgPath = string.Format("{0}/{1}_Screenshot.jpg", folder, template.name);
            string jpgPath = AssetDatabase.GenerateUniqueAssetPath(baseJpgPath);

            Texture2D saved = ScreenshotCaptureUtility.SaveAsJpg(captured, jpgPath);
            Object.DestroyImmediate(captured);

            // Assign through the serialized object so the change dirties the asset and supports undo.
            serializedObject.Update();
            SerializedProperty screenshot = serializedObject.FindProperty("m_info").FindPropertyRelative("m_screenshot");
            screenshot.objectReferenceValue = saved;
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
