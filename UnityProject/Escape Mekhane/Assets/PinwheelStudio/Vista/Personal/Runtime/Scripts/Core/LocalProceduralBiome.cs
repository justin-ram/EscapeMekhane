#if VISTA
using Pinwheel.Vista.Graph;
using Pinwheel.Vista.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pinwheel.Vista.ExposeProperty;
using Pinwheel.Vista.Diagnostics;

namespace Pinwheel.Vista
{
    [ExecuteInEditMode]
    [AddComponentMenu("Vista/Local Procedural Biome")]
    [HelpURL("https://docs.pinwheelstud.io/vista/docs/local-procedural-biome-component-overview.html")]
    /// <summary>
    /// Defines a biome whose graph is generated once in its own bounds, cached, then remapped into each requested tile.
    /// </summary>
    /// <remarks>
    /// A <see cref="LocalProceduralBiome"/> is both a biome definition and a graph input source. It contributes a polygonal
    /// biome mask, optional scene height, and custom texture or position inputs, then stores the generated
    /// <see cref="BiomeData"/> in <see cref="cachedData"/> for reuse. Subsequent tile requests copy that cached data from the
    /// biome's own bounds into the requested world bounds instead of re-running the terrain graph every time.
    /// </remarks>
    public class LocalProceduralBiome : MonoBehaviour, IProceduralBiome, IPolygonalAreaWithFalloff, IBiomeTemplateSpawnCallbackReceiver, ISerializationCallbackReceiver
    {
        protected static HashSet<LocalProceduralBiome> s_allInstances = new HashSet<LocalProceduralBiome>();
        /// <summary>
        /// Gets the currently enabled local procedural biomes tracked by the runtime.
        /// </summary>
        /// <remarks>
        /// Instances register in <c>OnEnable</c> and unregister in <c>OnDisable</c>. This collection is used by helpers
        /// such as <see cref="Graph.LPBInputProvider"/> to resolve a biome by serialized GUID after domain reload or
        /// deserialization.
        /// </remarks>
        public static IEnumerable<LocalProceduralBiome> allInstances
        {
            get
            {
                return s_allInstances;
            }
        }

        internal delegate TerrainGraph CloneAndOverrideGraphHandler(TerrainGraph src, IEnumerable<PropertyOverride> overrides);
        internal static event CloneAndOverrideGraphHandler cloneAndOverrideGraphCallback;

        [SerializeField]
        protected int m_order;
        /// <summary>
        /// Gets or sets the sort order used when multiple local biomes overlap the same tile.
        /// </summary>
        /// <remarks>
        /// The manager uses biome order to decide evaluation and blending sequence for overlapping local procedural biomes.
        /// Higher-level blending behavior is still controlled by <see cref="blendOptions"/>.
        /// </remarks>
        public int order
        {
            get
            {
                return m_order;
            }
            set
            {
                m_order = value;
            }
        }

        [SerializeField]
        protected TerrainGraph m_terrainGraph;
        /// <summary>
        /// Gets or sets the terrain graph that generates this biome's cached outputs.
        /// </summary>
        /// <remarks>
        /// The assigned graph is evaluated in the biome's own bounds when the cache is rebuilt. When exposed properties are
        /// present and a clone callback is available, the graph is cloned per cache rebuild so
        /// <see cref="propertyOverrides"/> can be applied without mutating the source asset instance.
        /// </remarks>
        public TerrainGraph terrainGraph
        {
            get
            {
                return m_terrainGraph;
            }
            set
            {
                m_terrainGraph = value;
            }
        }

        /// <summary>
        /// Creates the input provider that feeds this biome's terrain graph in biome context.
        /// </summary>
        public IExternalInputProvider CreateGraphInputProvider()
        {
            return new LPBInputProvider(this);
        }

        /// <summary>
        /// Fixes up this biome's owned references after it was spawned from a <see cref="BiomeTemplate"/>. The
        /// terrain graph is the one owned, per-biome authored asset, so it is cloned <b>in memory</b> and
        /// reassigned here; shared library assets (materials, tree/detail templates) stay referenced. The clone
        /// is registered in <paramref name="context"/> for the editor caller to persist; at runtime it simply
        /// lives in memory. No AssetDatabase use, so this runs in a build.
        /// </summary>
        /// <remarks>
        /// The clone comes from <see cref="BiomeTemplateSpawnContext.GetOrCreateClone{TObject}"/>, so biomes in
        /// the same spawn that share a source graph share one clone, preserving the reference topology the
        /// template author set up. That coordination is why the context is required: without it this biome could
        /// only clone in isolation, silently breaking the sharing a multi biome template was authored around.
        /// The clone is named after the source graph with any "Template" marker removed, so it reads as a
        /// regular working graph in the project window. Naming is a courtesy of whichever biome created the
        /// clone, not a contract other spawn callback receivers must follow.
        /// </remarks>
        public void OnSpawnedFromBiomeTemplate(BiomeTemplate template, BiomeTemplateSpawnContext context)
        {
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }
            if (m_terrainGraph == null)
            {
                return;
            }

            TerrainGraph clone = context.GetOrCreateClone(m_terrainGraph, out bool created);
            if (created)
            {
                clone.name = RemoveTemplateNameMarker(m_terrainGraph.name);
            }
            m_terrainGraph = clone;
        }

        // Strips every "Template" marker wherever it appears, ignoring case, then tidies the separators left
        // behind, so "Mountain_TerrainGraphTemplate", "Mountain_TemplateTerrainGraph" and "Mountain_template_TerrainGraph"
        // all yield "Mountain_TerrainGraph". Returns the name unchanged when it has no marker, or when stripping
        // would leave nothing meaningful.
        private static string RemoveTemplateNameMarker(string sourceName)
        {
            const string templateMarker = "Template";
            if (string.IsNullOrEmpty(sourceName) || sourceName.IndexOf(templateMarker, System.StringComparison.OrdinalIgnoreCase) < 0)
            {
                return sourceName;
            }
            string strippedName = System.Text.RegularExpressions.Regex.Replace(
                sourceName, templateMarker, string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            strippedName = System.Text.RegularExpressions.Regex.Replace(strippedName, @"([_\- ])[_\- ]+", "$1");
            strippedName = strippedName.Trim('_', ' ', '-');
            return string.IsNullOrEmpty(strippedName) ? sourceName : strippedName;
        }

        [SerializeField]
        protected Space m_space;
        /// <summary>
        /// Gets or sets the simulation space passed to the terrain graph and biome mask graph.
        /// </summary>
        /// <remarks>
        /// The value is forwarded into graph execution arguments so nodes can interpret biome-local data in either local or
        /// world space, depending on the selected mode.
        /// </remarks>
        public Space space
        {
            get
            {
                return m_space;
            }
            set
            {
                m_space = value;
            }
        }

        [SerializeField]
        protected BiomeDataMask m_dataMask;
        /// <summary>
        /// Gets or sets which output categories should be generated and cached for this biome.
        /// </summary>
        /// <remarks>
        /// The mask is passed to <c>TerrainGraphUtilities.RequestBiomeData</c>. Disabling a flag skips generating and
        /// storing that output category for this biome's cache.
        /// </remarks>
        public BiomeDataMask dataMask
        {
            get
            {
                return m_dataMask;
            }
            set
            {
                m_dataMask = value;
            }
        }

        [SerializeField]
        protected int m_baseResolution;
        /// <summary>
        /// Gets or sets the base graph resolution used when generating the biome cache in the biome's own bounds.
        /// </summary>
        /// <remarks>
        /// The value is clamped to Vista's supported range and rounded up to a multiple of 8 before storage because graph
        /// compute kernels expect resolutions aligned to that granularity.
        /// </remarks>
        public int baseResolution
        {
            get
            {
                return m_baseResolution;
            }
            set
            {
                m_baseResolution = Utilities.MultipleOf8(Mathf.Clamp(value, Constants.RES_MIN, Constants.RES_MAX));
            }
        }

        [SerializeField]
        protected int m_seed;
        /// <summary>
        /// Gets or sets the random seed forwarded into terrain graph generation for this biome.
        /// </summary>
        public int seed
        {
            get
            {
                return m_seed;
            }
            set
            {
                m_seed = value;
            }
        }

        /// <summary>
        /// Gets or sets whether the biome should capture a scene height texture and expose it to the terrain graph.
        /// </summary>
        /// <remarks>
        /// When enabled, <see cref="Graph.LPBInputProvider"/> asks the owning <see cref="VistaManager"/> to render the
        /// current scene height into an input texture named by Vista's graph constants before graph execution begins.
        /// </remarks>
        [System.Obsolete("Use terrainGraph.HasSceneHeightInput() instead.")]
        public bool shouldCollectSceneHeight
        {
            get
            {
                if (m_terrainGraph != null)
                {
                    return m_terrainGraph.HasSceneHeightInput();
                }
                return false;
            }
        }

        [SerializeField]
        protected int m_biomeMaskResolution;
        /// <summary>
        /// Gets or sets the resolution of the base biome mask and the editable biome-mask adjustment texture.
        /// </summary>
        /// <remarks>
        /// The value is clamped to Vista's supported range and rounded to a multiple of 8. When the resolution changes,
        /// existing <see cref="biomeMaskAdjustments"/> data is resampled with bilinear filtering so manual mask edits are
        /// preserved instead of discarded.
        /// </remarks>
        public int biomeMaskResolution
        {
            get
            {
                return m_biomeMaskResolution;
            }
            set
            {
                int oldRes = m_biomeMaskResolution;
                int newRes = Utilities.MultipleOf8(Mathf.Clamp(value, Constants.RES_MIN, Constants.RES_MAX));
                if (oldRes != newRes)
                {
                    m_biomeMaskResolution = newRes;
                    if (m_biomeMaskAdjustments != null && m_biomeMaskAdjustments.Length > 0)
                    {
                        m_biomeMaskAdjustments = Utilities.ResampleBilinear(m_biomeMaskAdjustments, oldRes, oldRes, newRes, newRes);
                    }
                }
            }
        }

        [SerializeField]
        protected BiomeMaskGraph m_biomeMaskGraph;
        /// <summary>
        /// Gets or sets an optional post-process graph that refines the generated biome mask.
        /// </summary>
        /// <remarks>
        /// When assigned, the base combined biome mask is fed into <c>BiomeMaskGraphUtilities.RequestData</c>, and the
        /// resulting mask replaces the original cache mask before the biome data is stored.
        /// </remarks>
        public BiomeMaskGraph biomeMaskGraph
        {
            get
            {
                return m_biomeMaskGraph;
            }
            set
            {
                m_biomeMaskGraph = value;
            }
        }

        /// <summary>
        /// Gets the biome bounds in world space.
        /// </summary>
        /// <remarks>
        /// The bounds are derived from anchor positions transformed by this object's transform. When
        /// <see cref="falloffDirection"/> is <see cref="FalloffDirection.Outer"/>, the expanded falloff polygon is used so
        /// overlap checks and cache generation cover the full fade region.
        /// </remarks>
        public Bounds worldBounds
        {
            get
            {
                return CalculateWorldBounds();
            }
        }

        protected long m_updateCounter;
        /// <summary>
        /// Gets or sets the biome change stamp used by the manager to detect invalidated state.
        /// </summary>
        /// <remarks>
        /// Extension helpers update this value with a timestamp-like counter whenever the biome changes and regeneration is
        /// required. After a successful manager pass, <see cref="VistaManager"/> overwrites it with the manager's own update
        /// counter so later comparisons can determine whether the biome is already in sync with that pass.
        /// </remarks>
        [System.Obsolete("Biome update counters are no longer used for generation invalidation.")]
        public long updateCounter
        {
            get
            {
                return m_updateCounter;
            }
            set
            {
                m_updateCounter = value;
            }
        }

        [SerializeField]
        protected Vector3[] m_anchors;
        /// <summary>
        /// Gets or sets the polygon vertices that define the core biome shape in biome-local space.
        /// </summary>
        /// <remarks>
        /// The getter returns a copy of the stored array. Assigning a new set of anchors recalculates
        /// <see cref="falloffAnchors"/> immediately so overlap checks and mask rendering stay in sync.
        /// </remarks>
        public Vector3[] anchors
        {
            get
            {
                return LocalAreaUtils.CloneAnchors(m_anchors);
            }
            set
            {
                if (value == null)
                {
                    m_anchors = new Vector3[0];
                }
                else
                {
                    m_anchors = new Vector3[value.Length];
                    value.CopyTo(m_anchors, 0);
                }
                RecalculateFalloffAnchors();
            }
        }

        [SerializeField]
        protected FalloffDirection m_falloffDirection;
        /// <summary>
        /// Gets or sets whether the falloff region expands outside the anchor polygon or shrinks inward.
        /// </summary>
        /// <remarks>
        /// Changing this value recomputes <see cref="falloffAnchors"/>. The selected direction also changes which polygon is
        /// treated as the biome's effective world bounds.
        /// </remarks>
        public FalloffDirection falloffDirection
        {
            get
            {
                return m_falloffDirection;
            }
            set
            {
                FalloffDirection oldValue = m_falloffDirection;
                FalloffDirection newValue = value;
                m_falloffDirection = newValue;
                if (oldValue != newValue)
                {
                    RecalculateFalloffAnchors();
                }
            }
        }

        [SerializeField]
        protected float m_falloffDistance;
        /// <summary>
        /// Gets or sets the distance used to build the falloff polygon from <see cref="anchors"/>.
        /// </summary>
        /// <remarks>
        /// Negative values are clamped to zero. Changing the distance recalculates <see cref="falloffAnchors"/>.
        /// </remarks>
        public float falloffDistance
        {
            get
            {
                return m_falloffDistance;
            }
            set
            {
                float oldValue = m_falloffDistance;
                float newValue = Mathf.Max(0, value);
                m_falloffDistance = newValue;
                if (oldValue != newValue)
                {
                    RecalculateFalloffAnchors();
                }
            }
        }

        [SerializeField]
        protected Vector3[] m_falloffAnchors;
        /// <summary>
        /// Gets the derived falloff polygon vertices in biome-local space.
        /// </summary>
        /// <remarks>
        /// The getter lazily rebuilds the array if it is missing or out of sync with <see cref="anchors"/>, then returns a
        /// cloned copy so callers cannot modify internal state by reference.
        /// </remarks>
        public Vector3[] falloffAnchors
        {
            get
            {
                if (m_falloffAnchors == null || m_falloffAnchors.Length != m_anchors.Length)
                {
                    RecalculateFalloffAnchors();
                }
                return LocalAreaUtils.CloneAnchors(m_falloffAnchors);
            }
        }

        /// <summary>
        /// Gets or sets the cached biome data generated in this biome's own bounds.
        /// </summary>
        /// <remarks>
        /// The cache owns GPU resources until <see cref="CleanUp"/> is called or the biome is regenerated. Tile requests do
        /// not return this object directly; they copy or remap from it into a separate request payload.
        /// </remarks>
        internal BiomeData cachedData { get; set; }

        // The effective data mask used to build cachedData, so a persistent cache can be rebuilt when a later pass needs a
        // channel it does not hold. Not serialized, it is only meaningful while a cache is alive.
        [System.NonSerialized]
        private BiomeDataMask m_cachedDataEffectiveMask;

        private GraphExecutionCache m_graphExecutionCache;

        [System.Serializable]
        /// <summary>
        /// Defines clean up mode values.
        /// </summary>
        public enum CleanUpMode
        {
            /// <summary>
            /// Dispose cached biome data automatically after each manager generation pass.
            /// </summary>
            EachIteration,
            /// <summary>
            /// Keep cached biome data until <see cref="CleanUp"/> is called manually.
            /// </summary>
            Manually
        }

        [SerializeField]
        protected CleanUpMode m_cleanUpMode;
        /// <summary>
        /// Gets or sets when cached biome data should be released.
        /// </summary>
        /// <remarks>
        /// <see cref="CleanUpMode.EachIteration"/> frees the cache after each manager generation pass. Use
        /// <see cref="CleanUpMode.Manually"/> only when repeated remapping of the same biome cache is more valuable than the
        /// extra memory cost.
        /// </remarks>
        public CleanUpMode cleanUpMode
        {
            get
            {
                return m_cleanUpMode;
            }
            set
            {
                m_cleanUpMode = value;
            }
        }

        [SerializeField]
        protected float[] m_biomeMaskAdjustments;
        /// <summary>
        /// Gets or sets per-pixel adjustments applied to the generated base biome mask.
        /// </summary>
        /// <remarks>
        /// The array length must exactly match <c>biomeMaskResolution * biomeMaskResolution</c>. The getter returns a copy.
        /// If the stored array length is invalid, it is discarded and treated as empty.
        /// </remarks>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the assigned array length does not match the current biome mask resolution.
        /// </exception>
        public float[] biomeMaskAdjustments
        {
            get
            {
                if (m_biomeMaskAdjustments.Length != m_biomeMaskResolution * m_biomeMaskResolution)
                {
                    m_biomeMaskAdjustments = new float[0];
                }

                float[] clonedData = new float[m_biomeMaskAdjustments.Length];
                m_biomeMaskAdjustments.CopyTo(clonedData, 0);
                return clonedData;
            }
            set
            {
                if (value == null)
                {
                    m_biomeMaskAdjustments = new float[0];
                }
                else if (value.Length != m_biomeMaskResolution * m_biomeMaskResolution)
                {
                    throw new System.ArgumentException("Wrong data dimension. Biome mask adjustment array length must be biomeMaskResolution^2");
                }
                else
                {
                    m_biomeMaskAdjustments = new float[value.Length];
                    value.CopyTo(m_biomeMaskAdjustments, 0);
                }
            }
        }

        [SerializeField]
        protected BiomeBlendOptions m_blendOptions;
        /// <summary>
        /// Gets or sets the per-output blending policy used when this biome overlaps others.
        /// </summary>
        /// <remarks>
        /// These settings are consumed by the multi-biome blend pipeline when cached results from several local biomes are
        /// merged into one tile payload.
        /// </remarks>
        public BiomeBlendOptions blendOptions
        {
            get
            {
                return m_blendOptions;
            }
            set
            {
                m_blendOptions = value;
            }
        }

        [SerializeField]
        protected TextureInput[] m_textureInputs = new TextureInput[0];
        /// <summary>
        /// Gets or sets custom texture inputs exposed to the terrain graph for this biome.
        /// </summary>
        /// <remarks>
        /// The getter returns a copy of the array. At request time, each valid input is copied into a temporary render
        /// texture and injected into the graph input container by name.
        /// </remarks>
        public TextureInput[] textureInputs
        {
            get
            {
                TextureInput[] clonedInputs = new TextureInput[m_textureInputs.Length];
                m_textureInputs.CopyTo(clonedInputs, 0);
                return clonedInputs;
            }
            set
            {
                if (value == null)
                {
                    m_textureInputs = new TextureInput[0];
                }
                else
                {
                    m_textureInputs = new TextureInput[value.Length];
                    value.CopyTo(m_textureInputs, 0);
                }
            }
        }

        [SerializeField]
        protected PositionInput[] m_positionInputs = new PositionInput[0];
        /// <summary>
        /// Gets or sets custom position-buffer inputs exposed to the terrain graph for this biome.
        /// </summary>
        /// <remarks>
        /// The getter returns a copy of the array. At request time, each valid position container is copied into a
        /// temporary graph buffer keyed by its configured input name.
        /// </remarks>
        public PositionInput[] positionInputs
        {
            get
            {
                PositionInput[] clonedInputs = new PositionInput[m_positionInputs.Length];
                m_positionInputs.CopyTo(clonedInputs, 0);
                return clonedInputs;
            }
            set
            {
                if (value == null)
                {
                    m_positionInputs = new PositionInput[0];
                }
                else
                {
                    m_positionInputs = new PositionInput[value.Length];
                    value.CopyTo(m_positionInputs, 0);
                }
            }
        }

        [SerializeField]
        internal string m_guid = Utilities.GenerateId();

        [SerializeField]
        internal PropertyOverride[] m_propertyOverrides = new PropertyOverride[0];
        /// <summary>
        /// Gets or sets exposed-property overrides applied when the terrain graph is cloned for this biome.
        /// </summary>
        /// <remarks>
        /// The getter returns a copy of the array. These overrides are used only when the graph exposes properties and the
        /// runtime has registered a clone-and-override callback; otherwise the source graph is executed directly and the
        /// overrides have no effect.
        /// </remarks>
        public PropertyOverride[] propertyOverrides
        {
            get
            {
                PropertyOverride[] clonedInputs = new PropertyOverride[m_propertyOverrides.Length];
                m_propertyOverrides.CopyTo(clonedInputs, 0);
                return clonedInputs;
            }
            set
            {
                if (value == null)
                {
                    m_propertyOverrides = new PropertyOverride[0];
                }
                else
                {
                    m_propertyOverrides = new PropertyOverride[value.Length];
                    value.CopyTo(m_propertyOverrides, 0);
                }
            }
        }

        /// <summary>
        /// Gets whether this biome is currently rebuilding its cached data.
        /// </summary>
        /// <remarks>
        /// This flag is raised only during the cache-generation portion of <see cref="RequestData"/> before the
        /// request-specific remap step runs.
        /// </remarks>
        internal bool isGeneratingCacheData { get; private set; }

        /// <summary>
        /// Restores the biome to Vista's default local-biome configuration.
        /// </summary>
        /// <remarks>
        /// The reset values define a square biome centered on the object, enable all output categories, use a 1024 base
        /// graph resolution, and clear all custom inputs and property overrides.
        /// </remarks>
        public void Reset()
        {
            m_order = 0;
            m_terrainGraph = null;
            m_space = Space.World;
            m_dataMask = (BiomeDataMask)(~0);
            m_baseResolution = 1024;
            m_seed = 0;

            m_biomeMaskResolution = 512;
            m_biomeMaskGraph = null;
            m_falloffDirection = FalloffDirection.Inner;
            m_falloffDistance = 100;
            m_anchors = new Vector3[]
            {
                new Vector3(-500, 0, -500), new Vector3(-500, 0, 500), new Vector3(500, 0, 500), new Vector3(500, 0, -500)
            };
            RecalculateFalloffAnchors();

            m_cleanUpMode = CleanUpMode.EachIteration;
            m_biomeMaskAdjustments = new float[0];

            m_blendOptions = BiomeBlendOptions.Default();
            m_textureInputs = new TextureInput[0];
            m_positionInputs = new PositionInput[0];
        }

        protected void OnEnable()
        {
            s_allInstances.Add(this);
            GraphAsset.graphChanged += OnGraphChanged;
            EnsureGraphExecutionCache();
        }

        protected void OnDisable()
        {
            s_allInstances.Remove(this);
            GraphAsset.graphChanged -= OnGraphChanged;
            CleanUp();
            DisposeGraphExecutionCache();
        }

        private GraphExecutionCache EnsureGraphExecutionCache()
        {
            if (m_graphExecutionCache == null)
            {
                m_graphExecutionCache = new GraphExecutionCache();
            }
            return m_graphExecutionCache;
        }

        private void DisposeGraphExecutionCache()
        {
            if (m_graphExecutionCache != null)
            {
                m_graphExecutionCache.Dispose();
                m_graphExecutionCache = null;
            }
        }

        protected void OnGraphChanged(GraphAsset graph)
        {
            if (graph != m_terrainGraph)
                return;
            CleanUp();
            NotifyChanged();
        }

        internal void NotifyChanged()
        {
            BiomeExtensions.NotifyChanged(this);
        }

        /// <summary>
        /// Creates a new biome GameObject in the current scene and optionally parents it to a manager.
        /// </summary>
        /// <param name="manager">
        /// Optional manager that should own the new biome. When supplied, the new object is parented under the manager and
        /// reset to local origin with identity rotation and unit scale.
        /// </param>
        /// <returns>The newly created biome component.</returns>
        public static LocalProceduralBiome CreateInstanceInScene(VistaManager manager)
        {
            GameObject biomeGO = new GameObject("Local Procedural Biome");
            LocalProceduralBiome biome = biomeGO.AddComponent<LocalProceduralBiome>();

            if (manager != null)
            {
                biome.transform.parent = manager.transform;
                biome.transform.localPosition = Vector3.zero;
                biome.transform.localRotation = Quaternion.identity;
                biome.transform.localScale = Vector3.one;
            }

            return biome;
        }

        /// <summary>
        /// Requests biome data for a target tile by remapping this biome's cached outputs into the requested bounds.
        /// </summary>
        /// <param name="worldBounds">
        /// Tile bounds that should receive the biome contribution. Cached data is copied from the biome's own world bounds
        /// into this destination area.
        /// </param>
        /// <param name="heightMapResolution">
        /// Target resolution for height-related outputs such as height, holes, and mesh density in the returned data.
        /// </param>
        /// <param name="textureResolution">
        /// Target resolution for texture-like outputs such as splat weights, albedo maps, density maps, and biome mask.
        /// </param>
        /// <param name="consumableDataMask">
        /// Channels the caller can consume. The biome intersects this value with <see cref="dataMask"/> before graph
        /// execution and rebuilds an existing cache when it lacks a newly required channel.
        /// </param>
        /// <param name="cancellationSignal">
        /// Optional read-only cancellation state propagated into terrain graph execution. Cancellation is observed after
        /// graph cleanup completes; partial cache data, cloned graphs, and input-provider resources are then released before
        /// the returned request completes.
        /// </param>
        /// <returns>
        /// A progressive request whose <see cref="BiomeDataRequest.data"/> payload is filled asynchronously. If
        /// <see cref="terrainGraph"/> is not assigned, the returned request completes immediately with an empty data object.
        /// </returns>
        /// <remarks>
        /// The first request after cache invalidation triggers full graph execution in the biome's own bounds at
        /// <see cref="baseResolution"/>. Later requests reuse <see cref="cachedData"/> and only perform the bounds-aware
        /// copy/remap step. The cache bounds are rounded to whole-world-unit XZ extents before generation so repeated
        /// requests use a stable cache domain. A cancelled cache build is never assigned to <see cref="cachedData"/> and
        /// does not modify the caller-facing payload.
        /// </remarks>
        public BiomeDataRequest RequestData(Bounds worldBounds, int heightMapResolution, int textureResolution, BiomeDataMask consumableDataMask = (BiomeDataMask)(~0), CancellationSignal cancellationSignal = null)
        {
            BiomeDataRequest request = new BiomeDataRequest();
            BiomeData data = new BiomeData();
            request.data = data;
            if (m_terrainGraph != null)
            {
                CoroutineUtility.StartCoroutine(RequestDataProgressive(request, worldBounds, heightMapResolution, textureResolution, consumableDataMask, cancellationSignal));
                return request;
            }
            else
            {
                request.Complete();
                return request;
            }
        }

        private IEnumerator RequestDataProgressive(BiomeDataRequest request, Bounds worldBounds, int heightMapResolution, int textureResolution, BiomeDataMask consumableDataMask, CancellationSignal cancellationSignal)
        {
            VistaDebugger.OpenScope($"Request biome data: {name}", DebugScopeType.Custom);

            Bounds biomeWorldBoundsInt = this.worldBounds;
            Vector3 boundsCenter = biomeWorldBoundsInt.center;
            boundsCenter.x = Mathf.Round(boundsCenter.x);
            boundsCenter.z = Mathf.Round(boundsCenter.z);
            Vector3 boundsSize = biomeWorldBoundsInt.size;
            boundsSize.x = Mathf.Round(boundsSize.x);
            boundsSize.y = worldBounds.size.y;
            boundsSize.z = Mathf.Round(boundsSize.z);

            biomeWorldBoundsInt.center = boundsCenter;
            biomeWorldBoundsInt.size = boundsSize;

            // Generate only channels produced by this biome and consumable by the requesting terrain systems.
            BiomeDataMask effectiveDataMask = m_dataMask & consumableDataMask;

            // A persistent cache built for an earlier pass may lack a channel this pass now needs, for example when a
            // different terrain system was added to the scene and widened the consumable set. Rebuild in that case. A cache
            // that is a superset is reused as is, the extra channels are simply not copied by unsupported tiles.
            if (cachedData != null && (effectiveDataMask & ~m_cachedDataEffectiveMask) != 0)
            {
                CleanUp();
            }

            // Build missing cache data once in the biome's stable bounds; tile requests remap from this cache.
            if (cachedData == null)
            {
                isGeneratingCacheData = true;

                BiomeDataRequest cacheDataRequest = new BiomeDataRequest();
                BiomeData cache = new BiomeData();
                cacheDataRequest.data = cache;

                TerrainGraph graphToExecute;
                if (m_terrainGraph.HasExposedProperties && cloneAndOverrideGraphCallback != null)
                {
                    graphToExecute = cloneAndOverrideGraphCallback.Invoke(terrainGraph, m_propertyOverrides);
                }
                else
                {
                    graphToExecute = m_terrainGraph;
                }

                GraphInputContainer inputContainer = new GraphInputContainer();
                LPBInputProvider inputProvider = new LPBInputProvider(this);
                // SetInput allocates the biome mask internally via RenderPostProcessedBiomeMask.
                // Ownership stays with the provider until RemoveTexture is called below.
                inputProvider.SetInput(inputContainer, graphToExecute);

                CoroutineUtility.StartCoroutine(TerrainGraphUtilities.RequestBiomeData(this, cacheDataRequest, graphToExecute, biomeWorldBoundsInt, space, m_baseResolution, m_seed, inputContainer, effectiveDataMask, inputProvider.FillTerrainGraphArguments, EnsureGraphExecutionCache(), cancellationSignal));
                yield return cacheDataRequest;

                if (cancellationSignal != null && cancellationSignal.isCancellationRequested)
                {
                    cacheDataRequest.data.Dispose();
                    if (graphToExecute != m_terrainGraph)
                    {
                        Object.DestroyImmediate(graphToExecute);
                    }
                    inputProvider.CleanUp();
                    isGeneratingCacheData = false;
                    request.Complete();
                    VistaDebugger.CloseScope();
                    yield break;
                }

                // Transfer biome mask ownership from the input provider to BiomeData for blending.
                // RemoveTexture detaches it from m_textures so CleanUp below will not dispose it.
                cacheDataRequest.data.biomeMaskMap = inputProvider.RemoveTexture(GraphConstants.BIOME_MASK_INPUT_NAME);
                cachedData = cacheDataRequest.data;
                m_cachedDataEffectiveMask = effectiveDataMask;

                if (graphToExecute != m_terrainGraph)
                {
                    Object.DestroyImmediate(graphToExecute);
                }
                inputProvider.CleanUp();
                isGeneratingCacheData = false;
            }

            // Copy the reusable biome-domain cache into this request's tile bounds and resolutions.
            BiomeDataUtilities.Copy(cachedData, biomeWorldBoundsInt, request.data, worldBounds, heightMapResolution, textureResolution);
            request.Complete();

            VistaDebugger.CloseScope();
            yield break;
        }

        /// <summary>
        /// Tests whether the biome's effective polygon overlaps a world-space rectangular area.
        /// </summary>
        /// <param name="area">The world-space bounds of the area to test.</param>
        /// <returns>
        /// <see langword="true"/> when the biome polygon overlaps the rectangle formed by <paramref name="area"/> in the XZ
        /// plane; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The check is purely planar and ignores Y extent. Outer falloff uses the expanded falloff polygon, while inner
        /// falloff uses the authored polygon because the complete fade band remains inside that boundary.
        /// </remarks>
        public bool IsOverlap(Bounds area)
        {
            return LocalAreaUtils.IsOverlap(this, area);
        }

        /// <summary>
        /// Rebuilds the falloff polygon from the current anchor polygon, falloff distance, and falloff direction.
        /// </summary>
        /// <remarks>
        /// This method should be called after low-level edits to the serialized anchor data. Property setters already call
        /// it when required.
        /// </remarks>
        public void RecalculateFalloffAnchors()
        {
            m_falloffAnchors = LocalAreaUtils.CalculateFalloffAnchors(
                m_anchors,
                m_falloffDistance,
                m_falloffDirection);
        }

        /// <summary>
        /// Calculates the biome's world-space bounding box from its authored polygon.
        /// </summary>
        /// <returns>The axis-aligned world-space bounds enclosing the effective biome polygon.</returns>
        /// <remarks>
        /// When <see cref="falloffDirection"/> is <see cref="FalloffDirection.Outer"/>, the expanded falloff polygon is used;
        /// otherwise the raw anchor polygon defines the bounds. The method includes transformed Y values from the authored
        /// vertices, even though overlap tests are performed in XZ space only.
        /// </remarks>
        protected Bounds CalculateWorldBounds()
        {
            return LocalAreaUtils.CalculateWorldBounds(
                transform,
                m_anchors,
                m_falloffAnchors,
                m_falloffDirection);
        }

        /// <summary>
        /// Releases the cached biome data owned by this biome.
        /// </summary>
        /// <remarks>
        /// This disposes GPU resources held by <see cref="cachedData"/> and clears the cache reference. It is invoked on
        /// disable, on graph changes, and optionally after each manager generation pass depending on
        /// <see cref="cleanUpMode"/>. The method does not modify biome inputs, masks, or authored settings.
        /// </remarks>
        public void CleanUp()
        {
            if (cachedData != null)
            {
                cachedData.Dispose();
                cachedData = null;
            }
        }

        /// <summary>
        /// Called by the manager before tile generation begins.
        /// </summary>
        /// <remarks>
        /// The current implementation performs no work here, but the method is part of the biome lifecycle contract and is
        /// available for future extension.
        /// </remarks>
        public void OnBeforeVMGenerate()
        {

        }

        /// <summary>
        /// Called by the manager after tile generation finishes.
        /// </summary>
        /// <remarks>
        /// When <see cref="cleanUpMode"/> is <see cref="CleanUpMode.EachIteration"/>, this method disposes the cached biome
        /// data so the next generation pass starts from a fresh cache.
        /// </remarks>
        public void OnAfterVMGenerate()
        {
            if (m_cleanUpMode == CleanUpMode.EachIteration)
            {
                CleanUp();
            }
        }

        /// <summary>
        /// Renders the authored biome polygon and falloff into a new mask texture.
        /// </summary>
        /// <returns>
        /// A newly created RFloat render texture at <see cref="biomeMaskResolution"/> containing the procedural base biome
        /// mask before manual adjustments are applied.
        /// </returns>
        /// <remarks>
        /// The raw anchor polygon defines the solid biome area, while <see cref="falloffAnchors"/> and
        /// <see cref="falloffDirection"/> define how the mask fades at the boundary. The returned texture is owned by the
        /// caller.
        /// </remarks>
        internal RenderTexture RenderBaseBiomeMask()
        {
            RenderTexture biomeMask = LocalAreaUtils.AllocatePolygonalMaskRT(m_biomeMaskResolution);
            LocalAreaUtils.RenderPolygonalMask(this, biomeMask);
            return biomeMask;
        }

        /// <summary>
        /// Renders the base biome mask and applies any serialized mask adjustments.
        /// </summary>
        /// <returns>
        /// A newly created biome mask texture suitable for injection into terrain-graph inputs or biome-mask post-process
        /// graphs.
        /// </returns>
        /// <remarks>
        /// When <see cref="biomeMaskAdjustments"/> contains data, the method builds a temporary texture from that float
        /// array, combines it with the procedural base mask, and destroys the temporary CPU-generated texture before
        /// returning.
        /// </remarks>
        internal RenderTexture RenderCombinedBiomeMask()
        {
            RenderTexture baseMask = RenderBaseBiomeMask();
            if (m_biomeMaskAdjustments != null && m_biomeMaskAdjustments.Length > 0)
            {
                Texture2D adjustmentTex = Utilities.TextureFromFloats(m_biomeMaskAdjustments, m_biomeMaskResolution, m_biomeMaskResolution);
                LPBUtilities.CombineBiomeMask(baseMask, adjustmentTex);
                Object.DestroyImmediate(adjustmentTex);
            }
            return baseMask;
        }

        /// <summary>
        /// Renders the final biome mask for use by both the Terrain Graph input and the biome blending pipeline.
        /// </summary>
        /// <remarks>
        /// Starts from the combined polygon mask (anchors plus any manual paint adjustments). If a Biome Mask Graph is
        /// assigned, runs it immediately via <see cref="BiomeMaskGraphUtilities.ProcessPolygonalMask"/> and returns the processed result,
        /// disposing the intermediate combined mask. The caller owns the returned texture and is responsible for releasing it.
        /// </remarks>
        internal RenderTexture RenderPostProcessedBiomeMask()
        {
            // Allocation point for the biome mask. A fresh RenderTexture is created every call.
            // Ownership passes to the caller (LPBInputProvider), which tracks it until
            // RequestDataProgressive transfers it to BiomeData.biomeMaskMap via RemoveTexture.
            RenderTexture combinedMask = RenderCombinedBiomeMask();
            if (m_biomeMaskGraph == null)
            {
                return combinedMask;
            }

            RenderTexture processedMask = BiomeMaskGraphUtilities.ProcessPolygonalMask(
                m_biomeMaskGraph,
                worldBounds,
                space,
                combinedMask);
            if (processedMask == null)
            {
                return combinedMask;
            }

            combinedMask.Release();
            Object.DestroyImmediate(combinedMask);
            return processedMask;
        }

        /// <summary>
        /// Renders the current scene height inside this biome's world bounds into a temporary texture.
        /// </summary>
        /// <returns>
        /// A newly created RFloat render texture at <see cref="baseResolution"/> containing scene height data. If no
        /// manager can be resolved, the texture is returned unchanged after allocation.
        /// </returns>
        /// <remarks>
        /// The texture is typically consumed only as a temporary graph input during biome cache generation and should be
        /// disposed by the caller or the input provider that created it.
        /// </remarks>
        public virtual RenderTexture RenderSceneHeightMap()
        {
            RenderTexture sceneHeightMap = new RenderTexture(m_baseResolution, m_baseResolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            sceneHeightMap.name = $"{gameObject.name} - Scene Height Map";
            sceneHeightMap.wrapMode = TextureWrapMode.Clamp;
            sceneHeightMap.filterMode = FilterMode.Bilinear;
            sceneHeightMap.enableRandomWrite = true;
            sceneHeightMap.antiAliasing = 1;
            sceneHeightMap.Create();
            GraphicsUtils.ClearWithZeros(sceneHeightMap);

            VistaManager vm = this.GetVistaManagerInstance();
            if (vm != null)
            {
                vm.CollectSceneHeight(sceneHeightMap, worldBounds);
            }

            return sceneHeightMap;
        }

        /// <summary>
        /// Ensures the biome instance has a persistent GUID before serialization.
        /// </summary>
        /// <remarks>
        /// The GUID is used by <see cref="Graph.LPBInputProvider"/> to reconnect serialized helper objects back to the live
        /// biome instance after reload.
        /// </remarks>
        public void OnBeforeSerialize()
        {
            if (string.IsNullOrEmpty(m_guid))
            {
                m_guid = Utilities.GenerateId();
            }
        }

        /// <summary>
        /// Receives Unity's deserialization callback.
        /// </summary>
        /// <remarks>
        /// No post-deserialization repair is currently required here. Runtime reconnection is handled lazily through the
        /// biome GUID when helper objects query <see cref="allInstances"/>.
        /// </remarks>
        public void OnAfterDeserialize()
        {
        }
    }
}
#endif


