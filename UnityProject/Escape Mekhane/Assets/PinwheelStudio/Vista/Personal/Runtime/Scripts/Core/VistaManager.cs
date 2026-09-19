#if VISTA
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using Pinwheel.Vista.Diagnostics;
using Pinwheel.Vista.Graph;


namespace Pinwheel.Vista
{
    [AddComponentMenu("Vista/Vista Manager")]
    [HelpURL("https://docs.pinwheelstud.io/vista/docs/vista-manager-component-overview.html")]
    [ExecuteInEditMode]
    /// <summary>
    /// Coordinates biome evaluation, data blending, and tile population for a Vista scene.
    /// </summary>
    /// <remarks>
    /// The manager is the runtime entry point for terrain generation. It discovers tiles and biomes, requests
    /// <see cref="BiomeData"/> for each relevant tile, optionally blends overlapping biome results, then forwards the
    /// resulting outputs to the tile interfaces implemented by the active terrain system. Generation runs progressively
    /// through a coroutine-backed <see cref="GenerationTask"/>. Each manager serializes its active and newest pending request.
    /// </remarks>
    public partial class VistaManager : MonoBehaviour
    {
        private const string EDIT_INTENT = "Vista Manager";
        protected static HashSet<VistaManager> s_allInstances = new HashSet<VistaManager>();
        /// <summary>
        /// Gets the currently enabled manager instances tracked by the runtime.
        /// </summary>
        /// <remarks>
        /// Managers register in <c>OnEnable</c> and unregister in <c>OnDisable</c>.
        /// </remarks>
        public static IEnumerable<VistaManager> allInstances
        {
            get
            {
                return s_allInstances;
            }
        }

        /// <summary>
        /// Represents a callback that contributes tiles owned by a manager.
        /// </summary>
        /// <param name="sender">The manager requesting tile discovery.</param>
        /// <param name="tiles">The collector that should receive discovered tiles.</param>
        public delegate void CollectTilesHandler(VistaManager sender, Collector<ITile> tiles);
        /// <summary>
        /// Occurs when a manager needs tile providers to register their tiles for generation.
        /// </summary>
        public static event CollectTilesHandler collectTiles;

        internal delegate void CollectBiomesHandler(VistaManager sender, Collector<IBiome> biomes);
        internal static CollectBiomesHandler collectFreeBiomes;

        /// <summary>
        /// Represents a callback raised at the start or end of the manager generation pipeline.
        /// </summary>
        /// <param name="sender">The manager whose pipeline is transitioning state.</param>
        public delegate void GeneratePipelineHandler(VistaManager sender);
        /// <summary>
        /// Occurs immediately before biome requests begin for a generation pass.
        /// </summary>
        public static event GeneratePipelineHandler beforeGenerating;
        /// <summary>
        /// Occurs after all requested tiles have been populated and geometry seams have been matched successfully.
        /// </summary>
        /// <remarks>This event is not raised when generation is cancelled.</remarks>
        public static event GeneratePipelineHandler afterGenerating;

        [SerializeField]
        private UnityEvent m_beforeGeneratingUnityCallback;
        /// <summary>
        /// Gets the UnityEvent invoked immediately before a generation pass begins.
        /// </summary>
        public UnityEvent beforeGeneratingUnityCallback
        {
            get
            {
                return m_beforeGeneratingUnityCallback;
            }
        }

        [SerializeField]
        private UnityEvent m_afterGeneratingUnityCallback;
        /// <summary>
        /// Gets the UnityEvent invoked after a generation pass finishes successfully.
        /// </summary>
        public UnityEvent afterGeneratingUnityCallback
        {
            get
            {
                return m_afterGeneratingUnityCallback;
            }
        }

        /// <summary>
        /// Represents a callback raised after a single texture output has been pushed into a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the texture.</param>
        /// <param name="texture">The generated texture that was just applied.</param>
        public delegate void TexturePopulatedHandler(VistaManager sender, ITile tile, RenderTexture texture);
        /// <summary>
        /// Occurs after a tile height map has been populated.
        /// </summary>
        public static event TexturePopulatedHandler heightMapPopulated;
        /// <summary>
        /// Occurs after a tile hole map has been populated.
        /// </summary>
        public static event TexturePopulatedHandler holeMapPopulated;
        /// <summary>
        /// Occurs after a tile mesh-density map has been populated.
        /// </summary>
        public static event TexturePopulatedHandler meshDensityMapPopulated;
        /// <summary>
        /// Occurs after a tile albedo map has been populated.
        /// </summary>
        public static event TexturePopulatedHandler albedoMapPopulated;
        /// <summary>
        /// Occurs after a tile metallic map has been populated.
        /// </summary>
        public static event TexturePopulatedHandler metallicMapPopulated;

        /// <summary>
        /// Represents a callback raised after terrain layer weights have been applied to a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the layer weights.</param>
        /// <param name="layers">The terrain layers paired with <paramref name="weights"/> by index.</param>
        /// <param name="weights">The weight textures applied for each layer.</param>
        public delegate void LayerWeightPopulatedHandler(VistaManager sender, ITile tile, List<TerrainLayer> layers, List<RenderTexture> weights);
        /// <summary>
        /// Occurs after terrain layer weights have been populated for a tile.
        /// </summary>
        public static event LayerWeightPopulatedHandler layerWeightPopulated;

        /// <summary>
        /// Represents a callback raised after tree samples have been populated into a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the tree data.</param>
        /// <param name="treeTemplates">Tree templates paired with <paramref name="treeBuffers"/> by index.</param>
        /// <param name="treeBuffers">Generated tree sample buffers for the tile.</param>
        public delegate void TreePopulatedHandler(VistaManager sender, ITile tile, List<TreeTemplate> treeTemplates, List<ComputeBuffer> treeBuffers);
        /// <summary>
        /// Occurs after tree data has been populated for a tile.
        /// </summary>
        public static event TreePopulatedHandler treePopulated;

        /// <summary>
        /// Represents a callback raised after detail density maps have been applied to a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the detail density data.</param>
        /// <param name="detailTemplates">Detail templates paired with <paramref name="densityMaps"/> by index.</param>
        /// <param name="densityMaps">Generated detail density maps for the tile.</param>
        public delegate void DetailDensityPopulatedHandler(VistaManager sender, ITile tile, List<DetailTemplate> detailTemplates, List<RenderTexture> densityMaps);
        /// <summary>
        /// Occurs after detail density data has been populated for a tile.
        /// </summary>
        public static event DetailDensityPopulatedHandler detailDensityPopulated;

        /// <summary>
        /// Represents a callback raised after detail instance buffers have been applied to a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the detail instance data.</param>
        /// <param name="detailTemplates">Detail templates paired with <paramref name="detailBuffers"/> by index.</param>
        /// <param name="detailBuffers">Generated detail instance buffers for the tile.</param>
        public delegate void DetailInstancePopulatedHandler(VistaManager sender, ITile tile, List<DetailTemplate> detailTemplates, List<ComputeBuffer> detailBuffers);
        /// <summary>
        /// Occurs after detail instance data has been populated for a tile.
        /// </summary>
        public static event DetailInstancePopulatedHandler detailInstancePopulated;

        /// <summary>
        /// Represents a callback raised after object instance data has been populated for a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the object data.</param>
        /// <param name="objectTemplates">Object templates paired with <paramref name="objectBuffers"/> by index.</param>
        /// <param name="objectBuffers">Generated object instance buffers for the tile.</param>
        public delegate void ObjectPopulatedHandler(VistaManager sender, ITile tile, List<ObjectTemplate> objectTemplates, List<ComputeBuffer> objectBuffers);
        /// <summary>
        /// Occurs after object data has been populated for a tile.
        /// </summary>
        public static event ObjectPopulatedHandler objectPopulated;

        /// <summary>
        /// Represents a callback raised after generic texture outputs have been applied to a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the generic textures.</param>
        /// <param name="labels">Labels paired with <paramref name="textures"/> by index.</param>
        /// <param name="textures">Generic texture outputs for the tile.</param>
        public delegate void GenericTexturePopulatedHandler(VistaManager sender, ITile tile, List<string> labels, List<RenderTexture> textures);
        /// <summary>
        /// Occurs after generic texture outputs have been populated for a tile.
        /// </summary>
        public static event GenericTexturePopulatedHandler genericTexturesPopulated;

        /// <summary>
        /// Represents a callback raised after generic buffer outputs have been applied to a tile.
        /// </summary>
        /// <param name="sender">The manager performing the population step.</param>
        /// <param name="tile">The tile that received the generic buffers.</param>
        /// <param name="labels">Labels paired with <paramref name="buffers"/> by index.</param>
        /// <param name="buffers">Generic buffer outputs for the tile.</param>
        public delegate void GenericBufferPopulatedHandler(VistaManager sender, ITile tile, List<string> labels, List<ComputeBuffer> buffers);
        /// <summary>
        /// Occurs after generic buffer outputs have been populated for a tile.
        /// </summary>
        public static event GenericBufferPopulatedHandler genericBuffersPopulated;

        internal static event Func<VistaManager, IBiome[]> getBiomesCallback;

        internal delegate BiomeData BiomeDataBlendHandler(List<BiomeBlendConfig> configs, List<BiomeData> srcDatas);
        internal static event BiomeDataBlendHandler blendBiomeDataCallback;

        internal event Action drawGizmosSelectedCallback;

        protected static List<ITerrainSystem> s_terrainSystems;
        /// <summary>
        /// Gets the tile currently being processed by the active generation pass.
        /// </summary>
        /// <remarks>
        /// The value changes as the manager iterates through overlapped tiles and is cleared when generation finishes.
        /// </remarks>
        public ITile currentlyProcessingTile { get; protected set; }

        private sealed class GenerationRequest
        {
            public ITile[] tiles;
            public GenerationTask task;
        }

        private GenerationRequest m_activeRequest;
        private CancellationHandle m_activeCancellation;
        private bool m_activeCancellationObserved;
        private GenerationRequest m_pendingRequest;
        private bool m_isGenerationCoordinatorRunning;

        /// <summary>
        /// Gets whether this manager has an active request or a pending replacement request.
        /// </summary>
        /// <remarks>
        /// This remains <see langword="true"/> while active work is cooperatively unwinding. Use the task returned by
        /// <see cref="CancelGeneration"/> when waiting for one specific active request to finish cleanup.
        /// </remarks>
        public bool isGenerating => m_activeRequest != null || m_pendingRequest != null;

        /// <summary>
        /// Gets the task currently running or cooperatively unwinding.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null"/> when no request is active. A pending replacement is exposed separately through
        /// <see cref="pendingGeneration"/>.
        /// </remarks>
        public GenerationTask activeGeneration => m_activeRequest != null ? m_activeRequest.task : null;

        /// <summary>
        /// Gets the newest request waiting for active generation to finish.
        /// </summary>
        /// <remarks>
        /// Returns <see langword="null"/> when no replacement is pending. A newer call to <see cref="Generate"/> cancels
        /// this task and replaces its authoritative tile snapshot.
        /// </remarks>
        public GenerationTask pendingGeneration => m_pendingRequest != null ? m_pendingRequest.task : null;

        /// <summary>
        /// Carries manager-level options used by object populators during one generation pass.
        /// </summary>
        public struct ObjectPopulateArgs
        {
            /// <summary>
            /// Gets or sets the maximum number of objects a progressive populator should spawn per frame.
            /// </summary>
            public int objectsPerFrame { get; set; }
        }

        [SerializeField]
        protected string m_id;
        /// <summary>
        /// Gets the persistent identifier of this manager instance.
        /// </summary>
        public string id
        {
            get
            {
                return m_id;
            }
        }

        [SerializeField]
        protected float m_terrainMaxHeight;
        /// <summary>
        /// Gets or sets the maximum terrain height assigned to tiles before population begins.
        /// </summary>
        /// <remarks>
        /// Values below zero are clamped to zero.
        /// </remarks>
        public float terrainMaxHeight
        {
            get
            {
                return m_terrainMaxHeight;
            }
            set
            {
                m_terrainMaxHeight = Mathf.Max(0, value);
            }
        }

        [SerializeField]
        protected int m_heightMapResolution;
        /// <summary>
        /// Gets or sets the height-map resolution pushed into tiles and biome requests.
        /// </summary>
        /// <remarks>
        /// The value is normalized to a valid Unity-style height-map resolution by taking the closest power of two, adding
        /// one, and clamping to Vista's supported height-map range.
        /// </remarks>
        public int heightMapResolution
        {
            get
            {
                return m_heightMapResolution;
            }
            set
            {
                m_heightMapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(value) + 1, Constants.HM_RES_MIN, Constants.HM_RES_MAX);
            }
        }

        [SerializeField]
        protected int m_textureResolution;
        /// <summary>
        /// Gets or sets the texture resolution used for non-height texture outputs.
        /// </summary>
        /// <remarks>
        /// The value is normalized to a power of two and clamped to Vista's supported range.
        /// </remarks>
        public int textureResolution
        {
            get
            {
                return m_textureResolution;
            }
            set
            {
                m_textureResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(value), Constants.HM_RES_MIN, Constants.HM_RES_MAX);
            }
        }

        [SerializeField]
        protected int m_detailDensityMapResolution;
        /// <summary>
        /// Gets or sets the resolution assigned to detail density maps before tile population.
        /// </summary>
        /// <remarks>
        /// The value is normalized to a power of two and clamped to Vista's supported generic-resolution range.
        /// </remarks>
        public int detailDensityMapResolution
        {
            get
            {
                return m_detailDensityMapResolution;
            }
            set
            {
                m_detailDensityMapResolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(value), Constants.RES_MIN, Constants.RES_MAX);
            }
        }

        [SerializeField]
        protected bool m_shouldCullBiomes;
        /// <summary>
        /// Gets or sets whether the manager should skip biome requests for tile-biome pairs that do not overlap.
        /// </summary>
        /// <remarks>
        /// When enabled, the precomputed overlap test is used to avoid requesting data from biomes that cannot contribute to
        /// the current tile.
        /// </remarks>
        public bool shouldCullBiomes
        {
            get
            {
                return m_shouldCullBiomes;
            }
            set
            {
                m_shouldCullBiomes = value;
            }
        }

        [SerializeField]
        protected MissingOutputAction m_missingGeometryAction;
        /// <summary>
        /// Gets or sets how geometry channels should behave when the current generation pass does not produce them.
        /// </summary>
        public MissingOutputAction missingGeometryAction
        {
            get
            {
                return m_missingGeometryAction;
            }
            set
            {
                m_missingGeometryAction = value;
            }
        }

        [SerializeField]
        protected MissingOutputAction m_missingTextureAction;
        /// <summary>
        /// Gets or sets how texture channels should behave when the current generation pass does not produce them.
        /// </summary>
        public MissingOutputAction missingTextureAction
        {
            get
            {
                return m_missingTextureAction;
            }
            set
            {
                m_missingTextureAction = value;
            }
        }

        [SerializeField]
        protected MissingOutputAction m_missingPopulationAction;
        /// <summary>
        /// Gets or sets how population channels should behave when the current generation pass does not produce them.
        /// </summary>
        public MissingOutputAction missingPopulationAction
        {
            get
            {
                return m_missingPopulationAction;
            }
            set
            {
                m_missingPopulationAction = value;
            }
        }

        [SerializeField]
        protected int m_objectToSpawnPerFrame;
        /// <summary>
        /// Gets or sets the object spawn budget forwarded to progressive object populators.
        /// </summary>
        /// <remarks>
        /// Values below one are clamped to one.
        /// </remarks>
        public int objectToSpawnPerFrame
        {
            get
            {
                return m_objectToSpawnPerFrame;
            }
            set
            {
                m_objectToSpawnPerFrame = Mathf.Max(1, value);
            }
        }

        /// <summary>
        /// Registers a terrain-system implementation with the global Vista runtime.
        /// </summary>
        /// <returns>No value is returned.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when a terrain system of type <typeparamref name="T"/> has already been registered.
        /// </exception>
        public static void RegisterTerrainSystem<T>() where T : class, ITerrainSystem, new()
        {
            if (s_terrainSystems == null)
            {
                s_terrainSystems = new List<ITerrainSystem>();
            }
            if (s_terrainSystems.Exists(s => s.GetType().Equals(typeof(T))))
            {
                throw new ArgumentException($"Terrain System {typeof(T).Name} is already registered.");
            }
            s_terrainSystems.Add(new T());
        }

        /// <summary>
        /// Unregisters a terrain-system implementation from the global Vista runtime.
        /// </summary>
        public static void UnregisterTerrainSystem<T>() where T : class, ITerrainSystem, new()
        {
            if (s_terrainSystems == null)
            {
                s_terrainSystems = new List<ITerrainSystem>();
            }
            s_terrainSystems.RemoveAll(s => s.GetType().Equals(typeof(T)));
        }

        /// <summary>
        /// Gets all terrain-system implementations currently registered with Vista.
        /// </summary>
        /// <returns>The registered terrain-system instances.</returns>
        public static IEnumerable<ITerrainSystem> GetTerrainSystems()
        {
            if (s_terrainSystems == null)
                s_terrainSystems = new List<ITerrainSystem>();
            // Surface Polaris first when present (the flagship backend), keeping the rest in registration
            // order (OrderBy is stable). All callers are editor only, so the per call ordering is cheap.
            return s_terrainSystems.OrderBy(s => IsPolarisTerrainSystem(s) ? 0 : 1);
        }

        // Matched by type name so this carries no dependency on the optional Polaris assembly and does not
        // rely on the display label.
        private static bool IsPolarisTerrainSystem(ITerrainSystem system)
        {
            return system != null &&
                system.GetType().Name.IndexOf("Polaris", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Gets the registered terrain-system instance of a specific type, if one exists.
        /// </summary>
        /// <returns>The registered terrain system of type <typeparamref name="T"/>, or <see langword="null"/> if none is registered.</returns>
        public static ITerrainSystem GetTerrainSystem<T>() where T : ITerrainSystem
        {
            if (s_terrainSystems == null)
            {
                s_terrainSystems = new List<ITerrainSystem>();
            }
            ITerrainSystem system = s_terrainSystems.Find(s => s.GetType().Equals(typeof(T)));
            return system;
        }

        /// <summary>
        /// Gets the union of channels every present terrain system can consume, for stripping unused biome outputs.
        /// </summary>
        /// <param name="tiles">The tiles being generated in this pass.</param>
        /// <returns>
        /// The OR of <see cref="ITerrainSystem.supportedData"/> across the distinct terrain systems the tiles map to. Returns
        /// all channels when a tile maps to no registered system, so an unknown backend is never stripped, and when no tile
        /// contributes a known system.
        /// </returns>
        /// <remarks>
        /// Unioning across the present systems keeps every channel some backend uses, so a mixed Unity and Polaris manager
        /// generates the superset and each tile still drops what it cannot consume through its own populator guards.
        /// </remarks>
        private static BiomeDataMask GetConsumableDataMask(IEnumerable<ITile> tiles)
        {
            BiomeDataMask combined = 0;
            bool anyKnownSystem = false;
            HashSet<Type> seenTileTypes = new HashSet<Type>();
            foreach (ITile tile in tiles)
            {
                Type tileType = tile.GetType();
                if (!seenTileTypes.Add(tileType))
                {
                    continue;
                }

                ITerrainSystem system = null;
                foreach (ITerrainSystem candidate in GetTerrainSystems())
                {
                    Type candidateTileType = candidate.GetTileComponentType();
                    if (candidateTileType != null && candidateTileType.IsAssignableFrom(tileType))
                    {
                        system = candidate;
                        break;
                    }
                }

                if (system == null)
                {
                    // Unknown tile type, be conservative and strip nothing.
                    return (BiomeDataMask)(~0);
                }

                combined |= system.supportedData;
                anyKnownSystem = true;
            }

            return anyKnownSystem ? combined : (BiomeDataMask)(~0);
        }

        /// <summary>
        /// Creates a new manager GameObject in the current scene.
        /// </summary>
        /// <returns>The newly created manager component.</returns>
        public static VistaManager CreateInstanceInScene()
        {
            GameObject managerGO = new GameObject("VistaManager");
            VistaManager manager = managerGO.AddComponent<VistaManager>();
            return manager;
        }

        /// <summary>
        /// Restores the manager to Vista's default runtime settings.
        /// </summary>
        public void Reset()
        {
            m_id = Utilities.GenerateId();
            m_terrainMaxHeight = 500;
            m_heightMapResolution = 513;
            m_textureResolution = 512;
            m_detailDensityMapResolution = 512;
            m_shouldCullBiomes = true;
            m_missingGeometryAction = MissingOutputAction.Clear;
            m_missingTextureAction = MissingOutputAction.Clear;
            m_missingPopulationAction = MissingOutputAction.Clear;
            m_objectToSpawnPerFrame = 20;
        }

        protected void OnEnable()
        {
            s_allInstances.Add(this);
        }

        protected void OnDisable()
        {
            s_allInstances.Remove(this);
            CancelGeneration();
        }

        /// <summary>
        /// Gets the biomes currently owned by or associated with this manager.
        /// </summary>
        /// <returns>
        /// The biomes returned by the registered biome-provider callback, or a fallback single child biome when no callback
        /// is registered.
        /// </returns>
        public IBiome[] GetBiomes()
        {
            if (getBiomesCallback != null)
            {
                return getBiomesCallback.Invoke(this);
            }
            else
            {
                return new IBiome[] { GetComponentInChildren<IBiome>() };
            }
        }

        /// <summary>
        /// Gets all tiles contributed to this manager through the tile-collection pipeline.
        /// </summary>
        /// <returns>A list containing the collected tiles.</returns>
        public List<ITile> GetTiles()
        {
            Collector<ITile> collector = new Collector<ITile>();
            if (collectTiles != null)
            {
                collectTiles.Invoke(this, collector);
            }
            return collector.ToList();
        }

        /// <summary>
        /// Gets all tiles contributed to this manager through the tile-collection pipeline as an array.
        /// </summary>
        /// <returns>An array containing the collected tiles.</returns>
        public ITile[] GetTileArray()
        {
            Collector<ITile> collector = new Collector<ITile>();
            if (collectTiles != null)
            {
                collectTiles.Invoke(this, collector);
            }
            return collector.ToArray();
        }

        /// <summary>
        /// Requests generation for an authoritative set of tiles.
        /// </summary>
        /// <param name="tiles">
        /// The complete, authoritative tile set for this request. The Manager snapshots the sequence immediately and does
        /// not combine it with earlier requests.
        /// </param>
        /// <returns>
        /// A task representing only this request. The task is cancelled if the request is superseded before starting or if
        /// active cancellation is observed before a tile or after one of its biome-data requests.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tiles"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when the collection contains a <see langword="null"/> tile.</exception>
        /// <remarks>
        /// If another request is active, this method asks it to cancel and stores the new request as the only pending
        /// replacement. The coordinator starts the replacement only after active work reaches a terminal point. Cancellation
        /// can stop long biome graph execution or prevent a later tile from starting. Once all biome requests for a tile
        /// finish, blending and population complete without another cancellation checkpoint so the tile is not left partially
        /// populated.
        /// </remarks>
        public GenerationTask Generate(IEnumerable<ITile> tiles)
        {
            // Reject an invalid request before changing coordinator state.
            if (tiles == null)
                throw new ArgumentNullException(nameof(tiles));

            // Snapshot the authoritative tile set so later caller mutations cannot change this request.
            ITile[] tileSnapshot = tiles.ToArray();
            if (tileSnapshot.Any(t => t == null))
                throw new ArgumentException("The tile collection cannot contain null.", nameof(tiles));

            // Last request wins: a pending request that never started is cancelled immediately.
            if (m_pendingRequest != null)
            {
                m_pendingRequest.task.MarkCancelled();
            }

            // Store this request as the single pending generation represented by its own task.
            GenerationTask task = new GenerationTask();
            m_pendingRequest = new GenerationRequest
            {
                tiles = tileSnapshot,
                task = task
            };

            // Ask active work to unwind cooperatively before the coordinator starts this request.
            m_activeCancellation?.Cancel();

            // Start the one coordinator responsible for serializing active and pending requests.
            if (!m_isGenerationCoordinatorRunning)
            {
                m_isGenerationCoordinatorRunning = true;
                CoroutineUtility.StartCoroutine(CoordinateGeneration());
            }

            // Return the task for this exact request, including cancellation if it is later superseded.
            return task;
        }

        /// <summary>
        /// Requests cancellation of active generation and cancels any pending replacement.
        /// </summary>
        /// <returns>
        /// The task that was active when cancellation was requested, or <see cref="GenerationTask.completedTask"/> when no
        /// request was active.
        /// </returns>
        /// <remarks>
        /// The method returns immediately. Yield the returned task to wait until that active request completes or reaches a
        /// cancellation checkpoint and finishes cleanup. A later caller may submit another request while the returned task is
        /// unwinding, so its completion does not by itself guarantee that <see cref="isGenerating"/> is false.
        /// </remarks>
        public GenerationTask CancelGeneration()
        {
            if (m_pendingRequest != null)
            {
                m_pendingRequest.task.MarkCancelled();
                m_pendingRequest = null;
            }

            GenerationTask task = m_activeRequest != null ? m_activeRequest.task : null;
            m_activeCancellation?.Cancel();
            return task ?? GenerationTask.completedTask;
        }

        private IEnumerator CoordinateGeneration()
        {
            // Process one request at a time, then loop again if Generate stored a newer pending request.
            while (m_pendingRequest != null)
            {
                // Promote the newest pending request to active before exposing its running state.
                GenerationRequest request = m_pendingRequest;
                m_pendingRequest = null;
                m_activeRequest = request;

                // Cancellation authority exists only for active work; subprocesses receive its read-only signal.
                m_activeCancellation = new CancellationHandle();
                m_activeCancellationObserved = false;
                request.task.MarkRunning();

                // Resolve biomes when the request actually starts so it observes the latest scene state.
                IBiome[] biomes = GetBiomes();
                if (biomes.Length > 0 && request.tiles.Length > 0)
                {
                    // Prepare overlap data only for the authoritative tile snapshot supplied by the caller.
                    List<ITile> overlappedTiles = new List<ITile>();
                    HashSet<KeyValuePair<ITile, IBiome>> overlapTests = new HashSet<KeyValuePair<ITile, IBiome>>();
                    foreach (ITile tile in request.tiles)
                    {
                        if (tile.OverlapTest(biomes, overlapTests))
                        {
                            overlappedTiles.Add(tile);
                        }
                    }

                    // Wait until generation succeeds or cooperatively unwinds after cancellation.
                    List<ITile> changedTiles = new List<ITile>();
                    Guid editSessionId = TerrainEditSession.Begin(EDIT_INTENT, overlappedTiles);
                    Guid progressId = ProgressNotifications.Begin("VistaManager.Generate()");
                    try
                    {
                        yield return ProcessBiomesProgressive(
                            m_activeCancellation.signal,
                            biomes,
                            overlappedTiles,
                            overlapTests,
                            editSessionId,
                            changedTiles,
                            progressId);
                    }
                    finally
                    {
                        try
                        {
                            TerrainEditSession.End(editSessionId, changedTiles);
                        }
                        finally
                        {
                            ProgressNotifications.End(progressId);
                        }
                    }
                }

                // Publish a terminal state only after all work for this request has finished cleaning up.
                if (m_activeCancellationObserved)
                    request.task.MarkCancelled();
                else
                    request.task.MarkCompleted();

                // Release active state so the next loop iteration can promote the latest pending request.
                m_activeRequest = null;
                m_activeCancellation = null;
            }

            // No active or pending request remains, so a future Generate call must start a new coordinator.
            m_isGenerationCoordinatorRunning = false;
        }

        private IEnumerator ProcessBiomesProgressive(
            CancellationSignal cancellationSignal,
            IEnumerable<IBiome> biomes,
            IEnumerable<ITile> overlappedTiles,
            ICollection<KeyValuePair<ITile, IBiome>> overlapTests,
            Guid editSessionId,
            List<ITile> changedTiles,
            Guid progressId)
        {
            int currentTileIndex = 0;
            int tileCount = overlappedTiles.Count();
            ProgressNotifications.Report(progressId, 0);
            VistaDebugger.OpenScope($"VistaManager: {gameObject.name}", DebugScopeType.Custom);

            beforeGeneratingUnityCallback.Invoke();
            beforeGenerating?.Invoke(this);

            foreach (IBiome b in biomes)
            {
                b.OnBeforeVMGenerate();
            }

            // Decide once per generation which channels any present terrain system can consume, then pass it to every biome
            // request below. The biome intersects it with its own data mask, so outputs no backend uses are never generated
            // while each biome still runs its graph once with a mask that stays stable across the pass.
            BiomeDataMask consumableDataMask = GetConsumableDataMask(overlappedTiles);

            foreach (ITile t in overlappedTiles)
            {
                t.maxHeight = terrainMaxHeight;
                t.heightMapResolution = heightMapResolution;
                t.textureResolution = textureResolution;
                t.detailDensityMapResolution = detailDensityMapResolution;
            }

            ObjectPopulateArgs objectPopulateArgs = new ObjectPopulateArgs();
            objectPopulateArgs.objectsPerFrame = m_objectToSpawnPerFrame;
            List<ITile> appliedTiles = new List<ITile>();

            foreach (ITile t in overlappedTiles)
            {
                // Treat one tile as the atomic generation unit: cancel before it starts or let it finish completely.
                if (cancellationSignal.isCancellationRequested)
                {
                    FinalizeAppliedTiles(appliedTiles, editSessionId, changedTiles);
                    m_activeCancellationObserved = true;
                    currentlyProcessingTile = null;
                    VistaDebugger.CloseScope(); // VistaManager generation scope
                    yield break;
                }

                using (TransientResourceRegistry tileTransientResources = new TransientResourceRegistry())
                {
                    VistaDebugger.OpenScope($"Process Tile: {t.gameObject.name}", DebugScopeType.Custom);
                    ProgressNotifications.Report(progressId, currentTileIndex, tileCount, "Processing tiles");
                    currentTileIndex += 1;
                    currentlyProcessingTile = t;

                    List<BiomeDataRequest> requests = new List<BiomeDataRequest>();
                    List<BiomeBlendConfig> blendConfigs = new List<BiomeBlendConfig>();
                    foreach (IBiome b in biomes)
                    {
                        if (m_shouldCullBiomes && !overlapTests.Contains(new KeyValuePair<ITile, IBiome>(t, b)))
                        {
                            continue;
                        }

                        BiomeDataRequest r = b.RequestData(t.worldBounds, heightMapResolution, textureResolution, consumableDataMask, cancellationSignal);
                        requests.Add(r);
                        BiomeBlendOptions blendOptions = b.blendOptions;
                        float heightOffset = 0;
                        float heightScale = 1;
                        if (blendOptions.useTransformForHeightBlend && terrainMaxHeight > 0)
                        {
                            Transform biomeTransform = b.gameObject.transform;
                            heightOffset = biomeTransform.position.y / terrainMaxHeight;
                            heightScale = biomeTransform.lossyScale.y;
                        }
                        blendConfigs.Add(new BiomeBlendConfig(
                            b.gameObject.name,
                            blendOptions,
                            heightOffset,
                            heightScale));
                        yield return r;
                        if (cancellationSignal.isCancellationRequested)
                        {
                            foreach (BiomeDataRequest request in requests)
                            {
                                request.data?.Dispose();
                            }
                            FinalizeAppliedTiles(appliedTiles, editSessionId, changedTiles);
                            m_activeCancellationObserved = true;
                            currentlyProcessingTile = null;
                            VistaDebugger.CloseScope(); // Process Tile
                            VistaDebugger.CloseScope(); // VistaManager generation scope
                            yield break;
                        }
                    }

                    List<BiomeData> biomeDatas = new List<BiomeData>();
                    foreach (BiomeDataRequest r in requests)
                    {
                        biomeDatas.Add(r.data);
                    }

                    BiomeData data = blendBiomeDataCallback.Invoke(blendConfigs, biomeDatas);
                    foreach (BiomeData d in biomeDatas)
                    {
                        d.Dispose();
                    }

                    yield return null;

                    SealHandler.ProcessResult sealResult = SealHandler.ProcessTile(
                        t, data, tileTransientResources, cancellationSignal);
                    yield return sealResult;
                    if (sealResult.isFaulted)
                    {
                        data.Dispose();
                        currentlyProcessingTile = null;
                        VistaDebugger.CloseScope(); // Process Tile
                        continue;
                    }
                    if (cancellationSignal.isCancellationRequested)
                    {
                        data.Dispose();
                        FinalizeAppliedTiles(appliedTiles, editSessionId, changedTiles);
                        m_activeCancellationObserved = true;
                        currentlyProcessingTile = null;
                        VistaDebugger.CloseScope(); // Process Tile
                        VistaDebugger.CloseScope(); // VistaManager generation scope
                        yield break;
                    }

                    VistaDebugger.OpenScope("Populate data", DebugScopeType.Custom);
                    TerrainEditSession.BeforeWrite(editSessionId, t);
                    t.OnBeforeApplyingData();
                    HandlePopulateGeometry(t, data);
                    HandlePopulateTextures(t, data);
                    UnregisterPopulatedTerrainLayers(data, tileTransientResources);
                    yield return null;

                    HandlePopulateTrees(t, data);
                    yield return null;

                    yield return HandlePopulateDetailDensity(t, data);
                    yield return null;

                    HandlePopulateDetailInstances(t, data);
                    yield return null;

                    yield return HandlePopulateObjects(t, data, objectPopulateArgs);
                    yield return null;

                    HandlePopulateGenericTextures(t, data);
                    yield return null;

                    HandlePopulateGenericBuffers(t, data);

                    data.Dispose();
                    appliedTiles.Add(t);
                    VistaDebugger.CloseScope(); // Populate
                    VistaDebugger.CloseScope(); // Process Tile
                    yield return null;
                }
            }
            yield return null;

            ProgressNotifications.Report(progressId, 1, "Finishing up");

            foreach (ITile t in appliedTiles)
            {
                currentlyProcessingTile = t;
                if (t is IGeometryPopulator gp)
                {
                    gp.MatchSeams();
                }
            }
            yield return null;

            foreach (ITile t in appliedTiles)
            {
                currentlyProcessingTile = t;
                t.OnAfterApplyingData();
                TerrainEditSession.AfterWrite(editSessionId, t);
                changedTiles.Add(t);
            }

            foreach (IBiome b in biomes)
            {
                b.OnAfterVMGenerate();
            }

            currentlyProcessingTile = null;
            afterGeneratingUnityCallback.Invoke();
            afterGenerating?.Invoke(this);

            VistaDebugger.CloseScope(); // VistaManager generation scope
        }

        private void FinalizeAppliedTiles(List<ITile> appliedTiles, Guid editSessionId, List<ITile> changedTiles)
        {
            foreach (ITile tile in appliedTiles)
            {
                currentlyProcessingTile = tile;
                if (tile is IGeometryPopulator geometry)
                {
                    geometry.MatchSeams();
                }
            }

            foreach (ITile tile in appliedTiles)
            {
                currentlyProcessingTile = tile;
                tile.OnAfterApplyingData();
                TerrainEditSession.AfterWrite(editSessionId, tile);
                changedTiles.Add(tile);
            }
        }

        /// <summary>
        /// Adds the Manager's generation pipeline delegates and their current subscribers to parallel collections.
        /// </summary>
        /// <param name="names">Receives the field name associated with each delegate.</param>
        /// <param name="delegates">Receives the corresponding delegate instance, including <see langword="null"/> entries.</param>
        /// <remarks>Used by diagnostics to inspect the generation pipeline without invoking it.</remarks>
        protected static void GetPipelineDelegates(List<string> names, List<Delegate> delegates)
        {
            names.Add(nameof(collectTiles)); delegates.Add(collectTiles);

            names.Add(nameof(beforeGenerating)); delegates.Add(beforeGenerating);

            names.Add(nameof(heightMapPopulated)); delegates.Add(heightMapPopulated);
            names.Add(nameof(holeMapPopulated)); delegates.Add(holeMapPopulated);
            names.Add(nameof(meshDensityMapPopulated)); delegates.Add(meshDensityMapPopulated);

            names.Add(nameof(albedoMapPopulated)); delegates.Add(albedoMapPopulated);
            names.Add(nameof(metallicMapPopulated)); delegates.Add(metallicMapPopulated);

            names.Add(nameof(layerWeightPopulated)); delegates.Add(layerWeightPopulated);

            names.Add(nameof(treePopulated)); delegates.Add(treePopulated);

            names.Add(nameof(detailDensityPopulated)); delegates.Add(detailDensityPopulated);
            names.Add(nameof(detailInstancePopulated)); delegates.Add(detailInstancePopulated);

            names.Add(nameof(objectPopulated)); delegates.Add(objectPopulated);

            names.Add(nameof(genericTexturesPopulated)); delegates.Add(genericTexturesPopulated);
            names.Add(nameof(genericBuffersPopulated)); delegates.Add(genericBuffersPopulated);

            names.Add(nameof(afterGenerating)); delegates.Add(afterGenerating);
        }

        /// <summary>
        /// Collects scene height from the manager's tiles into a destination render texture.
        /// </summary>
        /// <param name="targetRt">The destination texture that receives the collected height data.</param>
        /// <param name="worldBounds">The world-space bounds represented by <paramref name="targetRt"/>.</param>
        /// <remarks>
        /// This is primarily used by <see cref="LocalProceduralBiome"/> when it needs a scene-height input for graph
        /// execution.
        /// </remarks>
        public void CollectSceneHeight(RenderTexture targetRt, Bounds worldBounds)
        {
            ITile[] tiles = GetTileArray();
            SceneDataUtils.CollectWorldHeight(tiles, targetRt, worldBounds);
        }

        private void OnDrawGizmosSelected()
        {
            drawGizmosSelectedCallback?.Invoke();
        }
    }
}
#endif


