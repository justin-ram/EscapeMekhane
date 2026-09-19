#if VISTA
#if GRIFFIN
using Pinwheel.Griffin;
using Pinwheel.Vista.Diagnostics;
using Pinwheel.Vista.Graph;
using Pinwheel.Vista.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista.PolarisTerrain
{
    /// <summary>
    /// Represents polaris tile utilities.
    /// </summary>
    public class PolarisTileUtilities
    {
        private const int SNAPSHOT_TREE_INSTANCES_PER_FRAME = 8192;
        private const int SNAPSHOT_GRASS_INSTANCES_PER_FRAME = 8192;

        private static readonly System.Func<GGrassPatch, List<GGrassInstance>> GET_GRASS_PATCH_INSTANCES =
            CreateGrassPatchInstancesGetter();

        private static readonly string POLARIS_HEIGHT_MAP_OUTPUT_SHADER_NAME = "Hidden/Vista/PolarisHeightMapOutput";
        private static readonly int HEIGHT_MAP = Shader.PropertyToID("_HeightMap");
        private static readonly int NEW_HEIGHT_DATA = Shader.PropertyToID("_NewHeightData");
        private static readonly int NEW_HOLE_DATA = Shader.PropertyToID("_NewHoleData");
        private static readonly int NEW_DENSITY_DATA = Shader.PropertyToID("_NewDensityData");
        private static readonly int PASS_OUTPUT_HEIGHT = 0;
        private static readonly int PASS_OUTPUT_HOLE = 1;
        private static readonly int PASS_OUTPUT_MESH_DENSITY = 2;
        private static readonly int PASS_COLLECT_SCENE_HEIGHT = 3;
        private static readonly int PASS_CAPTURE_MESH_DENSITY = 4;
        private static readonly int PASS_CAPTURE_HOLE = 5;

#if __MICROSPLAT_POLARIS__
        private const string MICROSPLAT_PER_TEXTURE_UV_KEYWORD = "_PERTEXUVSCALEOFFSET";
#endif

        /// <summary>
        /// Sets height map.
        /// </summary>
        /// <param name="currentHeightMap">Current height map value.</param>
        /// <param name="srcHeightData">Src height data value.</param>
        /// <param name="destHeightMap">Dest height map value.</param>
        public static void SetHeightMap(Texture currentHeightMap, RenderTexture srcHeightData, RenderTexture destHeightMap)
        {
            Material mat = new Material(ShaderUtilities.Find(POLARIS_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            mat.SetTexture(HEIGHT_MAP, currentHeightMap);
            mat.SetTexture(NEW_HEIGHT_DATA, srcHeightData);
            Drawing.DrawQuad(destHeightMap, mat, PASS_OUTPUT_HEIGHT);
            Object.DestroyImmediate(mat);
        }

        /// <summary>
        /// Sets hole map.
        /// </summary>
        /// <param name="currentHeightMap">Current height map value.</param>
        /// <param name="srcHoleData">Src hole data value.</param>
        /// <param name="destHeightMap">Dest height map value.</param>
        public static void SetHoleMap(Texture currentHeightMap, RenderTexture srcHoleData, RenderTexture destHeightMap)
        {
            Material mat = new Material(ShaderUtilities.Find(POLARIS_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            mat.SetTexture(HEIGHT_MAP, currentHeightMap);
            mat.SetTexture(NEW_HOLE_DATA, srcHoleData);
            Drawing.DrawQuad(destHeightMap, mat, PASS_OUTPUT_HOLE);
            Object.DestroyImmediate(mat);
        }

        /// <summary>
        /// Sets mesh density map.
        /// </summary>
        /// <param name="currentHeightMap">Current height map value.</param>
        /// <param name="srcDensityData">Src density data value.</param>
        /// <param name="destHeightMap">Dest height map value.</param>
        public static void SetMeshDensityMap(Texture currentHeightMap, RenderTexture srcDensityData, RenderTexture destHeightMap)
        {
            Material mat = new Material(ShaderUtilities.Find(POLARIS_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            mat.SetTexture(HEIGHT_MAP, currentHeightMap);
            mat.SetTexture(NEW_DENSITY_DATA, srcDensityData);
            Drawing.DrawQuad(destHeightMap, mat, PASS_OUTPUT_MESH_DENSITY);
            Object.DestroyImmediate(mat);
        }

        /// <summary>
        /// Decodes and draw height map.
        /// </summary>
        /// <param name="targetRt">Target rt value.</param>
        /// <param name="heightMap">Height map texture input.</param>
        /// <param name="quads">Collection of quad values.</param>
        public static void DecodeAndDrawHeightMap(RenderTexture targetRt, Texture heightMap, Vector2[] quads)
        {
            Material mat = new Material(ShaderUtilities.Find(POLARIS_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            mat.SetTexture(HEIGHT_MAP, heightMap);
            Drawing.DrawQuad(targetRt, quads, mat, PASS_COLLECT_SCENE_HEIGHT);
            Object.DestroyImmediate(mat);
        }

        /// <summary>
        /// Decodes Polaris's packed RG-B-A geometry texture into Vista snapshot channels.
        /// </summary>
        /// <param name="snapshot">Biome data that takes ownership of the captured render textures.</param>
        /// <param name="terrainData">Polaris terrain data containing the packed geometry texture.</param>
        public static void CaptureGeometryMaps(BiomeData snapshot, GTerrainData terrainData)
        {
            Texture packedHeightMap = terrainData.Geometry.HeightMap;
            int resolution = packedHeightMap.width;
            snapshot.heightMap = GraphicsUtils.CreateBlankRT(resolution, RenderTextureFormat.RFloat);
            snapshot.meshDensityMap = GraphicsUtils.CreateBlankRT(resolution, RenderTextureFormat.RFloat);
            snapshot.holeMap = GraphicsUtils.CreateBlankRT(resolution, RenderTextureFormat.RFloat);

            Material mat = new Material(ShaderUtilities.Find(POLARIS_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            try
            {
                mat.SetTexture(HEIGHT_MAP, packedHeightMap);
                Drawing.DrawQuad(snapshot.heightMap, mat, PASS_COLLECT_SCENE_HEIGHT);
                Drawing.DrawQuad(snapshot.meshDensityMap, mat, PASS_CAPTURE_MESH_DENSITY);
                Drawing.DrawQuad(snapshot.holeMap, mat, PASS_CAPTURE_HOLE);
            }
            finally
            {
                Object.DestroyImmediate(mat);
            }
        }

        /// <summary>
        /// Copies a Polaris albedo map into the Vista snapshot channel.
        /// </summary>
        /// <param name="snapshot">Biome data that takes ownership of the captured render textures.</param>
        /// <param name="terrainData">Polaris terrain data containing the optional albedo map.</param>
        public static void CaptureAlbedoMap(BiomeData snapshot, GTerrainData terrainData)
        {
            if (!terrainData.Shading.HasAlbedoMap)
                return;

            snapshot.albedoMap = GraphicsUtils.CloneToRenderTexture(
                terrainData.Shading.AlbedoMap, RenderTextureFormat.ARGB32);
        }

        /// <summary>
        /// Copies a Polaris metallic map into the Vista snapshot channel.
        /// </summary>
        /// <param name="snapshot">Biome data that takes ownership of the captured render textures.</param>
        /// <param name="terrainData">Polaris terrain data containing the optional metallic map.</param>
        public static void CaptureMetallicMap(BiomeData snapshot, GTerrainData terrainData)
        {
            if (!terrainData.Shading.HasMetallicMap)
                return;

            snapshot.metallicMap = GraphicsUtils.CloneToRenderTexture(
                terrainData.Shading.MetallicMap, RenderTextureFormat.ARGB32);
        }

        /// <summary>
        /// Captures Polaris splat-control channels into Vista snapshot layers.
        /// </summary>
        /// <param name="snapshot">Biome data that takes ownership of the captured render textures.</param>
        /// <param name="terrainData">Polaris terrain data containing the packed splat control maps.</param>
        public static void CaptureLayerWeights(
            BiomeData snapshot,
            GTerrainData terrainData,
            TransientResourceRegistry transientResources)
        {
            List<TerrainLayer> terrainLayers = ConstructTerrainLayersFromPrototypes(terrainData);
            CaptureLayerWeights(snapshot, terrainData, terrainLayers, transientResources);
        }

#if __MICROSPLAT_POLARIS__
        /// <summary>
        /// Captures MicroSplat control channels into Vista snapshot layers.
        /// </summary>
        public static void CaptureLayerWeights(
            BiomeData snapshot,
            GTerrainData terrainData,
            JBooth.MicroSplat.MicroSplatObject microSplat,
            TransientResourceRegistry transientResources)
        {
            List<TerrainLayer> terrainLayers =
                ConstructTerrainLayersFromMSTextureEntries(terrainData, microSplat);
            CaptureLayerWeights(snapshot, terrainData, terrainLayers, transientResources);
        }
#endif

        private static void CaptureLayerWeights(
            BiomeData snapshot,
            GTerrainData terrainData,
            List<TerrainLayer> terrainLayers,
            TransientResourceRegistry transientResources)
        {
            if (terrainLayers.Count == 0)
                return;

            for (int i = 0; i < terrainLayers.Count; ++i)
            {
                transientResources.RegisterDestroyLater(terrainLayers[i]);
            }

            AlphaMapsCombiner combiner = new AlphaMapsCombiner();
            for (int i = 0; i < terrainLayers.Count; ++i)
            {
                Texture splatControl = terrainData.Shading.GetSplatControlOrDefault(i / 4);
                RenderTexture layerWeight = combiner.ExtractChannel(
                    splatControl, i % 4, terrainData.Shading.SplatControlResolution);
                snapshot.AddTextureLayer(terrainLayers[i], layerWeight);
            }
        }

        private static List<TerrainLayer> ConstructTerrainLayersFromPrototypes(GTerrainData terrainData)
        {
            GSplatPrototypeGroup splatGroup = terrainData.Shading.Splats;
            if (splatGroup == null)
                return new List<TerrainLayer>();

            List<TerrainLayer> terrainLayers = new List<TerrainLayer>(splatGroup.Prototypes.Count);
            for (int i = 0; i < splatGroup.Prototypes.Count; ++i)
            {
                TerrainLayer terrainLayer = (TerrainLayer)splatGroup.Prototypes[i];
                terrainLayers.Add(terrainLayer);
            }
            return terrainLayers;
        }

#if __MICROSPLAT_POLARIS__
        private static List<TerrainLayer> ConstructTerrainLayersFromMSTextureEntries(
            GTerrainData terrainData,
            JBooth.MicroSplat.MicroSplatObject microSplat)
        {
            JBooth.MicroSplat.TextureArrayConfig config =
                terrainData.Shading.MicroSplatTextureArrayConfig;
            if (config == null)
                return new List<TerrainLayer>();

            JBooth.MicroSplat.MicroSplatPropData propertyData =
                microSplat != null ? microSplat.propData : null;
            bool hasPerTextureUv = microSplat != null &&
                microSplat.keywordSO != null &&
                microSplat.keywordSO.IsKeywordEnabled(MICROSPLAT_PER_TEXTURE_UV_KEYWORD) &&
                propertyData != null;

            List<TerrainLayer> terrainLayers =
                new List<TerrainLayer>(config.sourceTextures.Count);
            for (int i = 0; i < config.sourceTextures.Count; ++i)
            {
                JBooth.MicroSplat.TextureArrayConfig.TextureEntry entry =
                    config.sourceTextures[i];
                TerrainLayer terrainLayer = new TerrainLayer();

                if (entry != null)
                {
                    terrainLayer.diffuseTexture = entry.diffuse;
                    terrainLayer.normalMapTexture = entry.normal;
                }

                if (hasPerTextureUv && i < propertyData.maxTextures)
                {
                    Color perTextureUv = propertyData.GetValue(i, 0);
                    terrainLayer.tileSize = ConvertMicroSplatRepeatToTileSize(
                        new Vector2(perTextureUv.r, perTextureUv.g),
                        terrainData.Geometry.Size);
                    terrainLayer.tileOffset = new Vector2(perTextureUv.b, perTextureUv.a);
                }
                else
                {
                    terrainLayer.tileSize = Vector2.one;
                    terrainLayer.tileOffset = Vector2.zero;
                }

                terrainLayers.Add(terrainLayer);
            }
            return terrainLayers;
        }

        private static Vector2 ConvertMicroSplatRepeatToTileSize(
            Vector2 repeat,
            Vector3 terrainSize)
        {
            float repeatX = Mathf.Max(Mathf.Abs(repeat.x), 0.001f);
            float repeatY = Mathf.Max(Mathf.Abs(repeat.y), 0.001f);
            return new Vector2(terrainSize.x / repeatX, terrainSize.z / repeatY);
        }

#endif

        /// <summary>
        /// Finds the control-map channel occupied by a terrain layer in the terrain's current shading configuration.
        /// </summary>
        /// <returns>The current layer index, or -1 when no equivalent layer is configured.</returns>
        public static int FindTerrainLayerIndex(GStylizedTerrain terrain, TerrainLayer terrainLayer)
        {
            if (terrain == null || terrain.TerrainData == null || terrainLayer == null)
                return -1;

            GTerrainData terrainData = terrain.TerrainData;
#if __MICROSPLAT_POLARIS__
            if (terrainData.Shading.ShadingSystem == GShadingSystem.MicroSplat)
            {
                JBooth.MicroSplat.TextureArrayConfig config =
                    terrainData.Shading.MicroSplatTextureArrayConfig;
                if (config == null)
                    return -1;

                for (int i = 0; i < config.sourceTextures.Count; ++i)
                {
                    JBooth.MicroSplat.TextureArrayConfig.TextureEntry entry = config.sourceTextures[i];
                    if (entry != null && entry.diffuse == terrainLayer.diffuseTexture)
                        return i;
                }
                return -1;
            }
#endif

            GSplatPrototypeGroup splatGroup = terrainData.Shading.Splats;
            if (splatGroup == null)
                return -1;

            for (int i = 0; i < splatGroup.Prototypes.Count; ++i)
            {
                GSplatPrototype prototype = splatGroup.Prototypes[i];
                if (prototype != null && prototype.Texture == terrainLayer.diffuseTexture)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Tests whether two terrain layers represent the same texture entry for the active Polaris shading system.
        /// </summary>
        public static bool IsEquivalent(
            GTerrainData terrainData,
            TerrainLayer first,
            TerrainLayer second)
        {
            if (terrainData == null || first == null || second == null)
            {
                return false;
            }
            if (first == second)
            {
                return true;
            }

#if __MICROSPLAT_POLARIS__
            if (terrainData.Shading.ShadingSystem == GShadingSystem.MicroSplat)
            {
                return first.diffuseTexture == second.diffuseTexture;
            }
#endif

            GSplatPrototype prototype = (GSplatPrototype)first;
            return prototype.Equals(second);
        }

        /// <summary>
        /// Tests whether two Vista tree templates produce the same Polaris tree prototype.
        /// Variant arrays are excluded because one captured template represents one exact native prototype.
        /// </summary>
        public static bool IsEquivalent(TreeTemplate first, TreeTemplate second)
        {
            return first != null &&
                second != null &&
                first.prefab == second.prefab &&
                first.baseScale == second.baseScale &&
                first.baseRotation == second.baseRotation &&
                first.shadowCastingMode == second.shadowCastingMode &&
                first.receiveShadow == second.receiveShadow &&
                first.billboard == second.billboard &&
                first.billboardShadowCastingMode == second.billboardShadowCastingMode &&
                first.billboardReceiveShadow == second.billboardReceiveShadow &&
                first.keepPrefabLayer == second.keepPrefabLayer &&
                first.layer == second.layer &&
                first.pivotOffset == second.pivotOffset;
        }

        /// <summary>
        /// Progressively captures Polaris tree prototypes and instances into Vista snapshot buffers.
        /// </summary>
        public static IEnumerator CaptureTreesProgressive(
            BiomeData snapshot,
            GTerrainData terrainData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal)
        {
            GTreePrototypeGroup treeGroup = terrainData.Foliage.Trees;
            if (treeGroup == null || treeGroup.Prototypes.Count == 0)
                yield break;

            List<GTreePrototype> prototypes = treeGroup.Prototypes;
            List<GTreeInstance> instances = terrainData.Foliage.TreeInstances;
            List<TreeTemplate> templates = new List<TreeTemplate>(prototypes.Count);
            List<InstanceSample>[] samplesByPrototype = new List<InstanceSample>[prototypes.Count];

            for (int i = 0; i < prototypes.Count; ++i)
            {
                TreeTemplate template = ScriptableObject.CreateInstance<TreeTemplate>();
                CopyToTreeTemplate(prototypes[i], template);
                templates.Add(template);
                samplesByPrototype[i] = new List<InstanceSample>();
            }

            for (int i = 0; i < templates.Count; ++i)
            {
                transientResources.RegisterDestroyLater(templates[i]);
            }

            for (int i = 0; i < instances.Count; ++i)
            {
                GTreeInstance instance = instances[i];
                if (instance.PrototypeIndex >= 0 && instance.PrototypeIndex < prototypes.Count)
                {
                    samplesByPrototype[instance.PrototypeIndex].Add(new InstanceSample()
                    {
                        isValid = 1,
                        position = instance.Position,
                        verticalScale = instance.Scale.y,
                        horizontalScale = instance.Scale.x,
                        rotationY = instance.Rotation.eulerAngles.y
                    });
                }

                if ((i + 1) % SNAPSHOT_TREE_INSTANCES_PER_FRAME == 0)
                {
                    yield return null;
                    if (cancellationSignal?.isCancellationRequested == true)
                        yield break;
                }
            }

            for (int i = 0; i < prototypes.Count; ++i)
            {
                List<InstanceSample> samples = samplesByPrototype[i];
                int paddedSampleCount = Utilities.MultipleOf8(Mathf.Max(1, samples.Count));
                while (samples.Count < paddedSampleCount)
                {
                    samples.Add(default);
                }

                ComputeBuffer buffer = GraphicsUtils.CreateSampleBuffer<InstanceSample>(samples.Count);
                buffer.SetData(samples);
                snapshot.AddTree(templates[i], buffer);
                yield return null;
                if (cancellationSignal?.isCancellationRequested == true)
                    yield break;
            }
        }

        /// <summary>
        /// Copies the Polaris tree-prototype fields represented by Vista into a tree template.
        /// </summary>
        public static void CopyToTreeTemplate(GTreePrototype source, TreeTemplate destination)
        {
            destination.prefab = source.Prefab;
            destination.baseScale = source.BaseScale;
            destination.baseRotation = source.BaseRotation;
            destination.shadowCastingMode = source.ShadowCastingMode;
            destination.receiveShadow = source.ReceiveShadow;
            destination.billboard = source.Billboard;
            destination.billboardShadowCastingMode = source.BillboardShadowCastingMode;
            destination.billboardReceiveShadow = source.BillboardReceiveShadow;
            destination.keepPrefabLayer = source.KeepPrefabLayer;
            destination.layer = source.Layer;
            destination.pivotOffset = source.PivotOffset;
        }

        /// <summary>
        /// Progressively captures Polaris grass prototypes and patch instances into Vista snapshot buffers.
        /// </summary>
        public static IEnumerator CaptureDetailInstancesProgressive(
            BiomeData snapshot,
            GTerrainData terrainData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal)
        {
            GGrassPrototypeGroup grassGroup = terrainData.Foliage.Grasses;
            if (grassGroup == null || grassGroup.Prototypes.Count == 0)
                yield break;

            List<GGrassPrototype> prototypes = grassGroup.Prototypes;
            List<DetailTemplate> templates = new List<DetailTemplate>(prototypes.Count);
            List<InstanceSample>[] samplesByPrototype = new List<InstanceSample>[prototypes.Count];
            for (int i = 0; i < prototypes.Count; ++i)
            {
                DetailTemplate template = ScriptableObject.CreateInstance<DetailTemplate>();
                CopyToDetailTemplate(prototypes[i], template);
                templates.Add(template);
                samplesByPrototype[i] = new List<InstanceSample>();
            }
            for (int i = 0; i < templates.Count; ++i)
            {
                transientResources.RegisterDestroyLater(templates[i]);
            }

            int processedInstanceCount = 0;
            GGrassPatch[] patches = terrainData.Foliage.GrassPatches;
            for (int patchIndex = 0; patchIndex < patches.Length; ++patchIndex)
            {
                List<GGrassInstance> instances = GET_GRASS_PATCH_INSTANCES(patches[patchIndex]);
                for (int i = 0; i < instances.Count; ++i)
                {
                    GGrassInstance instance = instances[i];
                    if (instance.PrototypeIndex >= 0 && instance.PrototypeIndex < prototypes.Count)
                    {
                        samplesByPrototype[instance.PrototypeIndex].Add(new InstanceSample()
                        {
                            isValid = 1,
                            position = instance.Position,
                            verticalScale = instance.Scale.y,
                            horizontalScale = instance.Scale.x,
                            rotationY = instance.Rotation.eulerAngles.y
                        });
                    }

                    processedInstanceCount += 1;
                    if (processedInstanceCount % SNAPSHOT_GRASS_INSTANCES_PER_FRAME == 0)
                    {
                        yield return null;
                        if (cancellationSignal?.isCancellationRequested == true)
                            yield break;
                    }
                }
            }

            for (int i = 0; i < prototypes.Count; ++i)
            {
                List<InstanceSample> samples = samplesByPrototype[i];
                int paddedSampleCount = Utilities.MultipleOf8(Mathf.Max(1, samples.Count));
                while (samples.Count < paddedSampleCount)
                {
                    samples.Add(default);
                }

                ComputeBuffer buffer = GraphicsUtils.CreateSampleBuffer<InstanceSample>(samples.Count);
                buffer.SetData(samples);
                snapshot.AddDetailInstance(templates[i], buffer);
                yield return null;
                if (cancellationSignal?.isCancellationRequested == true)
                    yield break;
            }
        }

        /// <summary>
        /// Adds normalized grass instances directly to their Polaris grass patches.
        /// </summary>
        /// <remarks>
        /// Polaris' general foliage API searches every patch for every instance. Vista already receives normalized
        /// positions, so it can calculate the patch index directly and update each populated patch once.
        /// </remarks>
        public static void AddGrassInstances(GTerrainData terrainData, List<GGrassInstance> instances)
        {
            if (terrainData == null)
                throw new System.ArgumentNullException(nameof(terrainData));
            if (instances == null)
                throw new System.ArgumentNullException(nameof(instances));
            if (instances.Count == 0)
                return;

            GFoliage foliage = terrainData.Foliage;
            int gridSize = foliage.PatchGridSize;
            GGrassPatch[] patches = foliage.GrassPatches;
            List<GGrassInstance>[] buckets = new List<GGrassInstance>[patches.Length];
            for (int i = 0; i < buckets.Length; ++i)
            {
                buckets[i] = new List<GGrassInstance>();
            }

            for (int i = 0; i < instances.Count; ++i)
            {
                GGrassInstance instance = instances[i];
                Vector3 position = instance.Position;
                if (position.x < 0 || position.x > 1 || position.z < 0 || position.z > 1)
                    continue;

                int patchX = Mathf.Min(Mathf.FloorToInt(position.x * gridSize), gridSize - 1);
                int patchZ = Mathf.Min(Mathf.FloorToInt(position.z * gridSize), gridSize - 1);
                int patchIndex = GUtilities.To1DIndex(patchX, patchZ, gridSize);

                buckets[patchIndex].Add(instance);
            }

            for (int i = 0; i < buckets.Length; ++i)
            {
                List<GGrassInstance> bucket = buckets[i];
                if (bucket.Count > 0)
                {
                    patches[i].AddInstances(bucket);
                }
            }
        }

        /// <summary>
        /// Copies the Polaris grass-prototype fields represented by Vista into a detail template.
        /// </summary>
        public static void CopyToDetailTemplate(GGrassPrototype source, DetailTemplate destination)
        {
            if (source.Shape == GGrassShape.DetailObject)
            {
                destination.renderMode = DetailRenderMode.VertexLit;
                destination.prefab = source.Prefab;
            }
            else
            {
                destination.renderMode = source.IsBillboard
                    ? DetailRenderMode.GrassBillboard
                    : DetailRenderMode.Grass;
                destination.texture = source.Texture;
                destination.textureBasedGrassShape = ToTextureBasedGrassShape(source.Shape);
            }

            destination.primaryColor = source.Color;
            destination.secondaryColor = source.Color;
            destination.minWidth = source.Size.x;
            destination.maxWidth = source.Size.x;
            destination.minHeight = source.Size.y;
            destination.maxHeight = source.Size.y;
            destination.pivotOffset = source.PivotOffset;
            destination.bendFactor = source.BendFactor;
            destination.layer = source.Layer;
            destination.alignToSurface = source.AlignToSurface;
            destination.castShadow = source.ShadowCastingMode;
            destination.receiveShadow = source.ReceiveShadow;
        }

        /// <summary>
        /// Tests whether two Vista detail templates produce the same Polaris grass prototype.
        /// Unity-only size variation, density, and variant arrays are excluded from native prototype identity.
        /// </summary>
        public static bool IsEquivalent(DetailTemplate first, DetailTemplate second)
        {
            if (first == null || second == null ||
                first.renderMode != second.renderMode ||
                first.primaryColor != second.primaryColor ||
                first.minWidth != second.minWidth ||
                first.minHeight != second.minHeight ||
                first.pivotOffset != second.pivotOffset ||
                first.bendFactor != second.bendFactor ||
                first.layer != second.layer ||
                first.alignToSurface != second.alignToSurface ||
                first.castShadow != second.castShadow ||
                first.receiveShadow != second.receiveShadow)
            {
                return false;
            }

            if (first.renderMode == DetailRenderMode.VertexLit)
            {
                return first.prefab == second.prefab;
            }

            return first.texture == second.texture &&
                DetailTemplate.ToPolarisGrassShape(first.textureBasedGrassShape) ==
                DetailTemplate.ToPolarisGrassShape(second.textureBasedGrassShape);
        }

        private static DetailTemplate.TextureBasedGrassShape ToTextureBasedGrassShape(GGrassShape shape)
        {
            switch (shape)
            {
                case GGrassShape.Quad:
                    return DetailTemplate.TextureBasedGrassShape.Quad;
                case GGrassShape.Cross:
                    return DetailTemplate.TextureBasedGrassShape.Cross;
                case GGrassShape.TriCross:
                    return DetailTemplate.TextureBasedGrassShape.TriCross;
                case GGrassShape.Clump:
                    return DetailTemplate.TextureBasedGrassShape.Clump;
                default:
                    return DetailTemplate.TextureBasedGrassShape.Default;
            }
        }

        private static System.Func<GGrassPatch, List<GGrassInstance>> CreateGrassPatchInstancesGetter()
        {
            System.Reflection.PropertyInfo property = typeof(GGrassPatch).GetProperty(
                "Instances",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            System.Reflection.MethodInfo getter = property.GetGetMethod(true);
            return (System.Func<GGrassPatch, List<GGrassInstance>>)getter.CreateDelegate(
                typeof(System.Func<GGrassPatch, List<GGrassInstance>>));
        }

        public static void CaptureSnapshotResults(BiomeData snapshot, GTerrainData terrainData)
        {
            VistaDebugger.OpenScope("Capture Polaris Snapshot", DebugScopeType.Custom);
            VistaDebugger.CaptureString(
                "Resolutions",
                $"Height: {terrainData.Geometry.HeightMapResolution}, Splat: {terrainData.Shading.SplatControlResolution}");
            VistaDebugger.Capture("Height Map", snapshot.heightMap);
            VistaDebugger.Capture("Mesh Density Map", snapshot.meshDensityMap);
            VistaDebugger.Capture("Hole Map", snapshot.holeMap);
            VistaDebugger.Capture("Albedo Map", snapshot.albedoMap);
            VistaDebugger.Capture("Metallic Map", snapshot.metallicMap);

            List<TerrainLayer> terrainLayers = new List<TerrainLayer>();
            List<RenderTexture> layerWeights = new List<RenderTexture>();
            snapshot.GetLayerWeights(terrainLayers, layerWeights);
            VistaDebugger.CaptureString(
                "Terrain Layer Order",
                string.Join("\n", terrainLayers.ConvertAll(layer => layer != null ? layer.name : "Unnamed Layer")));
            VistaDebugger.Capture("Layer Weights", layerWeights);

            List<TreeTemplate> treeTemplates = new List<TreeTemplate>();
            List<ComputeBuffer> treeBuffers = new List<ComputeBuffer>();
            snapshot.GetTrees(treeTemplates, treeBuffers);
            for (int i = 0; i < treeBuffers.Count; ++i)
            {
                string prefabName = treeTemplates[i].prefab != null
                    ? treeTemplates[i].prefab.name
                    : "Missing Prefab";
                VistaDebugger.Capture(
                    $"Tree: {prefabName}", treeBuffers[i], DebugBufferInterpretation.InstanceSample);
            }

            List<DetailTemplate> detailTemplates = new List<DetailTemplate>();
            List<ComputeBuffer> detailBuffers = new List<ComputeBuffer>();
            snapshot.GetDetailInstances(detailTemplates, detailBuffers);
            for (int i = 0; i < detailBuffers.Count; ++i)
            {
                DetailTemplate template = detailTemplates[i];
                string prototypeName = template.renderMode == DetailRenderMode.VertexLit
                    ? (template.prefab != null ? template.prefab.name : "Missing Prefab")
                    : (template.texture != null ? template.texture.name : "Missing Texture");
                VistaDebugger.Capture(
                    $"Detail Instance: {prototypeName}",
                    detailBuffers[i],
                    DebugBufferInterpretation.InstanceSample);
            }
            VistaDebugger.CloseScope();
        }
    }
}
#endif
#endif
