#if VISTA
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Pinwheel.Vista;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Editor-side orchestration for creating a terrain grid through a runtime terrain backend.
    /// </summary>
    internal static class TerrainGridFactory
    {
        internal sealed class Result
        {
            public bool success { get; }
            public string message { get; }
            public int tileCount { get; }
            public string holderName { get; }

            public Result(bool success, string message, int tileCount, string holderName)
            {
                this.success = success;
                this.message = message;
                this.tileCount = tileCount;
                this.holderName = holderName;
            }

            public static Result Failure(string message)
            {
                return new Result(false, message, 0, null);
            }

            public static Result Success(string message, int tileCount, string holderName)
            {
                return new Result(true, message, tileCount, holderName);
            }
        }

        public static Result TryCreate(VistaManager manager, ITerrainSystem system, TerrainGridConfig config)
        {
            if (manager == null)
            {
                return Result.Failure("The Vista Manager is gone, cannot create terrain.");
            }
            if (system == null)
            {
                return Result.Failure("No terrain system is registered, cannot create terrain.");
            }
            if (!EnsureSceneSaved(manager))
            {
                return Result.Failure("Terrain creation was cancelled because the scene was not saved.");
            }

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Vista Terrain");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject holder = new GameObject("Terrains");
            Undo.RegisterCreatedObjectUndo(holder, "Create Terrains");
            SceneManager.MoveGameObjectToScene(holder, manager.gameObject.scene);
            holder.transform.position = Vector3.zero;
            holder.transform.rotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            TerrainGridCreationContext context = new TerrainGridCreationContext(
                new Vector2Int(config.columns, config.rows),
                new Vector3(config.widthLength, config.height, config.widthLength),
                holder.transform);
            GameObject[,] grid = system.CreateTerrainGrid(context);

            List<ITile> tiles = new List<ITile>();
            foreach (GameObject tile in grid)
            {
                if (tile == null)
                {
                    continue;
                }

                Undo.RegisterCreatedObjectUndo(tile, "Create Vista Terrain");
                ITile setupTile = system.SetupTile(manager, tile);
                if (setupTile != null)
                {
                    tiles.Add(setupTile);
                }
            }

            SaveTileData(manager, tiles);

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeGameObject = holder;

            return Result.Success(
                string.Format("Created {0} terrain tile(s) under \"{1}\".", tiles.Count, holder.name),
                tiles.Count,
                holder.name);
        }

        private static bool EnsureSceneSaved(VistaManager manager)
        {
            Scene scene = manager.gameObject.scene;
            if (!string.IsNullOrEmpty(scene.path))
            {
                return true;
            }

            bool save = EditorUtility.DisplayDialog(
                "Save Scene",
                "Vista saves the terrain data next to your scene, so the scene needs to be saved first.",
                "Save Scene",
                "Cancel");
            if (!save)
            {
                return false;
            }

            EditorSceneManager.SaveScene(scene);
            return !string.IsNullOrEmpty(scene.path);
        }

        private static void SaveTileData(VistaManager manager, IEnumerable<ITile> tiles)
        {
            string folder = GetOrCreateDataFolder(manager);
            HashSet<Object> saved = new HashSet<Object>();

            foreach (ITile tile in tiles)
            {
                foreach (Object data in tile.GetDataAssets())
                {
                    if (data == null || EditorUtility.IsPersistent(data) || !saved.Add(data))
                    {
                        continue;
                    }

                    string extension = data is Material ? ".mat" : ".asset";
                    string baseName = string.Format("{0}_{1}", tile.gameObject.name, data.GetType().Name);
                    string path = AssetDatabase.GenerateUniqueAssetPath(string.Format("{0}/{1}{2}", folder, baseName, extension));
                    AssetDatabase.CreateAsset(data, path);
                }
            }

            if (saved.Count > 0)
            {
                AssetDatabase.SaveAssets();
            }
        }

        private static string GetOrCreateDataFolder(VistaManager manager)
        {
            string scenePath = manager.gameObject.scene.path;
            string sceneFolder = Path.GetDirectoryName(scenePath).Replace('\\', '/');
            string sceneDataFolderName = Path.GetFileNameWithoutExtension(scenePath);
            string sceneDataFolder = string.Format("{0}/{1}", sceneFolder, sceneDataFolderName);
            if (!AssetDatabase.IsValidFolder(sceneDataFolder))
            {
                AssetDatabase.CreateFolder(sceneFolder, sceneDataFolderName);
            }

            string terrainFolder = string.Format("{0}/Terrains", sceneDataFolder);
            if (!AssetDatabase.IsValidFolder(terrainFolder))
            {
                AssetDatabase.CreateFolder(sceneDataFolder, "Terrains");
            }
            return terrainFolder;
        }
    }
}
#endif
