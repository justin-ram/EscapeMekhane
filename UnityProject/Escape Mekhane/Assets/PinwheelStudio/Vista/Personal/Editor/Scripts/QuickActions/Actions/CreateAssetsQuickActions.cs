#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using Pinwheel.VistaEditor.QuickActions;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.CreateAssets
{
    /// <summary>
    /// Shortcuts mirroring Vista's native Assets/Create paths. Unity creates each asset in the currently
    /// selected Project folder, falls back to Assets, and starts inline naming.
    /// </summary>
    public static class CreateAssetsQuickActions
    {
        [VistaQuickAction("vista.create-assets.terrain-graph", "Create Assets", "Terrain Graph", "Create a Vista Terrain Graph asset.", 0, "res:Vista/Textures/TerrainGraphIcon")]
        private static void CreateTerrainGraph(VistaQuickActionContext ctx)
        {
            Create<TerrainGraph>("New Terrain Graph.asset");
        }

        [VistaQuickAction("vista.create-assets.tree-template", "Create Assets", "Tree Template", "Create a Vista Tree Template asset.", 2, "default:ScriptableObject")]
        private static void CreateTreeTemplate(VistaQuickActionContext ctx)
        {
            Create<TreeTemplate>("New Tree Template.asset");
        }

        [VistaQuickAction("vista.create-assets.object-template", "Create Assets", "Object Template", "Create a Vista Object Template asset.", 3, "default:ScriptableObject")]
        private static void CreateObjectTemplate(VistaQuickActionContext ctx)
        {
            Create<ObjectTemplate>("New Object Template.asset");
        }

        [VistaQuickAction("vista.create-assets.detail-template", "Create Assets", "Detail Template", "Create a Vista Detail Template asset.", 4, "default:ScriptableObject")]
        private static void CreateDetailTemplate(VistaQuickActionContext ctx)
        {
            Create<DetailTemplate>("New Detail Template.asset");
        }

        [VistaQuickAction("vista.create-assets.position-container", "Create Assets", "Position Container", "Create a Vista Position Container asset.", 5, "default:ScriptableObject")]
        private static void CreatePositionContainer(VistaQuickActionContext ctx)
        {
            Create<PositionContainer>("New Position Container.asset");
        }

        private static void Create<T>(string defaultName) where T : ScriptableObject
        {
            ProjectWindowUtil.CreateAsset(ScriptableObject.CreateInstance<T>(), defaultName);
        }
    }
}
#endif
