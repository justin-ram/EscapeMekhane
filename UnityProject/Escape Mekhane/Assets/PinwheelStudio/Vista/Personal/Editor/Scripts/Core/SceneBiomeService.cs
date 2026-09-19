#if VISTA
using Pinwheel.Vista;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Pinwheel.VistaEditor
{
    /// <summary>
    /// Tracks the biome-to-tile state applied to open scenes during the current editor session.
    /// </summary>
    [InitializeOnLoad]
    public static class SceneBiomeService
    {
        private sealed class BiomeState
        {
            public HashSet<ITile> appliedTiles;
            public Matrix4x4 lastObservedLocalToWorld;
            public bool hasTransform;

            public BiomeState(IBiome biome)
            {
                appliedTiles = new HashSet<ITile>();
                Component component = biome as Component;
                if (component != null)
                {
                    lastObservedLocalToWorld = component.transform.localToWorldMatrix;
                    hasTransform = true;
                }
            }
        }

        private sealed class ManagerState
        {
            public Dictionary<IBiome, BiomeState> biomes;

            public ManagerState()
            {
                biomes = new Dictionary<IBiome, BiomeState>();
            }
        }

        private sealed class GenerationCommit
        {
            public VistaManager manager;
            public GenerationTask task;
            public Dictionary<IBiome, HashSet<ITile>> currentTiles;
        }

        private static readonly Dictionary<VistaManager, ManagerState> s_managerStates;
        private static readonly Dictionary<VistaManager, HashSet<IBiome>> s_pendingRegeneration;
        private static readonly List<GenerationCommit> s_generationCommits;
        private static readonly Plane[] s_sceneViewFrustumPlanes;
        private const double TRANSFORM_CHECK_INTERVAL = 0.25;
        private static bool s_refreshScheduled;
        private static bool s_regenerationScheduled;
        private static bool s_hasSceneViewCamera;
        private static double s_nextTransformCheckTime;
        private static Vector3 s_sceneViewCameraPosition;

        static SceneBiomeService()
        {
            s_managerStates = new Dictionary<VistaManager, ManagerState>();
            s_pendingRegeneration = new Dictionary<VistaManager, HashSet<IBiome>>();
            s_generationCommits = new List<GenerationCommit>();
            s_sceneViewFrustumPlanes = new Plane[6];

            EditorApplication.hierarchyChanged += () => ScheduleRefresh();
            Undo.undoRedoPerformed += () => ScheduleRefresh();
            EditorSceneManager.sceneOpened += (scene, mode) => ScheduleRefresh();
            EditorSceneManager.sceneClosed += scene => ScheduleRefresh();
            BiomeVMConnector.managerIdChanged += connector => ScheduleRefresh();
            SceneView.duringSceneGui += sceneView => RecordSceneViewCamera(sceneView);
            BiomeExtensions.changedCallback += biome => OnBiomeChanged(biome);
            EditorApplication.update += () => Update();

            ScheduleRefresh();
        }

        private static void Update()
        {
            CommitCompletedGenerations();

            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime < s_nextTransformCheckTime)
                return;

            s_nextTransformCheckTime = currentTime + TRANSFORM_CHECK_INTERVAL;
            CheckBiomeTransforms();
        }

        private static void CheckBiomeTransforms()
        {
            foreach (KeyValuePair<VistaManager, ManagerState> managerEntry in s_managerStates)
            {
                foreach (KeyValuePair<IBiome, BiomeState> biomeEntry in managerEntry.Value.biomes)
                {
                    Component component = biomeEntry.Key as Component;
                    BiomeState biomeState = biomeEntry.Value;
                    if (component == null || !biomeState.hasTransform)
                        continue;

                    Matrix4x4 currentLocalToWorld = component.transform.localToWorldMatrix;
                    if (currentLocalToWorld != biomeState.lastObservedLocalToWorld)
                    {
                        biomeState.lastObservedLocalToWorld = currentLocalToWorld;
                        OnBiomeChanged(biomeEntry.Key);
                    }
                }
            }
        }

        private static void OnBiomeChanged(IBiome biome)
        {
            if (!IsAlive(biome))
                return;

            VistaManager manager = biome.GetVistaManagerInstance();
            if (manager == null)
                return;

            if (!s_pendingRegeneration.TryGetValue(manager, out HashSet<IBiome> biomeRequests))
            {
                biomeRequests = new HashSet<IBiome>();
                s_pendingRegeneration.Add(manager, biomeRequests);
            }
            biomeRequests.Add(biome);

            if (!s_regenerationScheduled)
            {
                s_regenerationScheduled = true;
                EditorApplication.delayCall += () => GeneratePendingBiomes();
            }
        }

        private static void GeneratePendingBiomes()
        {
            s_regenerationScheduled = false;
            CommitCompletedGenerations();
            foreach (KeyValuePair<VistaManager, HashSet<IBiome>> managerEntry in s_pendingRegeneration)
            {
                VistaManager manager = managerEntry.Key;
                if (manager == null)
                    continue;

                HashSet<ITile> tiles = new HashSet<ITile>();
                Dictionary<IBiome, HashSet<ITile>> currentTiles = new Dictionary<IBiome, HashSet<ITile>>();
                foreach (IBiome biome in managerEntry.Value)
                {
                    if (!IsAlive(biome))
                        continue;
                    if (!TryGetBiomeState(manager, biome, out BiomeState biomeState))
                        continue;

                    biomeState.appliedTiles.RemoveWhere(tile => !IsAlive(tile));
                    HashSet<ITile> currentBiomeTiles = GetCurrentTiles(manager, biome);
                    currentTiles.Add(biome, currentBiomeTiles);
                    tiles.UnionWith(biomeState.appliedTiles);
                    tiles.UnionWith(currentBiomeTiles);
                }
                tiles.RemoveWhere(tile => !IsAlive(tile));
                if (tiles.Count > 0)
                {
                    ITile[] sortedTiles = new ITile[tiles.Count];
                    tiles.CopyTo(sortedTiles);
                    if (s_hasSceneViewCamera)
                    {
                        Array.Sort(sortedTiles, CompareTilesBySceneView);
                    }
                    GenerationTask task = manager.Generate(sortedTiles);
                    s_generationCommits.Add(new GenerationCommit
                    {
                        manager = manager,
                        task = task,
                        currentTiles = currentTiles
                    });
                }
            }
            s_pendingRegeneration.Clear();
        }

        private static void CommitCompletedGenerations()
        {
            for (int i = s_generationCommits.Count - 1; i >= 0; --i)
            {
                GenerationCommit commit = s_generationCommits[i];
                if (!commit.task.isCompleted)
                    continue;

                if (commit.task.status == GenerationStatus.Completed &&
                    commit.manager != null &&
                    s_managerStates.TryGetValue(commit.manager, out ManagerState managerState))
                {
                    foreach (KeyValuePair<IBiome, HashSet<ITile>> biomeEntry in commit.currentTiles)
                    {
                        if (IsAlive(biomeEntry.Key) &&
                            managerState.biomes.TryGetValue(biomeEntry.Key, out BiomeState biomeState))
                        {
                            biomeState.appliedTiles.Clear();
                            foreach (ITile tile in biomeEntry.Value)
                            {
                                if (IsAlive(tile))
                                {
                                    biomeState.appliedTiles.Add(tile);
                                }
                            }
                        }
                    }
                }
                s_generationCommits.RemoveAt(i);
            }
        }

        private static void RecordSceneViewCamera(SceneView sceneView)
        {
            Camera camera = sceneView.camera;
            if (camera == null)
                return;

            s_sceneViewCameraPosition = camera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(camera, s_sceneViewFrustumPlanes);
            s_hasSceneViewCamera = true;
        }

        private static void ScheduleRefresh()
        {
            if (s_refreshScheduled)
                return;

            s_refreshScheduled = true;
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh()
        {
            s_refreshScheduled = false;

            HashSet<VistaManager> activeManagers = new HashSet<VistaManager>();
            foreach (VistaManager manager in VistaManager.allInstances)
            {
                if (manager != null)
                {
                    activeManagers.Add(manager);
                }
            }
            List<VistaManager> removedManagers = new List<VistaManager>();
            foreach (VistaManager manager in s_managerStates.Keys)
            {
                if (manager == null || !activeManagers.Contains(manager))
                {
                    removedManagers.Add(manager);
                }
            }

            foreach (VistaManager manager in removedManagers)
            {
                s_managerStates.Remove(manager);
            }

            foreach (VistaManager manager in activeManagers)
            {
                Refresh(manager);
            }
        }

        private static void Refresh(VistaManager manager)
        {
            if (!s_managerStates.TryGetValue(manager, out ManagerState managerState))
            {
                managerState = new ManagerState();
                s_managerStates.Add(manager, managerState);
            }

            IBiome[] currentBiomes = manager.GetBiomes() ?? Array.Empty<IBiome>();
            HashSet<IBiome> currentBiomeSet = new HashSet<IBiome>();
            foreach (IBiome biome in currentBiomes)
            {
                if (IsAlive(biome))
                {
                    currentBiomeSet.Add(biome);
                }
            }
            List<IBiome> removedBiomes = new List<IBiome>();
            foreach (IBiome biome in managerState.biomes.Keys)
            {
                managerState.biomes[biome].appliedTiles.RemoveWhere(tile => !IsAlive(tile));
                if (!currentBiomeSet.Contains(biome))
                {
                    removedBiomes.Add(biome);
                }
            }

            foreach (IBiome biome in removedBiomes)
            {
                managerState.biomes.Remove(biome);
            }

            List<ITile> tiles = null;
            foreach (IBiome biome in currentBiomes)
            {
                if (!IsAlive(biome))
                    continue;
                if (managerState.biomes.ContainsKey(biome))
                    continue;

                if (tiles == null)
                {
                    tiles = manager.GetTiles();
                }

                BiomeState biomeState = new BiomeState(biome);
                foreach (ITile tile in tiles)
                {
                    if (IsAlive(tile) && biome.IsOverlap(tile.worldBounds))
                    {
                        biomeState.appliedTiles.Add(tile);
                    }
                }
                managerState.biomes.Add(biome, biomeState);
            }
        }

        internal static void GetDiscoveredBiomes(Dictionary<VistaManager, List<IBiome>> result)
        {
            result.Clear();
            foreach (KeyValuePair<VistaManager, ManagerState> managerEntry in s_managerStates)
            {
                List<IBiome> biomes = new List<IBiome>(managerEntry.Value.biomes.Keys);
                biomes.RemoveAll(biome => !IsAlive(biome));
                result.Add(managerEntry.Key, biomes);
            }
        }

        internal static void GetAppliedTiles(IBiome biome, List<ITile> result)
        {
            result.Clear();
            foreach (ManagerState managerState in s_managerStates.Values)
            {
                if (managerState.biomes.TryGetValue(biome, out BiomeState biomeState))
                {
                    foreach (ITile tile in biomeState.appliedTiles)
                    {
                        if (IsAlive(tile))
                        {
                            result.Add(tile);
                        }
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Requests generation for every live tile owned by the specified Manager.
        /// </summary>
        /// <param name="manager">The Manager whose tiles should be regenerated.</param>
        /// <param name="sortTiles">
        /// Whether tiles should be ordered by the last observed Scene view frustum and camera distance.
        /// </param>
        /// <returns>The task representing this exact all-tiles request.</returns>
        public static GenerationTask GenerateAll(VistaManager manager, bool sortTiles = true)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            if (!ConfirmGenerateAllWithInactiveSeals())
                return GenerationTask.completedTask;

            return GenerateAllUnchecked(manager, sortTiles);
        }

        /// <summary>
        /// Requests generation for every live tile owned by each supplied Manager after one shared safety check.
        /// </summary>
        /// <returns>The tasks created for the Managers, or an empty list when the user cancels.</returns>
        public static IReadOnlyList<GenerationTask> GenerateAll(IReadOnlyList<VistaManager> managers, bool sortTiles = true)
        {
            if (managers == null)
                throw new ArgumentNullException(nameof(managers));

            if (!ConfirmGenerateAllWithInactiveSeals())
                return Array.Empty<GenerationTask>();

            List<GenerationTask> tasks = new List<GenerationTask>();
            for (int i = 0; i < managers.Count; ++i)
            {
                VistaManager manager = managers[i];
                if (manager != null)
                    tasks.Add(GenerateAllUnchecked(manager, sortTiles));
            }
            return tasks;
        }

        private static GenerationTask GenerateAllUnchecked(VistaManager manager, bool sortTiles)
        {

            Refresh(manager);
            List<ITile> liveTiles = manager.GetTiles();
            liveTiles.RemoveAll(tile => !IsAlive(tile));
            ITile[] tileSnapshot = liveTiles.ToArray();
            if (sortTiles && s_hasSceneViewCamera)
            {
                Array.Sort(tileSnapshot, CompareTilesBySceneView);
            }

            GenerationTask task = manager.Generate(tileSnapshot);
            if (s_managerStates.TryGetValue(manager, out ManagerState managerState))
            {
                Dictionary<IBiome, HashSet<ITile>> currentTiles = new Dictionary<IBiome, HashSet<ITile>>();
                foreach (IBiome biome in managerState.biomes.Keys)
                {
                    if (!IsAlive(biome))
                        continue;

                    HashSet<ITile> biomeTiles = new HashSet<ITile>();
                    foreach (ITile tile in tileSnapshot)
                    {
                        if (biome.IsOverlap(tile.worldBounds))
                        {
                            biomeTiles.Add(tile);
                        }
                    }
                    currentTiles.Add(biome, biomeTiles);
                }
                s_generationCommits.Add(new GenerationCommit
                {
                    manager = manager,
                    task = task,
                    currentTiles = currentTiles
                });
            }
            return task;
        }

        private static bool ConfirmGenerateAllWithInactiveSeals()
        {
            TerrainSeal[] seals = Resources.FindObjectsOfTypeAll<TerrainSeal>();
            for (int i = 0; i < seals.Length; ++i)
            {
                TerrainSeal seal = seals[i];
                if (seal == null ||
                    EditorUtility.IsPersistent(seal) ||
                    !seal.gameObject.scene.IsValid() ||
                    !seal.gameObject.scene.isLoaded ||
                    (seal.gameObject.activeInHierarchy && seal.enabled))
                    continue;

                return EditorUtility.DisplayDialog(
                    "Inactive or Disabled Terrain Seal",
                    "One or more Terrain Seals are inactive in the hierarchy or have a disabled component. Generate All may overwrite manual terrain edits that those Seals normally protect. Continue?",
                    "Generate Anyway",
                    "Cancel");
            }
            return true;
        }

        /// <summary>
        /// Gets the smallest known tile set that must be regenerated after the specified biome changes.
        /// </summary>
        /// <param name="biome">The biome requesting regeneration.</param>
        /// <returns>
        /// A snapshot containing tiles affected by the biome's last recorded state together with tiles overlapped by its
        /// current state. An empty array is returned when the biome has no associated Manager or is not discoverable by it.
        /// </returns>
        /// <remarks>This method does not update the recorded applied-tile set.</remarks>
        public static ITile[] GetTilesToRegenerate(IBiome biome)
        {
            if (biome == null)
                throw new ArgumentNullException(nameof(biome));

            VistaManager manager = biome.GetVistaManagerInstance();
            if (manager == null)
                return Array.Empty<ITile>();

            if (!TryGetBiomeState(manager, biome, out BiomeState biomeState))
                return Array.Empty<ITile>();

            HashSet<ITile> tilesToRegenerate = new HashSet<ITile>(biomeState.appliedTiles);
            tilesToRegenerate.RemoveWhere(tile => !IsAlive(tile));
            tilesToRegenerate.UnionWith(GetCurrentTiles(manager, biome));

            ITile[] result = new ITile[tilesToRegenerate.Count];
            tilesToRegenerate.CopyTo(result);
            if (s_hasSceneViewCamera)
            {
                Array.Sort(result, CompareTilesBySceneView);
            }
            return result;
        }

        private static bool TryGetBiomeState(VistaManager manager, IBiome biome, out BiomeState biomeState)
        {
            if (!s_managerStates.TryGetValue(manager, out ManagerState managerState) ||
                !managerState.biomes.TryGetValue(biome, out biomeState))
            {
                Refresh(manager);
                if (!s_managerStates.TryGetValue(manager, out managerState) ||
                    !managerState.biomes.TryGetValue(biome, out biomeState))
                {
                    biomeState = null;
                    return false;
                }
            }
            return true;
        }

        private static HashSet<ITile> GetCurrentTiles(VistaManager manager, IBiome biome)
        {
            HashSet<ITile> currentTiles = new HashSet<ITile>();
            foreach (ITile tile in manager.GetTiles())
            {
                if (IsAlive(tile) && biome.IsOverlap(tile.worldBounds))
                {
                    currentTiles.Add(tile);
                }
            }
            return currentTiles;
        }

        private static bool IsAlive(object value)
        {
            if (value == null)
                return false;
            if (value is Object unityObject)
                return unityObject != null;
            return true;
        }

        private static int CompareTilesBySceneView(ITile tile0, ITile tile1)
        {
            bool isAlive0 = IsAlive(tile0);
            bool isAlive1 = IsAlive(tile1);
            if (isAlive0 != isAlive1)
            {
                return isAlive0 ? -1 : 1;
            }
            if (!isAlive0)
                return 0;

            Bounds bounds0 = tile0.worldBounds;
            Bounds bounds1 = tile1.worldBounds;
            bool isVisible0 = GeometryUtility.TestPlanesAABB(s_sceneViewFrustumPlanes, bounds0);
            bool isVisible1 = GeometryUtility.TestPlanesAABB(s_sceneViewFrustumPlanes, bounds1);
            if (isVisible0 != isVisible1)
            {
                return isVisible0 ? -1 : 1;
            }

            float distance0 = bounds0.SqrDistance(s_sceneViewCameraPosition);
            float distance1 = bounds1.SqrDistance(s_sceneViewCameraPosition);
            return distance0.CompareTo(distance1);
        }
    }

    public class SceneBiomeServiceViewer : EditorWindow
    {
        private Dictionary<VistaManager, List<IBiome>> m_biomes;
        private List<ITile> m_appliedTiles;
        private IBiome m_selectedBiome;
        private Vector2 m_scrollPosition;
        private Action<SceneView> m_drawSceneBounds;

        //[MenuItem("Window/Vista/Diagnostics/Scene Biomes")]
        private static void Open()
        {
            GetWindow<SceneBiomeServiceViewer>("Scene Biomes");
        }

        private void OnEnable()
        {
            m_biomes = new Dictionary<VistaManager, List<IBiome>>();
            m_appliedTiles = new List<ITile>();
            m_drawSceneBounds = sceneView => DrawSceneBounds();
            SceneView.duringSceneGui += m_drawSceneBounds;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= m_drawSceneBounds;
        }

        private void OnInspectorUpdate()
        {
            Repaint();
            SceneView.RepaintAll();
        }

        private void OnGUI()
        {
            SceneBiomeService.GetDiscoveredBiomes(m_biomes);

            int biomeCount = 0;
            bool selectedBiomeFound = false;
            foreach (List<IBiome> biomes in m_biomes.Values)
            {
                biomeCount += biomes.Count;
                selectedBiomeFound |= biomes.Contains(m_selectedBiome);
            }
            if (!selectedBiomeFound)
            {
                m_selectedBiome = null;
            }

            EditorGUILayout.LabelField($"Managers: {m_biomes.Count}    Biomes: {biomeCount}", EditorStyles.boldLabel);
            m_scrollPosition = EditorGUILayout.BeginScrollView(m_scrollPosition);
            foreach (KeyValuePair<VistaManager, List<IBiome>> managerEntry in m_biomes)
            {
                EditorGUILayout.ObjectField(managerEntry.Key, typeof(VistaManager), true);
                EditorGUI.indentLevel += 1;
                foreach (IBiome biome in managerEntry.Value)
                {
                    Object biomeObject = biome as Object;
                    string label = biomeObject != null ? $"{biomeObject.name} ({biome.GetType().Name})" : "Missing Biome";
                    bool isSelected = ReferenceEquals(m_selectedBiome, biome);
                    if (GUILayout.Toggle(isSelected, label, "Button") != isSelected)
                    {
                        m_selectedBiome = biome;
                        Selection.activeObject = biomeObject;
                        SceneView.RepaintAll();
                    }
                }
                EditorGUI.indentLevel -= 1;
                EditorGUILayout.Space();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawSceneBounds()
        {
            Color previousColor = Handles.color;
            if (m_selectedBiome != null)
            {
                SceneBiomeService.GetAppliedTiles(m_selectedBiome, m_appliedTiles);
                Handles.color = Color.yellow;
                foreach (ITile tile in m_appliedTiles)
                {
                    Object tileObject = tile as Object;
                    if (tileObject != null)
                    {
                        Bounds bounds = tile.worldBounds;
                        Handles.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }

            Handles.color = Color.green;
            foreach (VistaManager manager in VistaManager.allInstances)
            {
                ITile tile = manager.currentlyProcessingTile;
                Object tileObject = tile as Object;
                if (tileObject != null)
                {
                    Bounds bounds = tile.worldBounds;
                    Handles.DrawWireCube(bounds.center, bounds.size);
                }
            }
            Handles.color = previousColor;
        }
    }
}
#endif
