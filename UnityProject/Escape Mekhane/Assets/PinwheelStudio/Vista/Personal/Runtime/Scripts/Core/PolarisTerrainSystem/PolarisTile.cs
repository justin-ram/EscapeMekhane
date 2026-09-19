#if VISTA
#if GRIFFIN
using Pinwheel.Griffin;
using Pinwheel.Vista.Graphics;
using Pinwheel.Vista.Graph;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TerrainMaterialTemplate = Pinwheel.Griffin.GRuntimeSettings.TerrainRenderingSettings.TerrainMaterialTemplate;

namespace Pinwheel.Vista.PolarisTerrain
{
    /// <summary>
    /// Connects a Polaris <see cref="GStylizedTerrain"/> to Vista so it can be generated, updated, and protected by Terrain Seals.
    /// </summary>
    /// <remarks>
    /// Add this component beside a Polaris terrain and assign it to a <see cref="VistaManager"/> through
    /// <see cref="managerId"/>. During generation, Vista can replace geometry, shading maps, splat layers, trees, grass,
    /// and spawned objects. The tile supports both Polaris and MicroSplat layer-weight capture for Terrain Seals and
    /// exposes generic outputs to custom processors through its callback events.
    ///
    /// Most population methods implement Vista's tile-backend contracts and are normally called by
    /// <see cref="VistaManager"/> rather than directly by user scripts.
    /// </remarks>
    [RequireComponent(typeof(GStylizedTerrain))]
    [ExecuteInEditMode]
    [AddComponentMenu("Vista/Polaris Tile")]
    public class PolarisTile : MonoBehaviour, ITile, ITileSnapshotProvider, IGeometryPopulator, IAlbedoMapPopulator, IMetallicMapPopulator, ILayerWeightsPopulator, ITreePopulator, IDetailInstancePopulator, IObjectPopulator, IGenericTexturePopulator, IGenericBufferPopulator, ISceneHeightProvider, ISceneTextureWeightProvider, ISerializationCallbackReceiver
    {
        private const string GENERATED_SPLAT_GROUP_NAME = "~GeneratedSplatGroup";
        private const string GENERATED_TREE_GROUP_NAME = "~GeneratedTreeGroup";
        private const string GENERATED_GRASS_GROUP_NAME = "~GeneratedGrassGroup";

        // Number of splat layers each Polaris built in splat shader variant can render.
        private const int SPLATS4_MAX_LAYERS = 4;
        private const int SPLATS8_MAX_LAYERS = 8;

        // Documentation on switching the terrain material to a different shader variant.
        private const string CHANGE_MATERIAL_DOC_URL = "https://docs.pinwheelstud.io/polaris/3/docs/basic-concepts/change-terrain-material.html";
        // Documentation on integrating the terrain with MicroSplat.
        private const string MICROSPLAT_INTEGRATION_DOC_URL = "https://docs.pinwheelstud.io/polaris/3/docs/ecosystem-and-integrations/microsplat.html";

        /// <summary>
        /// Occurs when Vista delivers labelled generic texture outputs for this tile to custom processors.
        /// </summary>
        /// <remarks>
        /// The textures remain owned by the active generation pass. Copy any data that must outlive the callback.
        /// </remarks>
        public event PopulateGenericTexturesHandler populateGenericTexturesCallback;

        /// <summary>
        /// Occurs when Vista delivers labelled generic buffer outputs for this tile to custom processors.
        /// </summary>
        /// <remarks>
        /// The buffers remain owned by the active generation pass. Copy any data that must outlive the callback.
        /// </remarks>
        public event PopulateGenericBuffersHandler populateGenericBuffersCallback;

        /// <summary>
        /// Occurs after an object-output prefab has been spawned and placed on this tile.
        /// </summary>
        /// <remarks>
        /// Use this callback to initialize or track each generated object after Vista applies its position, rotation, and scale.
        /// </remarks>
        public event PopulatePrefabHandler populatePrefabInstanceCallback;

        [SerializeField]
        private string m_managerId;
        /// <summary>
        /// Gets or sets the identifier of the <see cref="VistaManager"/> that generates this tile.
        /// </summary>
        /// <remarks>
        /// Set this to the target Manager's <see cref="VistaManager.id"/>. A Manager discovers this tile only when the two
        /// identifiers match.
        /// </remarks>
        public string managerId
        {
            get
            {
                return m_managerId;
            }
            set
            {
                m_managerId = value;
            }
        }

        /// <summary>
        /// Gets the Polaris terrain that receives Vista's generated data.
        /// </summary>
        /// <remarks>The reference is resolved when the component is enabled.</remarks>
        public GStylizedTerrain terrain { get; private set; }

        /// <summary>
        /// Gets the world-space bounds Vista uses to determine where this tile participates in generation.
        /// </summary>
        public Bounds worldBounds
        {
            get
            {
                return terrain.Bounds;
            }
        }

        /// <summary>
        /// Gets or sets the terrain's vertical range represented by normalized height values.
        /// </summary>
        public float maxHeight
        {
            get
            {
                if (terrain != null && terrain.TerrainData != null)
                {
                    return terrain.TerrainData.Geometry.Height;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                if (terrain != null && terrain.TerrainData != null)
                {
                    terrain.TerrainData.Geometry.Height = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the resolution of Polaris's packed geometry map used for height, mesh density, and holes.
        /// </summary>
        public int heightMapResolution
        {
            get
            {
                if (terrain != null && terrain.TerrainData != null)
                {
                    return terrain.TerrainData.Geometry.HeightMapResolution;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                if (terrain != null && terrain.TerrainData != null)
                {
                    terrain.TerrainData.Geometry.HeightMapResolution = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the shared resolution used for splat controls, albedo, and metallic maps.
        /// </summary>
        /// <remarks>Setting this value updates all three Polaris shading-map resolutions.</remarks>
        public int textureResolution
        {
            get
            {
                if (terrain != null && terrain.TerrainData != null)
                {
                    return terrain.TerrainData.Shading.SplatControlResolution;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                if (terrain != null && terrain.TerrainData != null)
                {
                    terrain.TerrainData.Shading.SplatControlResolution = value;
                    terrain.TerrainData.Shading.AlbedoMapResolution = value;
                    terrain.TerrainData.Shading.MetallicMapResolution = value;
                }
            }
        }

        /// <summary>
        /// Gets the unsupported detail-density resolution for this instance-based grass backend.
        /// </summary>
        /// <remarks>
        /// Polaris stores grass as instances rather than a density map, so this property always returns zero and ignores assignments.
        /// </remarks>
        public int detailDensityMapResolution
        {
            get
            {
                return 0;
            }
            set
            {

            }
        }

        [SerializeField]
        private List<GSplatPrototype> m_splatPrototypesSerialized;
        [SerializeField]
        private List<GTreePrototype> m_treePrototypesSerialized;
        [SerializeField]
        private List<GGrassPrototype> m_grassPrototypesSerialized;

        // Number of splat layers populated during the current apply pass, evaluated once in OnAfterApplyingData.
        // Not serialized, it only describes the pass in flight.
        private int m_populatedSplatLayerCount;
        // True when a non cleared albedo map was populated during the current apply pass. Non splat shaders
        // read the albedo map, so it is the alternative path to the screen when no splat variant is active.
        private bool m_albedoMapPopulated;

        [SerializeField]
        private TerrainLayer[] m_terrainLayers;

        /// <summary>
        /// Gets the consolidated terrain-layer array most recently converted into Polaris splat prototypes by Vista.
        /// </summary>
        public TerrainLayer[] terrainLayers
        {
            get
            {
                return m_terrainLayers;
            }
        }

        private void OnEnable()
        {
            terrain = GetComponent<GStylizedTerrain>();
            TileRegistry.Register(this);
            VistaManager.collectTiles += OnCollectTiles;
            DeserializePrototypes();
        }

        private void OnDisable()
        {
            TileRegistry.Unregister(this);
            VistaManager.collectTiles -= OnCollectTiles;
        }

        /// <summary>
        /// Starts a generation-scoped capture of the current Polaris terrain data for Terrain Seal preservation.
        /// </summary>
        /// <remarks>
        /// The snapshot includes packed geometry, allocated albedo and metallic maps, terrain-layer weights, trees, and
        /// grass instances in Vista's standard <see cref="BiomeData"/> representation. Layer capture follows the active
        /// Polaris or MicroSplat shading system. Fresh template adapters are registered with
        /// <paramref name="transientResources"/>; the request's caller owns the returned <see cref="BiomeData"/> and must
        /// dispose it after the tile pass.
        /// </remarks>
        /// <param name="transientResources">The tile-pass registry that owns temporary capture adapters.</param>
        /// <param name="cancellationSignal">An optional signal checked between progressive capture stages.</param>
        /// <returns>A request that completes after the current terrain state has been captured.</returns>
        public BiomeDataRequest CaptureSnapshot(
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal = null)
        {
            if (terrain == null)
            {
                terrain = GetComponent<GStylizedTerrain>();
            }
            if (terrain == null || terrain.TerrainData == null)
            {
                throw new System.InvalidOperationException(
                    "Cannot capture a Polaris terrain snapshot without terrain data.");
            }

            BiomeDataRequest request = new BiomeDataRequest();
            request.data = new BiomeData();
            IEnumerator captureRoutine = CaptureSnapshotProgressive(
                request, terrain.TerrainData, transientResources, cancellationSignal);
            CoroutineUtility.StartCoroutine(captureRoutine, exceptionHandler: request.Fail);
            return request;
        }

        /// <inheritdoc/>
        public bool IsEquivalent(TerrainLayer first, TerrainLayer second)
        {
            GTerrainData terrainData = terrain != null ? terrain.TerrainData : null;
            return PolarisTileUtilities.IsEquivalent(terrainData, first, second);
        }

        /// <inheritdoc/>
        public bool IsEquivalent(TreeTemplate first, TreeTemplate second)
        {
            return PolarisTileUtilities.IsEquivalent(first, second);
        }

        /// <inheritdoc/>
        public bool IsEquivalent(DetailTemplate first, DetailTemplate second)
        {
            return PolarisTileUtilities.IsEquivalent(first, second);
        }

        private IEnumerator CaptureSnapshotProgressive(
            BiomeDataRequest request,
            GTerrainData terrainData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal)
        {
            PolarisTileUtilities.CaptureGeometryMaps(request.data, terrainData);
            yield return null;
            if (cancellationSignal?.isCancellationRequested == true)
            {
                request.Complete();
                yield break;
            }

            PolarisTileUtilities.CaptureAlbedoMap(request.data, terrainData);
            PolarisTileUtilities.CaptureMetallicMap(request.data, terrainData);
#if __MICROSPLAT_POLARIS__
            if (terrainData.Shading.ShadingSystem == GShadingSystem.MicroSplat)
            {
                JBooth.MicroSplat.MicroSplatObject microSplat =
                    terrain.GetComponent<JBooth.MicroSplat.MicroSplatObject>();
                PolarisTileUtilities.CaptureLayerWeights(
                    request.data, terrainData, microSplat, transientResources);
            }
            else
#endif
            {
                PolarisTileUtilities.CaptureLayerWeights(
                    request.data, terrainData, transientResources);
            }
            if (cancellationSignal?.isCancellationRequested == true)
            {
                request.Complete();
                yield break;
            }

            yield return PolarisTileUtilities.CaptureTreesProgressive(
                request.data, terrainData, transientResources, cancellationSignal);
            if (cancellationSignal?.isCancellationRequested == true)
            {
                request.Complete();
                yield break;
            }

            yield return PolarisTileUtilities.CaptureDetailInstancesProgressive(
                request.data, terrainData, transientResources, cancellationSignal);
            if (cancellationSignal?.isCancellationRequested == true)
            {
                request.Complete();
                yield break;
            }

            PolarisTileUtilities.CaptureSnapshotResults(request.data, terrainData);
            request.Complete();
        }

        private void OnCollectTiles(VistaManager manager, Collector<ITile> tiles)
        {
            if (string.Equals(manager.id, m_managerId) && terrain != null && terrain.TerrainData != null)
            {
                tiles.Add(this);
            }
        }

        /// <summary>
        /// Begins one Vista apply pass and resets the shading-output state used for compatibility diagnostics.
        /// </summary>
        public void OnBeforeApplyingData()
        {
            m_populatedSplatLayerCount = 0;
            m_albedoMapPopulated = false;
        }

        /// <summary>
        /// Finishes one Vista apply pass, reports incompatible Polaris shading setup, and marks terrain data dirty in the Editor.
        /// </summary>
        public void OnAfterApplyingData()
        {
            // Evaluated here rather than inside the populate methods so the whole apply pass is visible,
            // and so at most one warning is raised per pass.
            WarnIfShadingSetupMisconfigured();

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(terrain.TerrainData);
#endif
        }

        /// <summary>
        /// Gets the data this tile owns, so a caller can persist it to assets.
        /// </summary>
        /// <returns>
        /// The custom material, generated geometry-data container, and terrain data in persistence order, omitting unavailable entries.
        /// </returns>
        public IEnumerable<UnityEngine.Object> GetDataAssets()
        {
            if (terrain == null || terrain.TerrainData == null)
            {
                yield break;
            }
            // The cloned material is saved before the terrain data, so it becomes its own asset instead of being
            // embedded into the data asset.
            Material material = terrain.TerrainData.Shading.CustomMaterial;
            if (material != null)
            {
                yield return material;
            }
            GTerrainGeneratedData generatedDataContainer = terrain.TerrainData.GeometryData;
            if (generatedDataContainer != null)
            {
                yield return generatedDataContainer;
            }
            yield return terrain.TerrainData;
        }

        /// <summary>
        /// Writes a Vista height map into the height channel of Polaris's packed geometry texture.
        /// </summary>
        /// <param name="heightMap">Generated normalized height values for this tile.</param>
        /// <remarks>The mesh-density and hole channels already stored in the packed texture are preserved.</remarks>
        public void PopulateHeightMap(RenderTexture heightMap)
        {
            int resolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture destHeightMap = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            PolarisTileUtilities.SetHeightMap(terrain.TerrainData.Geometry.HeightMap, heightMap, destHeightMap);

            GraphicsUtils.ReadRenderTexture(destHeightMap, terrain.TerrainData.Geometry.HeightMap);
            destHeightMap.Release();
            Object.DestroyImmediate(destHeightMap);
        }

        /// <summary>
        /// Clears the Polaris height channel by writing a zero-valued Vista height map.
        /// </summary>
        public void ClearHeightMap()
        {
            int resolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture zeroHeightMap = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            GraphicsUtils.ClearWithZeros(zeroHeightMap);
            PopulateHeightMap(zeroHeightMap);
            RenderTexture.ReleaseTemporary(zeroHeightMap);
        }

        /// <summary>
        /// Writes a Vista hole map into the hole channel of Polaris's packed geometry texture.
        /// </summary>
        /// <param name="holeMap">Generated hole values using Vista's hole-map convention.</param>
        /// <remarks>The height and mesh-density channels already stored in the packed texture are preserved.</remarks>
        public void PopulateHoleMap(RenderTexture holeMap)
        {
            int resolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture destHeightMap = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            PolarisTileUtilities.SetHoleMap(terrain.TerrainData.Geometry.HeightMap, holeMap, destHeightMap);

            GraphicsUtils.ReadRenderTexture(destHeightMap, terrain.TerrainData.Geometry.HeightMap);
            destHeightMap.Release();
            Object.DestroyImmediate(destHeightMap);
        }

        /// <summary>
        /// Clears the Polaris hole channel by writing a zero-valued Vista hole map.
        /// </summary>
        public void ClearHoleMap()
        {
            int resolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture zeroHoleMap = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            GraphicsUtils.ClearWithZeros(zeroHoleMap);
            PopulateHoleMap(zeroHoleMap);
            RenderTexture.ReleaseTemporary(zeroHoleMap);
        }

        /// <summary>
        /// Writes a Vista mesh-density map into the density channel of Polaris's packed geometry texture.
        /// </summary>
        /// <param name="meshDensityMap">Generated mesh-density values for this tile.</param>
        /// <remarks>The height and hole channels already stored in the packed texture are preserved.</remarks>
        public void PopulateMeshDensityMap(RenderTexture meshDensityMap)
        {
            int resolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture destHeightMap = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            PolarisTileUtilities.SetMeshDensityMap(terrain.TerrainData.Geometry.HeightMap, meshDensityMap, destHeightMap);

            GraphicsUtils.ReadRenderTexture(destHeightMap, terrain.TerrainData.Geometry.HeightMap);
            destHeightMap.Release();
            Object.DestroyImmediate(destHeightMap);
        }

        /// <summary>
        /// Clears the Polaris mesh-density channel by writing a zero-valued Vista density map.
        /// </summary>
        public void ClearMeshDensityMap()
        {
            int resolution = terrain.TerrainData.Geometry.HeightMapResolution;
            RenderTexture zeroDensityMap = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear);
            GraphicsUtils.ClearWithZeros(zeroDensityMap);
            PopulateMeshDensityMap(zeroDensityMap);
            RenderTexture.ReleaseTemporary(zeroDensityMap);
        }

        /// <summary>
        /// Marks the complete Polaris geometry region dirty so generated geometry is rebuilt.
        /// </summary>
        public void UpdateGeometry()
        {
            terrain.TerrainData.Geometry.SetRegionDirty(GCommon.UnitRect);
            terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.Geometry);
        }

        /// <summary>
        /// Matches this terrain's edges with its neighboring Polaris terrains.
        /// </summary>
        public void MatchSeams()
        {
            terrain.MatchEdges();
        }

        /// <summary>
        /// Writes a generated albedo map into Polaris shading data and marks shading dirty.
        /// </summary>
        /// <param name="albedoMap">Generated albedo colors for this tile.</param>
        /// <remarks>
        /// The map is populated whenever Vista supplies it, regardless of whether the current terrain shader displays it.
        /// </remarks>
        public void PopulateAlbedoMap(RenderTexture albedoMap)
        {
            int resolution = terrain.TerrainData.Shading.AlbedoMapResolution;
            if (resolution == albedoMap.width && resolution == albedoMap.height)
            {
                GraphicsUtils.ReadRenderTexture(albedoMap, terrain.TerrainData.Shading.AlbedoMap);
            }
            else
            {
                RenderTexture scaledAlbedo = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Drawing.Blit(albedoMap, scaledAlbedo);

                GraphicsUtils.ReadRenderTexture(scaledAlbedo, terrain.TerrainData.Shading.AlbedoMap);

                scaledAlbedo.Release();
                Object.DestroyImmediate(scaledAlbedo);
            }

            m_albedoMapPopulated = true;
            terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.Shading);
        }

        /// <summary>
        /// Clears the Polaris albedo map by writing a zero-valued texture.
        /// </summary>
        public void ClearAlbedoMap()
        {
            int resolution = terrain.TerrainData.Shading.AlbedoMapResolution;
            RenderTexture zeroAlbedoMap = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            GraphicsUtils.ClearWithZeros(zeroAlbedoMap);
            PopulateAlbedoMap(zeroAlbedoMap);
            RenderTexture.ReleaseTemporary(zeroAlbedoMap);

            // PopulateAlbedoMap just flagged the map as populated, but a cleared albedo map renders nothing,
            // so it must not count as a usable path to the screen.
            m_albedoMapPopulated = false;
        }

        /// <summary>
        /// Writes a generated metallic-smoothness map into Polaris shading data and marks shading dirty.
        /// </summary>
        /// <param name="metallicMap">Generated metallic and smoothness values for this tile.</param>
        public void PopulateMetallicMap(RenderTexture metallicMap)
        {
            int resolution = terrain.TerrainData.Shading.MetallicMapResolution;
            if (resolution == metallicMap.width && resolution == metallicMap.height)
            {
                GraphicsUtils.ReadRenderTexture(metallicMap, terrain.TerrainData.Shading.MetallicMap);
            }
            else
            {
                RenderTexture scaledMetallicMap = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                Drawing.Blit(metallicMap, scaledMetallicMap);

                GraphicsUtils.ReadRenderTexture(scaledMetallicMap, terrain.TerrainData.Shading.MetallicMap);

                scaledMetallicMap.Release();
                Object.DestroyImmediate(scaledMetallicMap);
            }
            terrain.TerrainData.SetDirty(GTerrainData.DirtyFlags.Shading);
        }

        /// <summary>
        /// Clears the Polaris metallic map by writing a zero-valued texture.
        /// </summary>
        public void ClearMetallicMap()
        {
            int resolution = terrain.TerrainData.Shading.MetallicMapResolution;
            RenderTexture zeroMetallicMap = RenderTexture.GetTemporary(resolution, resolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            GraphicsUtils.ClearWithZeros(zeroMetallicMap);
            PopulateMetallicMap(zeroMetallicMap);
            RenderTexture.ReleaseTemporary(zeroMetallicMap);
        }

        /// <summary>
        /// Replaces the tile's Polaris splat prototypes and control maps with Vista's generated terrain-layer output.
        /// </summary>
        /// <param name="layers">Terrain layers corresponding to weight textures.</param>
        /// <param name="weights">Layer-weight textures.</param>
        /// <remarks>
        /// Duplicate layer references are consolidated before each distinct <see cref="TerrainLayer"/> is converted to a
        /// Polaris splat prototype. The combined weights are packed into Polaris control maps. When MicroSplat integration
        /// is active, its texture-entry capacity is expanded to accommodate the populated layer count.
        /// </remarks>
        public void PopulateLayerWeights(List<TerrainLayer> layers, List<RenderTexture> weights)
        {
            List<TerrainLayer> distinctLayers;
            List<RenderTexture> alphaMaps;
            int resolution = textureResolution;

            AlphaMapsCombiner combiner = new AlphaMapsCombiner();
            combiner.CombineAndMerge(
                layers, weights, resolution, out distinctLayers, out alphaMaps);

            m_terrainLayers = distinctLayers.ToArray();

            List<GSplatPrototype> prototypes = new List<GSplatPrototype>();
            foreach (TerrainLayer l in m_terrainLayers)
            {
                prototypes.Add((GSplatPrototype)l);
            }
            GSplatPrototypeGroup splatGroup = CreateSplatGroup(prototypes);
            SetSplatGroup(splatGroup);

            m_populatedSplatLayerCount = m_terrainLayers.Length;

#if __MICROSPLAT_POLARIS__
            JBooth.MicroSplat.TextureArrayConfig cfg = terrain.TerrainData.Shading.MicroSplatTextureArrayConfig;
            while (cfg != null && cfg.sourceTextures.Count < m_terrainLayers.Length)
            {
                TerrainLayer layer = m_terrainLayers[cfg.sourceTextures.Count];
                cfg.sourceTextures.Add(CreateMicroSplatTextureEntry(layer, cfg));
            }
#endif

            for (int i = 0; i < alphaMaps.Count; ++i)
            {
                Texture2D alphaMap = terrain.TerrainData.Shading.GetSplatControl(i);
                GraphicsUtils.ReadRenderTexture(alphaMaps[i], alphaMap);
            }

            for (int i = 0; i < alphaMaps.Count; ++i)
            {
                alphaMaps[i].Release();
                Object.DestroyImmediate(alphaMaps[i]);
            }
        }

#if __MICROSPLAT_POLARIS__
        private static JBooth.MicroSplat.TextureArrayConfig.TextureEntry CreateMicroSplatTextureEntry(
            TerrainLayer layer,
            JBooth.MicroSplat.TextureArrayConfig config)
        {
            JBooth.MicroSplat.TextureArrayConfig.TextureEntry entry =
                new JBooth.MicroSplat.TextureArrayConfig.TextureEntry();
            if (layer == null)
                return entry;

            entry.diffuse = layer.diffuseTexture;
            entry.normal = layer.normalMapTexture;
            entry.metal = layer.maskMapTexture;
            entry.metalChannel = JBooth.MicroSplat.TextureArrayConfig.TextureChannel.R;
            entry.height = layer.maskMapTexture;
            entry.heightChannel = JBooth.MicroSplat.TextureArrayConfig.TextureChannel.B;
            entry.smoothness = layer.maskMapTexture;
            entry.smoothnessChannel = JBooth.MicroSplat.TextureArrayConfig.TextureChannel.A;
            entry.ao = layer.maskMapTexture;
            entry.aoChannel = JBooth.MicroSplat.TextureArrayConfig.TextureChannel.G;

            if (layer.maskMapTexture != null)
            {
                config.allTextureChannelAO = JBooth.MicroSplat.TextureArrayConfig.AllTextureChannel.G;
                config.allTextureChannelHeight = JBooth.MicroSplat.TextureArrayConfig.AllTextureChannel.B;
                config.allTextureChannelSmoothness = JBooth.MicroSplat.TextureArrayConfig.AllTextureChannel.A;
            }
            return entry;
        }
#endif

        /// <summary>
        /// Clears all Polaris splat weights and assigned terrain layers from this tile.
        /// </summary>
        public void ClearLayerWeights()
        {
            m_terrainLayers = System.Array.Empty<TerrainLayer>();
            m_populatedSplatLayerCount = 0;
            GSplatPrototypeGroup splatGroup = CreateSplatGroup(new List<GSplatPrototype>());
            SetSplatGroup(splatGroup);
            terrain.TerrainData.Shading.RemoveSplatControlMaps();
        }

        private void SetSplatGroup(GSplatPrototypeGroup splatGroup)
        {
            GSplatPrototypeGroup currentGroup = terrain.TerrainData.Shading.Splats;
            if (currentGroup != null && string.Equals(currentGroup.name, GENERATED_SPLAT_GROUP_NAME))
            {
                Object.DestroyImmediate(currentGroup);
            }
            terrain.TerrainData.Shading.Splats = splatGroup;
        }

        private GSplatPrototypeGroup CreateSplatGroup(List<GSplatPrototype> prototypes)
        {
            GSplatPrototypeGroup splatGroup = ScriptableObject.CreateInstance<GSplatPrototypeGroup>();
            splatGroup.name = GENERATED_SPLAT_GROUP_NAME;
            splatGroup.Prototypes = prototypes;
            return splatGroup;
        }

        /// <summary>
        /// Checks the shading data populated during this apply pass against the terrain current shader, and routes
        /// to the path that owns the channel that shader renders.
        /// Only data the user clearly meant to be visible is reported. Populating nothing means texturing has not
        /// been set up yet, which is never warned about.
        /// </summary>
        private void WarnIfShadingSetupMisconfigured()
        {
            // No shading channel was populated at all, so the user has not expressed any texturing intent yet.
            // A blank or black terrain is expected at this stage and is not worth interrupting them over.
            if (m_populatedSplatLayerCount < 1 && !m_albedoMapPopulated)
            {
                return;
            }

            // MicroSplat drives its own shading, so the built in shader rules below do not apply.
            if (terrain.TerrainData.Shading.ShadingSystem != GShadingSystem.Polaris)
            {
                return;
            }

            TerrainMaterialTemplate template;
            if (!TryGetCurrentMaterialTemplate(out template))
            {
                // The shader is not one of the built in Polaris materials, so there is no way to know which
                // channel it samples. Staying quiet beats guessing wrong about a custom shader.
                return;
            }

            if (template.texturingModel == GTexturingModel.Splat)
            {
                WarnIfSplatMisconfigured(template.splatsModel);
            }
            else if (template.texturingModel == GTexturingModel.ColorMap)
            {
                WarnIfAlbedoMisconfigured();
            }

            // GradientLookup and VertexColor derive their color without sampling anything populated here, so they
            // always render something. There is no unreachable data to report for them.
        }

        /// <summary>
        /// Splat misconfigured path. Runs when a splat shader variant is active, so the splat layers are the
        /// channel being rendered. Warns when the graph produced an albedo map instead of splat layers, or when
        /// it produced more layers than the active variant can render.
        /// </summary>
        /// <param name="currentSplatsModel">Splats model of the active shader variant.</param>
        private void WarnIfSplatMisconfigured(GSplatsModel currentSplatsModel)
        {
            // The shader renders splat layers but the graph produced none, so there is no capacity to check.
            if (m_populatedSplatLayerCount < 1)
            {
                // Only warn when an albedo map was populated instead. That shows texturing was intended while
                // the chosen variant cannot show it. With neither channel populated texturing has not started.
                if (m_albedoMapPopulated)
                {
                    Debug.LogWarning(
                        $"Terrain '{terrain.name}' uses a splat shader variant, which renders splat layers, " +
                        $"but Vista populated an albedo map and no splat layers, so none of it will be rendered. " +
                        $"Add a texture layer output to the graph, or switch the terrain to the Color Map shader variant. " +
                        $"See {CHANGE_MATERIAL_DOC_URL} for the steps.", terrain);
                }
                return;
            }

            // Beyond eight layers no built in Polaris splat variant is enough, MicroSplat is required.
            if (m_populatedSplatLayerCount > SPLATS8_MAX_LAYERS)
            {
                Debug.LogWarning(
                    $"Vista populated {m_populatedSplatLayerCount} splat layers onto terrain '{terrain.name}', " +
                    $"but built in Polaris shaders render at most {SPLATS8_MAX_LAYERS} layers. " +
                    $"Integrate the terrain with MicroSplat to render this many layers. " +
                    $"See {MICROSPLAT_INTEGRATION_DOC_URL} for the steps.", terrain);
                return;
            }

            // Splats4 and Splats4Normals4 render only four layers, so five to eight layers need Splats8.
            bool isFourLayerVariant = currentSplatsModel == GSplatsModel.Splats4 || currentSplatsModel == GSplatsModel.Splats4Normals4;
            bool exceedsFourLayerVariant = m_populatedSplatLayerCount > SPLATS4_MAX_LAYERS && m_populatedSplatLayerCount <= SPLATS8_MAX_LAYERS;
            if (isFourLayerVariant && exceedsFourLayerVariant)
            {
                Debug.LogWarning(
                    $"Vista populated {m_populatedSplatLayerCount} splat layers onto terrain '{terrain.name}', " +
                    $"but its current shader variant '{currentSplatsModel}' renders only {SPLATS4_MAX_LAYERS} layers. " +
                    $"Switch the terrain to the Splats8 shader variant, or reduce the layer count to {SPLATS4_MAX_LAYERS}. " +
                    $"See {CHANGE_MATERIAL_DOC_URL} for the steps.", terrain);
            }
        }

        /// <summary>
        /// Albedo misconfigured path. Runs when the Color Map shader variant is active, so the albedo map is the
        /// channel being rendered. Warns when the graph produced splat layers instead of an albedo map.
        /// </summary>
        private void WarnIfAlbedoMisconfigured()
        {
            // The albedo map is the channel this shader samples, so a populated one is exactly right. Any splat
            // layers alongside it are simply ignored, which is not worth reporting.
            if (m_albedoMapPopulated)
            {
                return;
            }

            // Only warn when splat layers were populated instead. That shows texturing was intended while the
            // chosen variant cannot show it. With neither channel populated texturing has not started.
            if (m_populatedSplatLayerCount < 1)
            {
                return;
            }

            string layerWord = m_populatedSplatLayerCount != 1 ? "layers" : "layer";

            // Switching to a splat variant only helps up to eight layers, beyond that MicroSplat is the answer.
            if (m_populatedSplatLayerCount > SPLATS8_MAX_LAYERS)
            {
                Debug.LogWarning(
                    $"Terrain '{terrain.name}' uses the Color Map shader variant, which renders the albedo map, " +
                    $"but Vista populated {m_populatedSplatLayerCount} splat {layerWord} and no albedo map, so none of it will be rendered. " +
                    $"Built in Polaris splat shaders render at most {SPLATS8_MAX_LAYERS} layers, so integrate the terrain with MicroSplat. " +
                    $"See {MICROSPLAT_INTEGRATION_DOC_URL} for the steps.", terrain);
            }
            else
            {
                Debug.LogWarning(
                    $"Terrain '{terrain.name}' uses the Color Map shader variant, which renders the albedo map, " +
                    $"but Vista populated {m_populatedSplatLayerCount} splat {layerWord} and no albedo map, so none of it will be rendered. " +
                    $"Add an albedo output to the graph, or switch the terrain to a splat shader variant that supports " +
                    $"at least {m_populatedSplatLayerCount} {layerWord}. " +
                    $"See {CHANGE_MATERIAL_DOC_URL} for the steps.", terrain);
            }
        }

        /// <summary>
        /// Resolves the built in Polaris material template matching the terrain current shader.
        /// </summary>
        /// <param name="template">The resolved template when the current shader is a known built in material.</param>
        /// <returns>True when the current shader matches a built in Polaris material, false otherwise.</returns>
        private bool TryGetCurrentMaterialTemplate(out TerrainMaterialTemplate template)
        {
            template = null;

            Material material = terrain.TerrainData.Shading.CustomMaterial;
            if (material == null || material.shader == null)
            {
                return false;
            }

            return GRuntimeSettings.Instance.terrainRendering.FindMaterialTemplate(material.shader, GCommon.CurrentRenderPipeline, out template);
        }

        /// <summary>
        /// Replaces this tile's Polaris tree prototypes and instances with Vista's generated tree output.
        /// </summary>
        /// <param name="templates">Tree templates paired with <paramref name="buffers"/> by index.</param>
        /// <param name="buffers">Generated instance buffers containing normalized placement, rotation, and scale.</param>
        /// <remarks>
        /// Repeated template references share one Polaris prototype. Every valid instance uses that template's primary prefab.
        /// </remarks>
        public void PopulateTrees(List<TreeTemplate> templates, List<ComputeBuffer> buffers)
        {
            List<TreeTemplate> distinctTemplates = templates.Distinct().ToList();
            List<GTreePrototype> prototypes = new List<GTreePrototype>();
            int[] protoIndices = new int[distinctTemplates.Count];
            for (int i = 0; i < distinctTemplates.Count; ++i)
            {
                TreeTemplate template = distinctTemplates[i];
                GTreePrototype prototype = CreateTreePrototypeFromTemplate(template);
                protoIndices[i] = prototype != null ? prototypes.Count : -1;
                if (prototype != null)
                    prototypes.Add(prototype);
            }

            List<GTreeInstance> instances = new List<GTreeInstance>();
            for (int i = 0; i < buffers.Count; ++i)
            {
                ComputeBuffer buffer = buffers[i];
                int prototypeIndex = protoIndices[distinctTemplates.IndexOf(templates[i])];
                if (prototypeIndex >= 0)
                    ParseTreeInstances(instances, buffer, prototypeIndex);
            }

            GTreePrototypeGroup treeGroup = CreateTreeGroup(prototypes);
            SetTreeGroup(treeGroup);
            terrain.TerrainData.Foliage.TreeInstances = instances;
            terrain.TerrainData.Foliage.SetTreeRegionDirty(new Rect(0, 0, 1, 1));
            terrain.UpdateTreesPosition();
            terrain.TerrainData.Foliage.ClearTreeDirtyRegions();
        }

        /// <summary>
        /// Clears all Polaris tree prototypes and instances from this tile.
        /// </summary>
        public void ClearTrees()
        {
            GTreePrototypeGroup treeGroup = CreateTreeGroup(new List<GTreePrototype>());
            SetTreeGroup(treeGroup);
            terrain.TerrainData.Foliage.ClearTreeInstances();
            terrain.TerrainData.Foliage.SetTreeRegionDirty(new Rect(0, 0, 1, 1));
            terrain.TerrainData.Foliage.ClearTreeDirtyRegions();
        }

        private GTreePrototype CreateTreePrototypeFromTemplate(TreeTemplate template)
        {
            if (template.prefab == null)
                return null;

            GTreePrototype proto = new GTreePrototype();
            proto.Prefab = template.prefab;
            proto.BaseScale = template.baseScale;
            proto.BaseRotation = template.baseRotation;

            proto.ShadowCastingMode = template.shadowCastingMode;
            proto.ReceiveShadow = template.receiveShadow;

            proto.Billboard = template.billboard;
            proto.BillboardShadowCastingMode = template.billboardShadowCastingMode;
            proto.BillboardReceiveShadow = template.billboardReceiveShadow;

            proto.KeepPrefabLayer = template.keepPrefabLayer;
            proto.Layer = template.layer;
            proto.PivotOffset = template.pivotOffset;

            return proto;
        }

        private void ParseTreeInstances(List<GTreeInstance> instances, ComputeBuffer buffer, int prototypeIndex)
        {
            if (buffer.count % InstanceSample.SIZE != 0)
            {
                Debug.LogError("Cannot parse instance sample buffer");
                return;
            }

            InstanceSample[] data = new InstanceSample[buffer.count / InstanceSample.SIZE];
            buffer.GetData(data);

            foreach (InstanceSample t in data)
            {
                if (t.isValid <= 0)
                    continue;
                GTreeInstance tree = new GTreeInstance();
                tree.Position = t.position;
                tree.Rotation = Quaternion.Euler(0, t.rotationY, 0);
                tree.Scale = new Vector3(t.horizontalScale, t.verticalScale, t.horizontalScale);
                tree.PrototypeIndex = prototypeIndex;

                instances.Add(tree);
            }
        }

        private GTreePrototypeGroup CreateTreeGroup(List<GTreePrototype> prototypes)
        {
            GTreePrototypeGroup treeGroup = ScriptableObject.CreateInstance<GTreePrototypeGroup>();
            treeGroup.name = GENERATED_TREE_GROUP_NAME;
            treeGroup.Prototypes = prototypes;
            return treeGroup;
        }

        private void SetTreeGroup(GTreePrototypeGroup treeGroup)
        {
            GTreePrototypeGroup currentGroup = terrain.TerrainData.Foliage.Trees;
            if (currentGroup != null && string.Equals(currentGroup.name, GENERATED_TREE_GROUP_NAME, System.StringComparison.Ordinal))
            {
                Object.DestroyImmediate(currentGroup);
            }
            terrain.TerrainData.Foliage.Trees = treeGroup;
        }

        /// <summary>
        /// Replaces this tile's Polaris grass prototypes and instances with Vista's generated detail-instance output.
        /// </summary>
        /// <param name="templates">Detail templates paired with <paramref name="buffers"/> by index.</param>
        /// <param name="buffers">Generated grass-instance buffers containing normalized placement, rotation, and scale.</param>
        /// <remarks>
        /// Each detail template contributes one Polaris grass prototype from its primary prefab or texture. Existing grass
        /// is cleared, then valid instances are committed one source buffer at a time to bound temporary managed memory.
        /// </remarks>
        public void PopulateDetailInstance(List<DetailTemplate> templates, List<ComputeBuffer> buffers)
        {
            List<DetailTemplate> distinctTemplates = templates.Distinct().ToList();
            List<GGrassPrototype> prototypes = new List<GGrassPrototype>();
            int[] protoIndices = new int[distinctTemplates.Count];
            for (int i = 0; i < distinctTemplates.Count; ++i)
            {
                DetailTemplate template = distinctTemplates[i];
                GGrassPrototype prototype = CreateGrassPrototypeFromTemplate(template);
                protoIndices[i] = prototype != null ? prototypes.Count : -1;
                if (prototype != null)
                    prototypes.Add(prototype);
            }

            GGrassPrototypeGroup grassGroup = CreateGrassGroup(prototypes);
            terrain.TerrainData.Foliage.ClearGrassInstances();
            SetGrassGroup(grassGroup);

            // Commit one source buffer at a time so temporary storage grows only to the largest
            // buffer's valid population instead of retaining every grass instance for the tile.
            List<GGrassInstance> instances = new List<GGrassInstance>();
            for (int i = 0; i < buffers.Count; ++i)
            {
                instances.Clear();
                ComputeBuffer buffer = buffers[i];
                int prototypeIndex = protoIndices[distinctTemplates.IndexOf(templates[i])];
                if (prototypeIndex >= 0)
                    ParseGrassInstances(instances, buffer, prototypeIndex);

                if (instances.Count > 0)
                {
                    PolarisTileUtilities.AddGrassInstances(
                        terrain.TerrainData,
                        instances);
                }
            }

            terrain.TerrainData.Foliage.SetGrassRegionDirty(new Rect(0, 0, 1, 1));
            terrain.UpdateGrassPatches();
            terrain.TerrainData.Foliage.ClearGrassDirtyRegions();
        }

        /// <summary>
        /// Clears all Polaris grass prototypes and grass instances from this tile.
        /// </summary>
        public void ClearDetailInstance()
        {
            GGrassPrototypeGroup grassGroup = CreateGrassGroup(new List<GGrassPrototype>());
            SetGrassGroup(grassGroup);
            terrain.TerrainData.Foliage.ClearGrassInstances();
            terrain.TerrainData.Foliage.SetGrassRegionDirty(new Rect(0, 0, 1, 1));
            terrain.TerrainData.Foliage.ClearGrassDirtyRegions();
        }

        private GGrassPrototype CreateGrassPrototypeFromTemplate(DetailTemplate template)
        {
            if (!template.IsValid())
                return null;

            GGrassPrototype proto = new GGrassPrototype();

            if (template.renderMode == DetailRenderMode.VertexLit)
            {
                proto.Shape = GGrassShape.DetailObject;
                proto.Prefab = template.prefab;
            }
            else
            {
                proto.Shape = DetailTemplate.ToPolarisGrassShape(template.textureBasedGrassShape);
                proto.Texture = template.texture;
            }

            proto.Color = template.primaryColor;
            proto.Size = new Vector3(template.minWidth, template.minHeight, template.minWidth);
            proto.PivotOffset = template.pivotOffset;
            proto.BendFactor = template.bendFactor;
            proto.Layer = template.layer;
            proto.AlignToSurface = template.alignToSurface;
            proto.ShadowCastingMode = template.castShadow;
            proto.ReceiveShadow = template.receiveShadow;
            proto.IsBillboard = template.renderMode == DetailRenderMode.GrassBillboard;

            return proto;
        }

        private GGrassPrototypeGroup CreateGrassGroup(List<GGrassPrototype> prototypes)
        {
            GGrassPrototypeGroup grassGroup = ScriptableObject.CreateInstance<GGrassPrototypeGroup>();
            grassGroup.name = GENERATED_GRASS_GROUP_NAME;
            grassGroup.Prototypes = prototypes;
            return grassGroup;
        }

        private void SetGrassGroup(GGrassPrototypeGroup grassGroup)
        {
            GGrassPrototypeGroup currentGroup = terrain.TerrainData.Foliage.Grasses;
            if (currentGroup != null && string.Equals(currentGroup.name, GENERATED_GRASS_GROUP_NAME, System.StringComparison.Ordinal))
            {
                Object.DestroyImmediate(currentGroup);
            }
            terrain.TerrainData.Foliage.Grasses = grassGroup;
        }

        private void ParseGrassInstances(List<GGrassInstance> instances, ComputeBuffer buffer, int prototypeIndex)
        {
            if (buffer.count % InstanceSample.SIZE != 0)
            {
                Debug.LogError("Cannot parse instance sample buffer");
                return;
            }

            InstanceSample[] data = new InstanceSample[buffer.count / InstanceSample.SIZE];
            buffer.GetData(data);

            foreach (InstanceSample t in data)
            {
                if (t.isValid <= 0)
                    continue;
                GGrassInstance tree = new GGrassInstance();
                tree.Position = t.position;
                tree.Rotation = Quaternion.Euler(0, t.rotationY, 0);
                tree.Scale = new Vector3(t.horizontalScale, t.verticalScale, t.horizontalScale);
                tree.PrototypeIndex = prototypeIndex;

                instances.Add(tree);
            }
        }

        /// <summary>
        /// Replaces Vista-owned spawned objects under this terrain with generated object instances.
        /// </summary>
        /// <param name="templates">Object templates paired with <paramref name="sampleBuffers"/> by index.</param>
        /// <param name="sampleBuffers">Generated object-instance buffers.</param>
        /// <param name="objectPopulateArgs">Options that control object spawning cadence.</param>
        /// <returns>A progressive task that completes after all eligible objects have been spawned.</returns>
        /// <remarks>
        /// The previous Vista-owned object hierarchy is removed first. Valid samples are projected onto the Polaris terrain,
        /// then their selected prefab, rotation, scale, and optional normal alignment are applied over multiple frames.
        /// </remarks>
        public ProgressiveTask PopulateObject(List<ObjectTemplate> templates, List<ComputeBuffer> sampleBuffers, VistaManager.ObjectPopulateArgs objectPopulateArgs)
        {
            ProgressiveTask task = new ProgressiveTask();
            CoroutineUtility.StartCoroutine(PopulateObjectProgressive(task, templates, sampleBuffers, objectPopulateArgs));
            return task;
        }

        /// <summary>
        /// Clears spawned object prefabs owned by this terrain tile.
        /// </summary>
        /// <returns>A completed task after the spawned-object hierarchy has been removed.</returns>
        public ProgressiveTask ClearObject()
        {
            string rootName = SpawnUtilities.ROOT_NAME;
            Transform existingRoot = terrain.transform.Find(rootName);
            if (existingRoot != null)
            {
                DestroyImmediate(existingRoot.gameObject);
            }

            ProgressiveTask task = new ProgressiveTask();
            task.Complete();
            return task;
        }

        private IEnumerator PopulateObjectProgressive(ProgressiveTask task, List<ObjectTemplate> templates, List<ComputeBuffer> sampleBuffers, VistaManager.ObjectPopulateArgs objectPopulateArgs)
        {
            string rootName = SpawnUtilities.ROOT_NAME;
            Transform existingRoot = terrain.transform.Find(rootName);
            if (existingRoot != null)
            {
                DestroyImmediate(existingRoot.gameObject);
            }

            Transform mainRoot = new GameObject(rootName).transform;
            mainRoot.parent = terrain.transform;
            mainRoot.localPosition = Vector3.zero;
            mainRoot.localRotation = Quaternion.identity;
            mainRoot.localScale = Vector3.one;

            List<ObjectTemplate> distinctTemplates = templates.Distinct().ToList();
            int[] templateIndices = new int[templates.Count];
            for (int i = 0; i < templates.Count; ++i)
            {
                templateIndices[i] = distinctTemplates.IndexOf(templates[i]);
            }

            CoroutineHandle[] coroutines = new CoroutineHandle[templates.Count];
            for (int i = 0; i < sampleBuffers.Count; ++i)
            {
                int tIndex = templateIndices[i];
                ObjectTemplate template = distinctTemplates[tIndex];
                CoroutineHandle c = CoroutineUtility.StartCoroutine(PopulateObjectProgressive(template, sampleBuffers[i], mainRoot, objectPopulateArgs));
                coroutines[i] = c;
            }

            foreach (CoroutineHandle c in coroutines)
            {
                yield return c.coroutine;
            }

            task.Complete();
            yield break;
        }

        private IEnumerator PopulateObjectProgressive(ObjectTemplate template, ComputeBuffer buffer, Transform mainRoot, VistaManager.ObjectPopulateArgs objectPopulateArgs)
        {
            if (buffer.count % InstanceSample.SIZE != 0)
            {
                Debug.LogError("Cannot parse tree sample buffer");
                yield break;
            }
            if (mainRoot == null)
            {
                yield break;
            }

            int instanceCount = buffer.count / InstanceSample.SIZE;
            InstanceSample[] samples = new InstanceSample[buffer.count / InstanceSample.SIZE];
            buffer.GetData(samples);

            string prefabRootName = $"~{template.name}";
            Transform prefabRoot = mainRoot.Find(prefabRootName);
            if (prefabRoot == null)
            {
                prefabRoot = new GameObject(prefabRootName).transform;
                prefabRoot.parent = mainRoot;
                prefabRoot.localPosition = Vector3.zero;
                prefabRoot.localRotation = Quaternion.identity;
                prefabRoot.localScale = Vector3.one;
            }

            Vector3 terrainSize = terrain.TerrainData.Geometry.Size;
            for (int i = 0; i < instanceCount; ++i)
            {
                InstanceSample sample = samples[i];
                if (sample.isValid == 0)
                    continue;
                RaycastHit hit;
                Vector3 normalizedPoint = new Vector3(sample.position.x, 0, sample.position.z);
                Vector3 worldPosition;
                if (terrain.Raycast(normalizedPoint, out hit))
                {
                    worldPosition = hit.point;
                }
                else
                {
                    worldPosition = terrain.transform.TransformPoint(new Vector3(terrainSize.x * sample.position.x, 0, terrainSize.z * sample.position.z));
                }

                GameObject prefab = template.prefab;

                Quaternion localRotation = Quaternion.Euler(0, sample.rotationY * Mathf.Rad2Deg, 0);
                Vector3 baseScale = prefab.transform.localScale;
                Vector3 localScale = new Vector3(sample.horizontalScale, sample.verticalScale, sample.horizontalScale);
                localScale.Scale(baseScale);

                if (template.alignToNormal)
                {
                    Vector3 normalVector = hit.normal;
                    float errorFactor = Random.Range(1 - template.normalAlignmentError, 1 + template.normalAlignmentError);
                    normalVector = Vector3.LerpUnclamped(Vector3.up, normalVector, errorFactor);
                    Quaternion alignmentRotation = Quaternion.FromToRotation(Vector3.up, normalVector);
                    localRotation *= alignmentRotation;
                }

                GameObject instance = SpawnUtilities.Spawn(prefab);
                instance.transform.parent = prefabRoot;
                instance.transform.position = worldPosition;
                instance.transform.localRotation = localRotation;
                instance.transform.localScale = localScale;
                populatePrefabInstanceCallback?.Invoke(this, instance);

                if (i % objectPopulateArgs.objectsPerFrame == 0)
                {
                    yield return null;
                }
            }

            yield break;
        }

        /// <summary>
        /// Delivers generic texture outputs to custom processors subscribed to <see cref="populateGenericTexturesCallback"/>.
        /// </summary>
        /// <param name="labels">Labels paired with <paramref name="textures"/> by index.</param>
        /// <param name="textures">Generic texture outputs produced for this tile.</param>
        public void PopulateGenericTextures(List<string> labels, List<RenderTexture> textures)
        {
            populateGenericTexturesCallback?.Invoke(labels, textures);
        }

        /// <summary>
        /// Delivers generic buffer outputs to custom processors subscribed to <see cref="populateGenericBuffersCallback"/>.
        /// </summary>
        /// <param name="labels">Labels paired with <paramref name="buffers"/> by index.</param>
        /// <param name="buffers">Generic buffer outputs produced for this tile.</param>
        public void PopulateGenericBuffers(List<string> labels, List<ComputeBuffer> buffers)
        {
            populateGenericBuffersCallback?.Invoke(labels, buffers);
        }

        private void SerializePrototypes()
        {
            if (terrain != null && terrain.TerrainData != null)
            {
                if (terrain.TerrainData.Shading.Splats != null)
                {
                    m_splatPrototypesSerialized = terrain.TerrainData.Shading.Splats.Prototypes;
                }
                if (terrain.TerrainData.Foliage.Trees != null)
                {
                    m_treePrototypesSerialized = terrain.TerrainData.Foliage.Trees.Prototypes;
                }
                if (terrain.TerrainData.Foliage.Grasses != null)
                {
                    m_grassPrototypesSerialized = terrain.TerrainData.Foliage.Grasses.Prototypes;
                }
            }
        }

        private void DeserializePrototypes()
        {
            if (terrain != null && terrain.TerrainData != null)
            {
                if (m_splatPrototypesSerialized != null)
                {
                    GSplatPrototypeGroup splatGroup = CreateSplatGroup(m_splatPrototypesSerialized);
                    SetSplatGroup(splatGroup);
                }
                if (m_treePrototypesSerialized != null)
                {
                    GTreePrototypeGroup treeGroup = CreateTreeGroup(m_treePrototypesSerialized);
                    SetTreeGroup(treeGroup);
                }
                if (m_grassPrototypesSerialized != null)
                {
                    GGrassPrototypeGroup grassGroup = CreateGrassGroup(m_grassPrototypesSerialized);
                    SetGrassGroup(grassGroup);
                }
            }
        }

        /// <summary>
        /// Copies the active generated splat, tree, and grass prototype lists into serialized component fields.
        /// </summary>
        /// <remarks>
        /// Unity invokes this callback so prototype data owned by generated in-memory Polaris groups survives serialization.
        /// </remarks>
        public void OnBeforeSerialize()
        {
            SerializePrototypes();
        }

        /// <summary>
        /// Receives Unity's post-deserialization callback.
        /// </summary>
        /// <remarks>
        /// Restoration is deferred until <c>OnEnable</c>, when the Polaris terrain reference is available, so this callback is intentionally empty.
        /// </remarks>
        public void OnAfterDeserialize()
        {

        }

        /// <summary>
        /// Draws this terrain's decoded height contribution into a shared scene-height render texture.
        /// </summary>
        /// <param name="targetRt">The destination texture representing the requested scene-height region.</param>
        /// <param name="requestedWorldRect">World-space rectangle represented by the destination texture.</param>
        /// <remarks>
        /// The terrain bounds are mapped into the requested rectangle before Polaris's packed height channel is decoded and drawn.
        /// </remarks>
        public void OnCollectSceneHeight(RenderTexture targetRt, Rect requestedWorldRect)
        {
            Bounds selfWorldBounds = worldBounds;
            Rect selfRect = new Rect(selfWorldBounds.min.x, selfWorldBounds.min.z, selfWorldBounds.size.x, selfWorldBounds.size.z);
            float minX = Utilities.InverseLerpUnclamped(requestedWorldRect.min.x, requestedWorldRect.max.x, selfRect.min.x);
            float maxX = Utilities.InverseLerpUnclamped(requestedWorldRect.min.x, requestedWorldRect.max.x, selfRect.max.x) + targetRt.texelSize.x;
            float minY = Utilities.InverseLerpUnclamped(requestedWorldRect.min.y, requestedWorldRect.max.y, selfRect.min.y);
            float maxY = Utilities.InverseLerpUnclamped(requestedWorldRect.min.y, requestedWorldRect.max.y, selfRect.max.y) + targetRt.texelSize.y;

            Vector2[] uvCorner = new Vector2[]
            {
                new Vector2(minX, minY),
                new Vector2(minX, maxY),
                new Vector2(maxX, maxY),
                new Vector2(maxX, minY)
            };

            Texture terrainHeightMap = terrain.TerrainData.Geometry.HeightMap;
            PolarisTileUtilities.DecodeAndDrawHeightMap(targetRt, terrainHeightMap, uvCorner);
        }

        public void OnCollectSceneTextureWeight(TerrainLayer terrainLayer, RenderTexture targetRt, Rect requestedWorldRect)
        {
            GTerrainData terrainData = terrain.TerrainData;
            int layerIndex = PolarisTileUtilities.FindTerrainLayerIndex(terrain, terrainLayer);
            if (layerIndex < 0)
                return;

            Bounds selfWorldBounds = worldBounds;
            Rect selfRect = new Rect(selfWorldBounds.min.x, selfWorldBounds.min.z, selfWorldBounds.size.x, selfWorldBounds.size.z);
            float minX = Utilities.InverseLerpUnclamped(requestedWorldRect.min.x, requestedWorldRect.max.x, selfRect.min.x);
            float maxX = Utilities.InverseLerpUnclamped(requestedWorldRect.min.x, requestedWorldRect.max.x, selfRect.max.x) + targetRt.texelSize.x;
            float minY = Utilities.InverseLerpUnclamped(requestedWorldRect.min.y, requestedWorldRect.max.y, selfRect.min.y);
            float maxY = Utilities.InverseLerpUnclamped(requestedWorldRect.min.y, requestedWorldRect.max.y, selfRect.max.y) + targetRt.texelSize.y;
            Vector2[] uvCorner = new Vector2[]
            {
                new Vector2(minX, minY),
                new Vector2(minX, maxY),
                new Vector2(maxX, maxY),
                new Vector2(maxX, minY)
            };

            AlphaMapsCombiner combiner = new AlphaMapsCombiner();
            combiner.ExtractChannel(
                terrainData.Shading.GetSplatControlOrDefault(layerIndex / 4),
                layerIndex % 4,
                targetRt,
                uvCorner);
        }
    }
}
#endif
#endif


