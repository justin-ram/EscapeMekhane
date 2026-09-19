#if VISTA
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.VistaEditor.QuickActions;
using Pinwheel.VistaEditor.UIElements;
using Pinwheel.VistaEditor.Wizard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Pinwheel.VistaEditor.SceneSetup
{
    /// <summary>
    /// Repeat-work shortcuts for the Personal edition scene setup flow.
    /// </summary>
    public static class SceneQuickAction
    {
        [VistaQuickAction(
            "vista.scene-setup.create-terrains",
            "Scene",
            "Create Terrains",
            "Create terrains and add them to a Vista Manager.",
            0,
            "default:Terrain")]
        private static void CreateTerrains(VistaQuickActionContext ctx)
        {
            if (!TryPrepareFlow(ctx, "terrain"))
                return;

            ctx.Wizard.PushPage(new CreateInScenePage());
            ctx.Wizard.PushPage(new CreateTerrainPage());
        }

        [VistaQuickAction(
            "vista.scene-setup.create-biomes",
            "Scene",
            "Create Biomes",
            "Create biomes under a Vista Manager.",
            1,
            "res:Vista/Textures/BiomeIcon")]
        private static void CreateBiomes(VistaQuickActionContext ctx)
        {
            if (!TryPrepareFlow(ctx, "biome"))
                return;

            ctx.Wizard.PushPage(new CreateInScenePage());
            ctx.Wizard.PushPage(new CreateBiomePage());
        }

        [VistaQuickAction(
            "vista.scene-setup.create-terrain-seal",
            "Scene",
            "Create Seal",
            "Create a polygonal region where work from any manual terrain tool is kept through regeneration.",
            2,
            "res:Vista/Textures/SealIcon")]
        private static void CreateTerrainSeal(VistaQuickActionContext ctx)
        {
            TerrainSealEditorUtility.Create();
            SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.TERRAIN_SEAL_CREATED);
            Snackbar.Show(
                ctx.Wizard.rootVisualElement,
                "Terrain Seal created.",
                SnackbarType.Success);
        }

        [VistaQuickAction(
            "vista.scene.generate-all-biomes",
            "Scene",
            "Generate All Biomes",
            "Force regenerate all Vista Managers in the scene.",
            3,
            "res:Vista/Textures/ForceGenerate")]
        private static void GenerateAllBiomes(VistaQuickActionContext ctx)
        {
            VistaManager[] managers = Utilities.FindObjectsNoSortCompat<VistaManager>();
            if (managers.Length == 0)
            {
                ShowInfo(ctx, "No Vista Manager was found in the scene.");
                return;
            }

            IReadOnlyList<GenerationTask> tasks = SceneBiomeService.GenerateAll(managers);
            if (tasks.Count > 0)
                SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.GENERATE_ALL);
        }

        [VistaQuickAction(
            "vista.scene.select-manager",
            "Scene",
            "Select Vista Manager",
            "Select the Vista Manager in the scene.",
            4,
            "res:Vista/Textures/VistaIcon")]
        private static void SelectVistaManager(VistaQuickActionContext ctx)
        {
            VistaManager[] managers = Utilities.FindObjectsNoSortCompat<VistaManager>();
            if (managers.Length == 0)
            {
                ShowInfo(ctx, "No Vista Manager was found in the scene.");
                return;
            }

            Selection.objects = managers;
            EditorGUIUtility.PingObject(managers[0]);
        }

        private static void ShowInfo(VistaQuickActionContext ctx, string message)
        {
            if (ctx?.Wizard != null)
                Snackbar.Show(ctx.Wizard.rootVisualElement, message, SnackbarType.Info);
            else
                Debug.Log(message);
        }

        private static void ShowWarning(VistaQuickActionContext ctx, string message)
        {
            if (ctx?.Wizard != null)
                Snackbar.Show(ctx.Wizard.rootVisualElement, message, SnackbarType.Warning);
            else
                Debug.LogWarning(message);
        }

        private static bool TryPrepareFlow(
            VistaQuickActionContext ctx,
            string workflow)
        {
            if (ctx?.Wizard == null)
            {
                Debug.LogWarning($"Cannot open the Vista {workflow} creation flow without a Wizard window.");
                return false;
            }

            foreach (VistaManager manager in VistaManager.allInstances)
            {
                if (manager != null)
                    return true;
            }

            VistaManager created = VistaManager.CreateInstanceInScene();
            if (created == null)
            {
                Snackbar.Show(
                    ctx.Wizard.rootVisualElement,
                    "Vista could not create a Manager for this workflow.",
                    SnackbarType.Error);
                return false;
            }

            Selection.activeObject = created;
            EditorSceneManager.MarkSceneDirty(created.gameObject.scene);
            Snackbar.Show(
                ctx.Wizard.rootVisualElement,
                "A Vista Manager was created automatically in the scene.",
                SnackbarType.Success);
            return true;
        }
    }
}
#endif
