#if VISTA
using System.Collections.Generic;
using System.IO;
using System;
using Pinwheel.Vista;
using Pinwheel.VistaEditor.Wizard;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Pinwheel.Vista.Graph;

namespace Pinwheel.VistaEditor
{
    /// <summary>
    /// Editor-side helpers for the biome template gallery: <b>discovery</b> of valid <see cref="BiomeTemplate"/>
    /// assets, and <b>creation</b> of a biome from a template. Creation is the editor <b>shell</b> around the
    /// runtime <see cref="BiomeTemplateSpawner"/>: it runs the runtime spawn, then persists the in-memory clones
    /// the biome registered, wraps the whole thing in one Undo group, selects the result, and records it in
    /// Recent Biomes. Mirrors <c>TerrainGridFactory</c>. The former "blank" biome is now itself a
    /// <see cref="BiomeTemplate"/>, so there is a single create path, not a special case.
    /// </summary>
    public static class BiomeTemplateUtils
    {
        public sealed class Result
        {
            public bool success { get; }
            public string message { get; }
            /// <summary>The root of the spawned hierarchy; it covers the whole spawn, single biome or a group.</summary>
            public GameObject spawnedRoot { get; }
            /// <summary>All spawned biomes in hierarchy order. A template may carry several under a plain root.</summary>
            public IReadOnlyList<IBiome> biomes { get; }

            private Result(bool success, string message, GameObject spawnedRoot, IReadOnlyList<IBiome> biomes)
            {
                this.success = success;
                this.message = message;
                this.spawnedRoot = spawnedRoot;
                this.biomes = biomes ?? new List<IBiome>();
            }

            public static Result Failure(string message)
            {
                return new Result(false, message, null, null);
            }

            public static Result Success(string message, GameObject spawnedRoot, IReadOnlyList<IBiome> biomes)
            {
                return new Result(true, message, spawnedRoot, biomes);
            }
        }

        /// <summary>
        /// All valid <see cref="BiomeTemplate"/> assets in the project. Invalid ones are filtered out silently,
        /// the same shape as Recent Biomes pruning dead entries before rendering. This load-time filter is the
        /// second validation layer, alongside the author-time warning on the asset itself, so the gallery can
        /// never show or spawn a broken template (its prefab was deleted, or a required component removed, after
        /// authoring).
        /// </summary>
        public static List<BiomeTemplate> GetValid()
        {
            List<BiomeTemplate> result = new List<BiomeTemplate>();

            foreach (BiomeTemplate template in GetAllTemplates())
            {
                if (template != null && template.IsValid(out _))
                {
                    result.Add(template);
                }
            }

            result.Sort(CompareTemplates);

            return result;
        }

        /// <summary>
        /// Returns the template that uses <paramref name="graph"/> as its source terrain graph, if any.
        /// Template-ness is a usage role, not a graph subtype: a graph counts as a template graph only when a
        /// biome template's prefab references it through an <see cref="IProceduralBiome"/>.
        /// </summary>
        public static bool TryGetTemplateUsingGraph(TerrainGraph graph, out BiomeTemplate template)
        {
            template = null;
            if (graph == null)
            {
                return false;
            }

            foreach (BiomeTemplate candidate in GetAllTemplates())
            {
                if (candidate == null || candidate.biomePrefab == null)
                {
                    continue;
                }

                // Search the whole prefab hierarchy: a multi biome template holds its biomes under a plain root,
                // and several of them may reference the same source graph.
                foreach (IProceduralBiome proceduralBiome in candidate.biomePrefab.GetComponentsInChildren<IProceduralBiome>(true))
                {
                    if (proceduralBiome.terrainGraph == graph)
                    {
                        template = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        private static int CompareTemplates(BiomeTemplate a, BiomeTemplate b)
        {
            int orderCompare = a.sortingNumber.CompareTo(b.sortingNumber);
            if (orderCompare != 0)
            {
                return orderCompare;
            }

            string aTitle = GetTemplateSortLabel(a);
            string bTitle = GetTemplateSortLabel(b);
            return string.Compare(aTitle, bTitle, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTemplateSortLabel(BiomeTemplate template)
        {
            if (template == null)
            {
                return string.Empty;
            }

            if (template.info != null && !string.IsNullOrEmpty(template.info.title))
            {
                return template.info.title;
            }

            return template.name;
        }

        private static IEnumerable<BiomeTemplate> GetAllTemplates()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(BiomeTemplate)))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                BiomeTemplate template = AssetDatabase.LoadAssetAtPath<BiomeTemplate>(path);
                if (template != null)
                {
                    yield return template;
                }
            }
        }

        /// <summary>Spawn a biome from a template under the manager, persisting its owned clones beside the scene.</summary>
        public static Result CreateFromTemplate(VistaManager manager, BiomeTemplate template)
        {
            if (manager == null)
            {
                return Result.Failure("The Vista Manager is gone, cannot create the biome.");
            }
            if (template == null)
            {
                return Result.Failure("No biome template was given.");
            }
            if (!template.IsValid(out string templateError))
            {
                return Result.Failure(string.Format("The biome template cannot be used: {0}", templateError));
            }
            if (!EnsureSceneSaved(manager))
            {
                return Result.Failure("Biome creation was cancelled because the scene was not saved.");
            }

            int undoGroup = BeginUndo();

            BiomeTemplateSpawnContext context = new BiomeTemplateSpawnContext(manager);
            GameObject spawnedRoot = BiomeTemplateSpawner.Spawn(template, context);

            // The root may be a plain GameObject holding several biomes (a multi biome template), so the root is
            // the undo and selection handle, while the biome list feeds count-aware messaging and the result the
            // wizard reads.
            List<IBiome> biomes = new List<IBiome>(spawnedRoot.GetComponentsInChildren<IBiome>(true));
            if (biomes.Count == 0)
            {
                return Result.Failure("Failed to spawn the biome from the template.");
            }

            Undo.RegisterCreatedObjectUndo(spawnedRoot, "Create Vista Biome");
            PersistOwnedObjects(manager, spawnedRoot, context.ownedObjects);

            string message = biomes.Count == 1
                ? string.Format("Created biome \"{0}\"", spawnedRoot.name)
                : string.Format("Created {0} biomes under \"{1}\"", biomes.Count, spawnedRoot.name);
            return Finish(manager, spawnedRoot, biomes, undoGroup, message);
        }

        // Vista saves biome-owned data (the graph) beside the scene, so an untitled scene cannot proceed.
        private static bool EnsureSceneSaved(VistaManager manager)
        {
            Scene scene = manager.gameObject.scene;
            if (!string.IsNullOrEmpty(scene.path))
            {
                return true;
            }

            bool save = EditorUtility.DisplayDialog(
                "Save Scene",
                "Vista saves the biome data next to your scene, so the scene needs to be saved first.",
                "Save Scene",
                "Cancel");
            if (!save)
            {
                return false;
            }

            EditorSceneManager.SaveScene(scene);
            return !string.IsNullOrEmpty(scene.path);
        }

        private static int BeginUndo()
        {
            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Create Vista Biome");
            return Undo.GetCurrentGroup();
        }

        // Collapse the undo group, dirty the scene, select the spawned root (it covers the whole spawn, biome or
        // multi biome), and record the first biome in the recents list. One recents entry per spawn: the group
        // was created as one gesture, so recents should not flood with every sibling.
        private static Result Finish(VistaManager manager, GameObject spawnedRoot, List<IBiome> biomes, int undoGroup, string message)
        {
            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeGameObject = spawnedRoot;
            RecentBiomes.Record(biomes[0]);

            return Result.Success(message, spawnedRoot, biomes);
        }

        // Persist the in-memory objects the receivers registered (or that we created), beside the scene. Mirrors
        // TerrainGridFactory.SaveTileData: Material as .mat, everything else as .asset, skipping null,
        // already-persistent (shared library assets), and duplicates. CreateAsset makes the existing in-memory
        // object persistent in place, so the owners' references stay valid.
        private static void PersistOwnedObjects(VistaManager manager, GameObject spawnedRoot, IEnumerable<Object> owned)
        {
            string folder = GetOrCreateDataFolder(manager);
            HashSet<Object> saved = new HashSet<Object>();

            foreach (Object data in owned)
            {
                if (data == null || EditorUtility.IsPersistent(data) || !saved.Add(data))
                {
                    continue;
                }

                string extension = data is Material ? ".mat" : ".asset";
                string baseName = string.IsNullOrEmpty(data.name)
                    ? string.Format("{0}_{1}", spawnedRoot.name, data.GetType().Name)
                    : data.name;
                string path = AssetDatabase.GenerateUniqueAssetPath(string.Format("{0}/{1}{2}", folder, baseName, extension));
                AssetDatabase.CreateAsset(data, path);
            }

            if (saved.Count > 0)
            {
                AssetDatabase.SaveAssets();
            }
        }

        // A "Biomes" folder inside the scene's data folder, created if needed. Parallels the terrain flow's
        // "Terrains" folder.
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

            string biomeFolder = string.Format("{0}/Biomes", sceneDataFolder);
            if (!AssetDatabase.IsValidFolder(biomeFolder))
            {
                AssetDatabase.CreateFolder(sceneDataFolder, "Biomes");
            }
            return biomeFolder;
        }
    }
}
#endif
