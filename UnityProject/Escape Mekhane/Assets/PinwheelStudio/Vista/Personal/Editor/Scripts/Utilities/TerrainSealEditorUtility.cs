#if VISTA
using Pinwheel.Vista;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pinwheel.VistaEditor
{
    /// <summary>
    /// Provides the shared editor creation path for Terrain Seals.
    /// </summary>
    internal static class TerrainSealEditorUtility
    {
        internal static TerrainSeal Create(Transform parent = null)
        {
            GameObject sealObject = new GameObject("Terrain Seal");
            TerrainSeal seal = sealObject.AddComponent<TerrainSeal>();
            if (parent != null)
            {
                seal.transform.SetParent(parent, true);
            }

            Undo.RegisterCreatedObjectUndo(sealObject, "Create Terrain Seal");
            Selection.activeObject = seal;
            EditorSceneManager.MarkSceneDirty(sealObject.scene);
            return seal;
        }
    }
}
#endif
