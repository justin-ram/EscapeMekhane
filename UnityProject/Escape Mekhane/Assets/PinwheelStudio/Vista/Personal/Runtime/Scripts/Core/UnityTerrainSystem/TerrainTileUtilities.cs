#if VISTA
using Pinwheel.Vista.Graphics;
using Pinwheel.Vista.Diagnostics;
using Pinwheel.Vista.Graph;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Pinwheel.Vista.UnityTerrain
{
    /// <summary>
    /// Represents terrain tile utilities.
    /// </summary>
    public class TerrainTileUtilities
    {
        private const int SNAPSHOT_ROWS_PER_FRAME = 128;
        private const int SNAPSHOT_TREE_INSTANCES_PER_FRAME = 8192;

        private static readonly string UNITY_HEIGHT_MAP_OUTPUT_SHADER_NAME = "Hidden/Vista/UnityHeightMapOutput";
        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int MAIN_TEX_TEXEL_SIZE = Shader.PropertyToID("_MainTex_TexelSize");
        private static readonly int PASS_HM_TO_UNITY = 0;
        private static readonly int PASS_COLLECT_SCENE_HEIGHT = 1;
        private static readonly int PASS_CAPTURE_HOLE_MAP = 2;

        /// <summary>
        /// Converts height map to unity.
        /// </summary>
        /// <param name="src">Src value.</param>
        /// <param name="output">Output value.</param>
        public static void ConvertHeightMapToUnity(RenderTexture src, RenderTexture output)
        {
            Material mat = new Material(ShaderUtilities.Find(UNITY_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            mat.SetTexture(MAIN_TEX, src);
            Drawing.DrawQuad(output, mat, PASS_HM_TO_UNITY);
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
            Material mat = new Material(ShaderUtilities.Find(UNITY_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            mat.SetTexture(MAIN_TEX, heightMap);
            mat.SetVector(MAIN_TEX_TEXEL_SIZE, new Vector4(heightMap.texelSize.x, heightMap.texelSize.y, heightMap.width, heightMap.height));
            Drawing.DrawQuad(targetRt, quads, mat, PASS_COLLECT_SCENE_HEIGHT);
            Object.DestroyImmediate(mat);
        }

        /// <summary>
        /// Copies Unity's surface-presence hole texture into Vista's hole-presence convention.
        /// </summary>
        /// <param name="targetRt">Destination single-channel Vista hole map.</param>
        /// <param name="holeMap">Unity Terrain hole texture, where one means terrain is present.</param>
        public static void CaptureHoleMap(RenderTexture targetRt, Texture holeMap)
        {
            Material mat = new Material(ShaderUtilities.Find(UNITY_HEIGHT_MAP_OUTPUT_SHADER_NAME));
            mat.SetTexture(MAIN_TEX, holeMap);
            Drawing.DrawQuad(targetRt, mat, PASS_CAPTURE_HOLE_MAP);
            Object.DestroyImmediate(mat);
        }

        public static void CaptureHeightMap(BiomeData snapshot, TerrainData terrainData)
        {
            RenderTexture heightMap = GraphicsUtils.CreateBlankRT(
                terrainData.heightmapResolution, RenderTextureFormat.RFloat);
            DecodeAndDrawHeightMap(heightMap, terrainData.heightmapTexture, Drawing.unitQuad);
            snapshot.heightMap = heightMap;
        }

        public static void CaptureHoleMap(BiomeData snapshot, TerrainData terrainData)
        {
            RenderTexture holeMap = GraphicsUtils.CreateBlankRT(
                terrainData.holesResolution, RenderTextureFormat.RFloat);
            CaptureHoleMap(holeMap, terrainData.holesTexture);
            snapshot.holeMap = holeMap;
        }

        public static void CaptureLayerWeights(BiomeData snapshot, TerrainData terrainData)
        {
            TerrainLayer[] layers = terrainData.terrainLayers;
            if (layers == null || layers.Length == 0)
                return;

            AlphaMapsCombiner combiner = new AlphaMapsCombiner();
            Texture2D[] alphaMaps = terrainData.alphamapTextures;
            for (int i = 0; i < layers.Length; ++i)
            {
                RenderTexture weight = combiner.ExtractChannel(
                    alphaMaps[i / 4], i % 4, terrainData.alphamapResolution);
                snapshot.AddTextureLayer(layers[i], weight);
            }
        }

        public static IEnumerator CaptureTreesProgressive(
            BiomeData snapshot,
            TerrainData terrainData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal)
        {
            TreePrototype[] prototypes = terrainData.treePrototypes;
            int instanceCount = terrainData.treeInstanceCount;
            List<TreeTemplate> templates = new List<TreeTemplate>(prototypes.Length);

            for (int i = 0; i < prototypes.Length; ++i)
            {
                TreeTemplate template = ScriptableObject.CreateInstance<TreeTemplate>();
                CopyToTreeTemplate(prototypes[i], template);
                templates.Add(template);
            }
            for (int i = 0; i < templates.Count; ++i)
            {
                transientResources.RegisterDestroyLater(templates[i]);
            }

            List<InstanceSample>[] samplesByPrototype = new List<InstanceSample>[prototypes.Length];
            for (int i = 0; i < prototypes.Length; ++i)
            {
                samplesByPrototype[i] = new List<InstanceSample>();
            }

            for (int i = 0; i < instanceCount; ++i)
            {
                TreeInstance instance = terrainData.GetTreeInstance(i);
                if (instance.prototypeIndex >= 0 && instance.prototypeIndex < prototypes.Length)
                {
                    samplesByPrototype[instance.prototypeIndex].Add(new InstanceSample()
                    {
                        isValid = 1,
                        position = instance.position,
                        verticalScale = instance.heightScale,
                        horizontalScale = instance.widthScale,
                        rotationY = instance.rotation
                    });
                }
                if ((i + 1) % SNAPSHOT_TREE_INSTANCES_PER_FRAME == 0)
                {
                    yield return null;
                    if (cancellationSignal?.isCancellationRequested == true)
                        yield break;
                }
            }

            for (int i = 0; i < prototypes.Length; ++i)
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

        public static IEnumerator CaptureDetailDensityProgressive(
            BiomeData snapshot,
            TerrainData terrainData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal)
        {
            DetailPrototype[] prototypes = terrainData.detailPrototypes;
            if (prototypes.Length == 0)
                yield break;

            int resolution = terrainData.detailResolution;
            Texture2D densityTexture = new Texture2D(
                resolution, resolution, TextureFormat.RFloat, false, true);
            densityTexture.wrapMode = TextureWrapMode.Clamp;
            densityTexture.filterMode = FilterMode.Bilinear;
            float[] densityBand = new float[resolution * Mathf.Min(SNAPSHOT_ROWS_PER_FRAME, resolution)];

            for (int i = 0; i < prototypes.Length; ++i)
            {
                DetailTemplate template = ScriptableObject.CreateInstance<DetailTemplate>();
                template.density = 1;
                CopyToDetailTemplate(prototypes[i], template);
                transientResources.RegisterDestroyLater(template);

                for (int baseY = 0; baseY < resolution; baseY += SNAPSHOT_ROWS_PER_FRAME)
                {
                    int bandHeight = Mathf.Min(SNAPSHOT_ROWS_PER_FRAME, resolution - baseY);
                    int[,] density = terrainData.GetDetailLayer(0, baseY, resolution, bandHeight, i);
                    WriteDetailDensityBand(densityTexture, density, densityBand, baseY, resolution);
                }

                densityTexture.Apply();
                RenderTexture densityMap = GraphicsUtils.CreateBlankRT(resolution, RenderTextureFormat.RFloat);
                Drawing.Blit(densityTexture, densityMap);
                snapshot.AddDetailDensity(template, densityMap);
                yield return null;
                if (cancellationSignal?.isCancellationRequested == true)
                {
                    Object.DestroyImmediate(densityTexture);
                    yield break;
                }
            }

            Object.DestroyImmediate(densityTexture);
        }

        public static void CaptureSnapshotResults(BiomeData snapshot, TerrainData terrainData)
        {
            VistaDebugger.OpenScope("Capture Terrain Snapshot", DebugScopeType.Custom);
            VistaDebugger.CaptureString("Resolutions", $"Height: {terrainData.heightmapResolution}, Holes: {terrainData.holesResolution}, Alphamap: {terrainData.alphamapResolution}, Detail: {terrainData.detailResolution}");
            VistaDebugger.Capture("Height Map", snapshot.heightMap);
            VistaDebugger.Capture("Hole Map", snapshot.holeMap);

            List<TerrainLayer> terrainLayers = new List<TerrainLayer>();
            List<RenderTexture> layerWeights = new List<RenderTexture>();
            snapshot.GetLayerWeights(terrainLayers, layerWeights);
            VistaDebugger.CaptureString("Terrain Layer Order", string.Join("\n", terrainLayers.ConvertAll(layer => layer != null ? layer.name : "Unnamed Layer")));
            VistaDebugger.Capture("Layer Weights", layerWeights);

            List<TreeTemplate> treeTemplates = new List<TreeTemplate>();
            List<ComputeBuffer> treeBuffers = new List<ComputeBuffer>();
            snapshot.GetTrees(treeTemplates, treeBuffers);
            for (int i = 0; i < treeBuffers.Count; ++i)
            {
                string prefabName = treeTemplates[i].prefab != null ? treeTemplates[i].prefab.name : "Missing Prefab";
                VistaDebugger.Capture($"Tree: {prefabName}", treeBuffers[i], DebugBufferInterpretation.InstanceSample);
            }

            List<DetailTemplate> detailTemplates = new List<DetailTemplate>();
            List<RenderTexture> densityMaps = new List<RenderTexture>();
            snapshot.GetDensityMaps(detailTemplates, densityMaps);
            for (int i = 0; i < densityMaps.Count; ++i)
            {
                DetailTemplate template = detailTemplates[i];
                string prototypeName = template.renderMode == DetailRenderMode.VertexLit
                    ? (template.prefab != null ? template.prefab.name : "Missing Prefab")
                    : (template.texture != null ? template.texture.name : "Missing Texture");
                VistaDebugger.Capture($"Detail Density: {prototypeName}", densityMaps[i]);
            }
            VistaDebugger.CloseScope();
        }

        private static void WriteDetailDensityBand(
            Texture2D texture,
            int[,] density,
            float[] densityBand,
            int baseY,
            int resolution)
        {
            int bandHeight = density.GetLength(0);
            for (int y = 0; y < bandHeight; ++y)
            {
                int rowStart = y * resolution;
                for (int x = 0; x < resolution; ++x)
                {
                    densityBand[rowStart + x] = density[y, x];
                }
            }

            int bandLength = bandHeight * resolution;
            NativeArray<float> textureData = texture.GetRawTextureData<float>();
            NativeArray<float>.Copy(densityBand, 0, textureData, baseY * resolution, bandLength);
        }

        /// <summary>
        /// Copies the Unity Terrain tree-prototype fields represented by Vista into a tree template.
        /// </summary>
        /// <param name="source">Source Unity Terrain prototype.</param>
        /// <param name="destination">Destination Vista template.</param>
        public static void CopyToTreeTemplate(TreePrototype source, TreeTemplate destination)
        {
            if (source == null)
            {
                throw new System.ArgumentNullException(nameof(source));
            }
            if (destination == null)
            {
                throw new System.ArgumentNullException(nameof(destination));
            }

            destination.prefab = source.prefab;
            destination.bendFactor = source.bendFactor;
            destination.navMeshLod = source.navMeshLod;
        }

        /// <summary>
        /// Creates one Unity Terrain tree prototype from the fields represented by a Vista tree template.
        /// </summary>
        /// <param name="source">Source Vista template.</param>
        /// <returns>A new Unity Terrain prototype for the template's primary prefab.</returns>
        public static TreePrototype CreateTreePrototype(TreeTemplate source)
        {
            if (source == null)
            {
                throw new System.ArgumentNullException(nameof(source));
            }

            TreePrototype prototype = new TreePrototype();
            prototype.prefab = source.prefab;
            prototype.bendFactor = source.bendFactor;
            prototype.navMeshLod = source.navMeshLod;
            return prototype;
        }

        /// <summary>
        /// Tests whether a Unity Terrain tree prototype and Vista tree template represent the same Unity tree type.
        /// </summary>
        public static bool IsEquivalent(TreePrototype prototype, TreeTemplate template)
        {
            return prototype != null &&
                template != null &&
                prototype.prefab == template.prefab &&
                prototype.bendFactor == template.bendFactor &&
                prototype.navMeshLod == template.navMeshLod;
        }

        /// <summary>
        /// Tests whether two Vista tree templates represent the same Unity tree prototype.
        /// </summary>
        public static bool IsEquivalent(TreeTemplate first, TreeTemplate second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            TreePrototype prototype = CreateTreePrototype(first);
            return IsEquivalent(prototype, second);
        }

        /// <summary>
        /// Copies the Unity Terrain detail-prototype fields represented by Vista into a detail template.
        /// </summary>
        public static void CopyToDetailTemplate(DetailPrototype source, DetailTemplate destination)
        {
            if (source == null)
            {
                throw new System.ArgumentNullException(nameof(source));
            }
            if (destination == null)
            {
                throw new System.ArgumentNullException(nameof(destination));
            }

            destination.renderMode = source.renderMode;
            destination.texture = source.prototypeTexture;
            destination.prefab = source.prototype;
            destination.primaryColor = source.healthyColor;
            destination.secondaryColor = source.dryColor;
            destination.minWidth = source.minWidth;
            destination.maxWidth = source.maxWidth;
            destination.minHeight = source.minHeight;
            destination.maxHeight = source.maxHeight;
            destination.noiseSpread = source.noiseSpread;
            destination.holeEdgePadding = source.holeEdgePadding;
#if UNITY_2021_2_OR_NEWER
            destination.useInstancing = source.useInstancing;
#endif
        }

        /// <summary>
        /// Creates one Unity Terrain detail prototype from the fields represented by a Vista detail template.
        /// </summary>
        public static DetailPrototype CreateDetailPrototype(DetailTemplate source)
        {
            if (source == null)
            {
                throw new System.ArgumentNullException(nameof(source));
            }

            DetailPrototype prototype = new DetailPrototype();
            prototype.renderMode = source.renderMode;
            prototype.healthyColor = source.primaryColor;
            prototype.dryColor = source.secondaryColor;
            prototype.minWidth = source.minWidth;
            prototype.maxWidth = source.maxWidth;
            prototype.minHeight = source.minHeight;
            prototype.maxHeight = source.maxHeight;
            prototype.noiseSpread = source.noiseSpread;
            prototype.holeEdgePadding = source.holeEdgePadding;
            prototype.usePrototypeMesh = source.renderMode == DetailRenderMode.VertexLit;
#if UNITY_2021_2_OR_NEWER
            prototype.useInstancing = source.renderMode == DetailRenderMode.VertexLit && source.useInstancing;
#endif
            if (source.renderMode == DetailRenderMode.VertexLit)
            {
                prototype.prototype = source.prefab;
            }
            else
            {
                prototype.prototypeTexture = source.texture;
            }
            return prototype;
        }

        /// <summary>
        /// Tests whether a Unity Terrain detail prototype and Vista detail template represent the same Unity detail type.
        /// </summary>
        public static bool IsEquivalent(DetailPrototype prototype, DetailTemplate template)
        {
            if (prototype == null || template == null ||
                prototype.renderMode != template.renderMode ||
                prototype.healthyColor != template.primaryColor ||
                prototype.dryColor != template.secondaryColor ||
                prototype.minWidth != template.minWidth ||
                prototype.maxWidth != template.maxWidth ||
                prototype.minHeight != template.minHeight ||
                prototype.maxHeight != template.maxHeight ||
                prototype.noiseSpread != template.noiseSpread ||
                prototype.holeEdgePadding != template.holeEdgePadding ||
                prototype.usePrototypeMesh != (template.renderMode == DetailRenderMode.VertexLit))
            {
                return false;
            }
#if UNITY_2021_2_OR_NEWER
            if (prototype.useInstancing != (template.renderMode == DetailRenderMode.VertexLit && template.useInstancing))
            {
                return false;
            }
#endif
            return template.renderMode == DetailRenderMode.VertexLit
                ? prototype.prototype == template.prefab
                : prototype.prototypeTexture == template.texture;
        }

        /// <summary>
        /// Tests whether two Vista detail templates represent the same Unity detail prototype.
        /// Density is excluded because it scales the density map rather than defining prototype identity.
        /// </summary>
        public static bool IsEquivalent(DetailTemplate first, DetailTemplate second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            DetailPrototype prototype = CreateDetailPrototype(first);
            return IsEquivalent(prototype, second);
        }
    }
}
#endif


