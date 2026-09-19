#if VISTA
using Pinwheel.Vista.BigWorld;
using Pinwheel.Vista.Diagnostics;
using Pinwheel.Vista.Graph;
using Pinwheel.Vista.Graphics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Coordinates transient tile capture and Seal preservation for one tile generation pass.
    /// </summary>
    internal static class SealHandler
    {
        internal sealed class ProcessResult : ProgressiveTask
        {
            private readonly GameObject m_context;
            private readonly string m_tileName;

            public bool isFaulted { get; private set; }

            public ProcessResult(ITile tile)
            {
                m_context = tile.gameObject;
                m_tileName = m_context.name;
            }

            public void Fail(System.Exception exception)
            {
                if (isFaulted)
                    return;

                isFaulted = true;
                Complete();
                Debug.LogError(
                    $"Terrain Seal failed while preserving tile '{m_tileName}'. " +
                    "Vista skipped population for this tile to protect its current terrain data.",
                    m_context);
                Debug.LogException(exception, m_context);
            }
        }

        /// <summary>
        /// Preserves captured tile channels covered by active Seals while leaving unsupported or unaffected tiles unchanged.
        /// </summary>
        internal static ProcessResult ProcessTile(
            ITile tile,
            BiomeData biomeBlendedData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal)
        {
            ProcessResult result = new ProcessResult(tile);
            IEnumerator processRoutine = ProcessTileProgressive(
                tile,
                biomeBlendedData,
                transientResources,
                cancellationSignal,
                result);
            CoroutineUtility.StartCoroutine(processRoutine, result.Fail);
            return result;
        }

        private static IEnumerator ProcessTileProgressive(
            ITile tile,
            BiomeData biomeBlendedData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal,
            ProcessResult result)
        {
            try
            {
                yield return ProcessTileCore(
                    tile,
                    biomeBlendedData,
                    transientResources,
                    cancellationSignal,
                    result);
            }
            finally
            {
                result.Complete();
            }
        }

        private static IEnumerator ProcessTileCore(
            ITile tile,
            BiomeData biomeBlendedData,
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal,
            ProcessResult result)
        {
            List<TerrainSeal> overlappingSeals = CollectOverlappingSeals(tile.worldBounds);
            if (overlappingSeals.Count == 0)
            {
                yield break;
            }

            ITileSnapshotProvider snapshotProvider = tile as ITileSnapshotProvider;
            if (snapshotProvider == null)
            {
                Debug.LogWarning(
                    $"Terrain Seal cannot preserve data on tile type '{tile.GetType().FullName}' because it does not implement {nameof(ITileSnapshotProvider)}. The tile will be populated normally.");
                yield break;
            }

            BiomeDataRequest snapshotRequest = snapshotProvider.CaptureSnapshot(
                transientResources, cancellationSignal);
            yield return snapshotRequest;

            if (snapshotRequest.isFaulted)
            {
                snapshotRequest.data?.Dispose();
                result.Fail(snapshotRequest.exception);
                yield break;
            }

            BiomeData snapshot = snapshotRequest.data;
            if (snapshot == null)
            {
                yield break;
            }

            if (cancellationSignal?.isCancellationRequested == true)
            {
                snapshot.Dispose();
                yield break;
            }

            RenderTexture sealMask = null;
            try
            {
                int sealMaskResolution = Mathf.Max(tile.heightMapResolution, tile.textureResolution);
                sealMask = CreateUnifiedSealMask(
                    tile.worldBounds,
                    overlappingSeals,
                    sealMaskResolution);

                VistaDebugger.OpenScope("Apply Terrain Seals", DebugScopeType.Custom);
                try
                {
                    VistaDebugger.CaptureString("Seal Count", overlappingSeals.Count.ToString());
                    VistaDebugger.CaptureString("Tile Bounds", tile.worldBounds.ToString());
                    VistaDebugger.CaptureString("Seal Mask Wrap Mode", sealMask.wrapMode.ToString());
                    VistaDebugger.Capture("Unified Seal Mask", sealMask);

                    // These internal passes detach and release captured RT channels as they are consumed;
                    // the final snapshot disposal releases only the resources that remain attached.
                    BiomeTextureBlend.BlendContext blendContext = BiomeTextureBlend.Begin();
                    try
                    {
                        BlendGeometry(sealMask, biomeBlendedData, snapshot, blendContext);
                        BlendSurfaceMaps(sealMask, biomeBlendedData, snapshot, blendContext);
                        BlendLayerWeights(snapshotProvider, sealMask, biomeBlendedData, snapshot, blendContext);
                        BlendDensityMaps(snapshotProvider, sealMask, biomeBlendedData, snapshot, blendContext);
                        SuppressGenericTextures(sealMask, biomeBlendedData, blendContext);
                    }
                    finally
                    {
                        BiomeTextureBlend.End(blendContext);
                    }

                    BiomeBufferBlend.BufferBlendContext bufferBlendContext = BiomeBufferBlend.Begin();
                    try
                    {
                        BlendTrees(snapshotProvider, sealMask, biomeBlendedData, snapshot, bufferBlendContext);
                        BlendDetailInstances(snapshotProvider, sealMask, biomeBlendedData, snapshot, bufferBlendContext);
                        SuppressGenericBuffers(sealMask, biomeBlendedData, bufferBlendContext);
                    }
                    finally
                    {
                        BiomeBufferBlend.End(bufferBlendContext);
                    }

                }
                finally
                {
                    VistaDebugger.CloseScope();
                }
            }
            finally
            {
                ReleaseRenderTexture(sealMask);
                snapshot.Dispose();
            }
        }

        private static List<TerrainSeal> CollectOverlappingSeals(Bounds tileWorldBounds)
        {
            List<TerrainSeal> overlappingSeals = new List<TerrainSeal>();
            foreach (TerrainSeal seal in TerrainSeal.allInstances)
            {
                Vector3[] anchors = seal.anchors;
                if (anchors != null && anchors.Length >= 3 && seal.IsOverlap(tileWorldBounds))
                {
                    overlappingSeals.Add(seal);
                }
            }
            return overlappingSeals;
        }

        private static void BlendGeometry(
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeData snapshot,
            BiomeTextureBlend.BlendContext blendContext)
        {
            if (snapshot.heightMap != null)
            {
                RenderTexture oldHeightMap = biomeBlendedData.heightMap;
                RenderTexture capturedHeightMap = snapshot.DetachHeightMap();
                try
                {
                    RenderTexture newHeightMap = CreateBlendedMap(
                        "Height Map", oldHeightMap, capturedHeightMap, sealMask, blendContext);
                    biomeBlendedData.heightMap = newHeightMap;
                    ReleaseRenderTexture(oldHeightMap);
                }
                finally
                {
                    ReleaseRenderTexture(capturedHeightMap);
                }
            }

            if (snapshot.holeMap != null)
            {
                RenderTexture oldHoleMap = biomeBlendedData.holeMap;
                RenderTexture capturedHoleMap = snapshot.DetachHoleMap();
                try
                {
                    RenderTexture newHoleMap = CreateBlendedMap(
                        "Hole Map", oldHoleMap, capturedHoleMap, sealMask, blendContext);
                    biomeBlendedData.holeMap = newHoleMap;
                    ReleaseRenderTexture(oldHoleMap);
                }
                finally
                {
                    ReleaseRenderTexture(capturedHoleMap);
                }
            }

            if (snapshot.meshDensityMap != null)
            {
                RenderTexture oldMeshDensityMap = biomeBlendedData.meshDensityMap;
                RenderTexture capturedMeshDensityMap = snapshot.DetachMeshDensityMap();
                try
                {
                    RenderTexture newMeshDensityMap = CreateBlendedMap(
                        "Mesh Density Map", oldMeshDensityMap, capturedMeshDensityMap, sealMask, blendContext);
                    biomeBlendedData.meshDensityMap = newMeshDensityMap;
                    ReleaseRenderTexture(oldMeshDensityMap);
                }
                finally
                {
                    ReleaseRenderTexture(capturedMeshDensityMap);
                }
            }
        }

        private static RenderTexture CreateUnifiedSealMask(
            Bounds tileWorldBounds,
            List<TerrainSeal> seals,
            int resolution)
        {
            RenderTexture unifiedMask = LocalAreaUtils.AllocatePolygonalMaskRT(resolution);
            RenderTexture scratchMask = null;
            try
            {
                scratchMask = LocalAreaUtils.AllocatePolygonalMaskRT(resolution);
                for (int i = 0; i < seals.Count; ++i)
                {
                    LocalAreaUtils.RenderPolygonalMask(seals[i], tileWorldBounds, scratchMask);
                    Drawing.BlitMax(scratchMask, unifiedMask);
                }
                return unifiedMask;
            }
            catch
            {
                ReleaseRenderTexture(unifiedMask);
                throw;
            }
            finally
            {
                ReleaseRenderTexture(scratchMask);
            }
        }

        private static void BlendSurfaceMaps(
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeData snapshot,
            BiomeTextureBlend.BlendContext blendContext)
        {
            if (snapshot.albedoMap != null)
            {
                RenderTexture oldAlbedoMap = biomeBlendedData.albedoMap;
                RenderTexture capturedAlbedoMap = snapshot.DetachAlbedoMap();
                try
                {
                    RenderTexture newAlbedoMap = CreateBlendedMap(
                        "Albedo Map", oldAlbedoMap, capturedAlbedoMap, sealMask, blendContext);
                    biomeBlendedData.albedoMap = newAlbedoMap;
                    ReleaseRenderTexture(oldAlbedoMap);
                }
                finally
                {
                    ReleaseRenderTexture(capturedAlbedoMap);
                }
            }

            if (snapshot.metallicMap != null)
            {
                RenderTexture oldMetallicMap = biomeBlendedData.metallicMap;
                RenderTexture capturedMetallicMap = snapshot.DetachMetallicMap();
                try
                {
                    RenderTexture newMetallicMap = CreateBlendedMap(
                        "Metallic Map", oldMetallicMap, capturedMetallicMap, sealMask, blendContext);
                    biomeBlendedData.metallicMap = newMetallicMap;
                    ReleaseRenderTexture(oldMetallicMap);
                }
                finally
                {
                    ReleaseRenderTexture(capturedMetallicMap);
                }
            }
        }

        private static RenderTexture CreateBlendedMap(
            string channelName,
            RenderTexture biomeBlendedMap,
            RenderTexture snapshotMap,
            RenderTexture sealMask,
            BiomeTextureBlend.BlendContext blendContext)
        {
            VistaDebugger.OpenScope(channelName, DebugScopeType.BlendPass);
            try
            {
                // The snapshot represents the backend's persisted pixel grid, so it defines the replacement layout.
                RenderTexture result = GraphicsUtils.CreateBlankRT(snapshotMap.width, snapshotMap.format);
                try
                {
                    if (biomeBlendedMap != null)
                    {
                        Drawing.Blit(biomeBlendedMap, result);
                        VistaDebugger.Capture("Generated Before Seal", biomeBlendedMap);
                    }
                    VistaDebugger.Capture("Captured", snapshotMap);
                    VistaDebugger.Capture("Seal Mask", sealMask);

                    BiomeTextureBlend.BlendReplace(snapshotMap, result, sealMask, blendContext);
                    VistaDebugger.Capture("Final", result);
                    return result;
                }
                catch
                {
                    ReleaseRenderTexture(result);
                    throw;
                }
            }
            finally
            {
                VistaDebugger.CloseScope();
            }
        }

        private static void BlendLayerWeights(
            ITileSnapshotProvider snapshotProvider,
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeData snapshot,
            BiomeTextureBlend.BlendContext blendContext)
        {
            VistaDebugger.OpenScope("Texture Weights", DebugScopeType.BlendPass);

            List<TerrainLayer> biomeLayers = new List<TerrainLayer>();
            List<RenderTexture> biomeWeights = new List<RenderTexture>();
            biomeBlendedData.GetLayerWeights(biomeLayers, biomeWeights);
            List<TerrainLayer> snapshotLayers = new List<TerrainLayer>();
            List<RenderTexture> snapshotWeights = new List<RenderTexture>();
            snapshot.DetachLayerWeights(snapshotLayers, snapshotWeights);

            try
            {
                CaptureLayerWeights("Generated Before Seal", biomeLayers, biomeWeights);
                VistaDebugger.Capture("Seal Mask", sealMask);
                CaptureLayerWeights("Captured", snapshotLayers, snapshotWeights);

                for (int i = 0; i < biomeWeights.Count; ++i)
                {
                    BiomeTextureBlend.BlendReplace(Texture2D.blackTexture, biomeWeights[i], sealMask, blendContext);
                }

                for (int i = 0; i < snapshotWeights.Count; ++i)
                {
                    TerrainLayer resultLayer = FindEquivalentTerrainLayer(
                        snapshotProvider, snapshotLayers[i], biomeLayers);
                    RenderTexture snapshotWeight = snapshotWeights[i];
                    RenderTexture maskedWeight = null;
                    try
                    {
                        maskedWeight = GraphicsUtils.CreateBlankRT(snapshotWeight.width, snapshotWeight.format);
                        BiomeTextureBlend.BlendReplace(snapshotWeight, maskedWeight, sealMask, blendContext);
                        biomeBlendedData.AddTextureLayer(resultLayer, maskedWeight);
                        maskedWeight = null;
                    }
                    finally
                    {
                        ReleaseRenderTexture(maskedWeight);
                        ReleaseRenderTexture(snapshotWeight);
                        snapshotWeights[i] = null;
                    }
                }

                biomeBlendedData.GetLayerWeights(biomeLayers, biomeWeights);
                CaptureLayerWeights("Final", biomeLayers, biomeWeights);
            }
            finally
            {
                ReleaseRenderTextures(snapshotWeights);
                VistaDebugger.CloseScope();
            }
        }

        private static TerrainLayer FindEquivalentTerrainLayer(
            ITileSnapshotProvider snapshotProvider,
            TerrainLayer capturedLayer,
            List<TerrainLayer> generatedLayers)
        {
            for (int i = 0; i < generatedLayers.Count; ++i)
            {
                TerrainLayer generatedLayer = generatedLayers[i];
                if (snapshotProvider.IsEquivalent(capturedLayer, generatedLayer))
                {
                    return generatedLayer;
                }
            }
            return capturedLayer;
        }

        private static void CaptureLayerWeights(
            string label,
            List<TerrainLayer> layers,
            List<RenderTexture> weights)
        {
            VistaDebugger.CaptureString(
                $"{label} Layer Order\n",
                string.Join("\n", layers.ConvertAll(layer => layer != null ? layer.name : "Unnamed Layer")));
            VistaDebugger.CaptureTexture($"{label} Weights", weights);
        }

        private static void BlendDensityMaps(
            ITileSnapshotProvider snapshotProvider,
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeData snapshot,
            BiomeTextureBlend.BlendContext blendContext)
        {
            VistaDebugger.OpenScope("Density Maps", DebugScopeType.BlendPass);

            List<DetailTemplate> generatedTemplates = new List<DetailTemplate>();
            List<RenderTexture> generatedMaps = new List<RenderTexture>();
            biomeBlendedData.GetDensityMaps(generatedTemplates, generatedMaps);
            List<DetailTemplate> capturedTemplates = new List<DetailTemplate>();
            List<RenderTexture> capturedMaps = new List<RenderTexture>();
            snapshot.DetachDensityMaps(capturedTemplates, capturedMaps);

            try
            {
                CaptureDensityMaps("Generated Before Seal", generatedTemplates, generatedMaps);
                VistaDebugger.Capture("Seal Mask", sealMask);
                CaptureDensityMaps("Captured", capturedTemplates, capturedMaps);

                for (int i = 0; i < generatedMaps.Count; ++i)
                {
                    BiomeTextureBlend.BlendReplace(
                        Texture2D.blackTexture, generatedMaps[i], sealMask, blendContext);
                }

                for (int i = 0; i < capturedMaps.Count; ++i)
                {
                    DetailTemplate capturedTemplate = capturedTemplates[i];
                    DetailTemplate generatedTemplate = FindEquivalentTemplate(
                        snapshotProvider, capturedTemplate, generatedTemplates);
                    DetailTemplate resultTemplate = generatedTemplate != null
                        ? generatedTemplate
                        : capturedTemplate;
                    float normalizationScale = generatedTemplate != null
                        ? 1f / generatedTemplate.density
                        : 1f;

                    RenderTexture capturedMap = capturedMaps[i];
                    RenderTexture maskedMap = null;
                    try
                    {
                        maskedMap = GraphicsUtils.CreateBlankRT(
                            capturedMap.width, capturedMap.format);
                        BiomeTextureBlend.BlendReplace(
                            capturedMap,
                            maskedMap,
                            sealMask,
                            blendContext,
                            0f,
                            normalizationScale);
                        biomeBlendedData.AddDetailDensity(resultTemplate, maskedMap);
                        maskedMap = null;
                    }
                    finally
                    {
                        ReleaseRenderTexture(maskedMap);
                        ReleaseRenderTexture(capturedMap);
                        capturedMaps[i] = null;
                    }
                }

                biomeBlendedData.GetDensityMaps(generatedTemplates, generatedMaps);
                CaptureDensityMaps("Final", generatedTemplates, generatedMaps);
            }
            finally
            {
                ReleaseRenderTextures(capturedMaps);
                VistaDebugger.CloseScope();
            }
        }

        private static DetailTemplate FindEquivalentTemplate(
            ITileSnapshotProvider snapshotProvider,
            DetailTemplate capturedTemplate,
            List<DetailTemplate> generatedTemplates)
        {
            for (int i = 0; i < generatedTemplates.Count; ++i)
            {
                if (snapshotProvider.IsEquivalent(capturedTemplate, generatedTemplates[i]))
                {
                    return generatedTemplates[i];
                }
            }
            return null;
        }

        private static void CaptureDensityMaps(
            string label,
            List<DetailTemplate> templates,
            List<RenderTexture> maps)
        {
            VistaDebugger.CaptureString(
                $"{label} Template Order",
                string.Join("\n", templates.ConvertAll(template => template != null ? template.name : "Unnamed Template")));
            VistaDebugger.CaptureTexture($"{label} Maps", maps);
        }

        private static void SuppressGenericTextures(
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeTextureBlend.BlendContext blendContext)
        {
            VistaDebugger.OpenScope("Generic Textures", DebugScopeType.BlendPass);

            List<string> labels = new List<string>();
            List<RenderTexture> textures = new List<RenderTexture>();
            biomeBlendedData.GetGenericTextures(labels, textures);
            try
            {
                VistaDebugger.CaptureString("Label Order", string.Join("\n", labels));
                VistaDebugger.CaptureTexture("Generated Before Seal", textures);
                VistaDebugger.Capture("Seal Mask", sealMask);

                for (int i = 0; i < textures.Count; ++i)
                {
                    if (textures[i] != null)
                    {
                        BiomeTextureBlend.BlendReplace(
                            Texture2D.blackTexture, textures[i], sealMask, blendContext);
                    }
                }

                VistaDebugger.CaptureTexture("Final", textures);
            }
            finally
            {
                VistaDebugger.CloseScope();
            }
        }

        private static void BlendTrees(
            ITileSnapshotProvider snapshotProvider,
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeData snapshot,
            BiomeBufferBlend.BufferBlendContext blendContext)
        {
            VistaDebugger.OpenScope("Trees", DebugScopeType.BlendPass);

            List<TreeTemplate> generatedTemplates = new List<TreeTemplate>();
            List<ComputeBuffer> generatedBuffers = new List<ComputeBuffer>();
            biomeBlendedData.GetTrees(generatedTemplates, generatedBuffers);
            List<TreeTemplate> capturedTemplates = new List<TreeTemplate>();
            List<ComputeBuffer> capturedBuffers = new List<ComputeBuffer>();
            snapshot.GetTrees(capturedTemplates, capturedBuffers);

            try
            {
                CaptureTreeBuffers("Generated Before Seal", generatedTemplates, generatedBuffers);
                VistaDebugger.Capture("Seal Mask", sealMask);
                CaptureTreeBuffers("Captured", capturedTemplates, capturedBuffers);

                for (int i = 0; i < generatedBuffers.Count; ++i)
                {
                    if (generatedBuffers[i] != null)
                    {
                        BiomeBufferBlend.DispatchBufferMask<InstanceSample>(
                            blendContext, generatedBuffers[i], sealMask, flipMask: true);
                    }
                }

                for (int i = 0; i < capturedBuffers.Count; ++i)
                {
                    ComputeBuffer capturedBuffer = capturedBuffers[i];
                    if (capturedBuffer == null)
                    {
                        continue;
                    }

                    TreeTemplate generatedTemplate = FindEquivalentTemplate(
                        snapshotProvider, capturedTemplates[i], generatedTemplates);
                    TreeTemplate resultTemplate = generatedTemplate != null
                        ? generatedTemplate
                        : capturedTemplates[i];
                    ComputeBuffer maskedBuffer = BufferHelper.Clone(capturedBuffer);
                    try
                    {
                        BiomeBufferBlend.DispatchBufferMask<InstanceSample>(
                            blendContext, maskedBuffer, sealMask, flipMask: false);
                        biomeBlendedData.AddTree(resultTemplate, maskedBuffer);
                    }
                    catch
                    {
                        maskedBuffer.Release();
                        throw;
                    }
                }

                biomeBlendedData.GetTrees(generatedTemplates, generatedBuffers);
                CaptureTreeBuffers("Final", generatedTemplates, generatedBuffers);
            }
            finally
            {
                VistaDebugger.CloseScope();
            }
        }

        private static TreeTemplate FindEquivalentTemplate(
            ITileSnapshotProvider snapshotProvider,
            TreeTemplate capturedTemplate,
            List<TreeTemplate> generatedTemplates)
        {
            for (int i = 0; i < generatedTemplates.Count; ++i)
            {
                if (snapshotProvider.IsEquivalent(capturedTemplate, generatedTemplates[i]))
                {
                    return generatedTemplates[i];
                }
            }
            return null;
        }

        private static void CaptureTreeBuffers(
            string label,
            List<TreeTemplate> templates,
            List<ComputeBuffer> buffers)
        {
            VistaDebugger.CaptureString(
                $"{label} Template Order",
                string.Join("\n", templates.ConvertAll(template => template != null ? template.name : "Unnamed Template")));
            for (int i = 0; i < buffers.Count; ++i)
            {
                if (buffers[i] != null)
                {
                    VistaDebugger.Capture(
                        $"{label} {i}", buffers[i], DebugBufferInterpretation.InstanceSample);
                }
            }
        }

        private static void BlendDetailInstances(
            ITileSnapshotProvider snapshotProvider,
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeData snapshot,
            BiomeBufferBlend.BufferBlendContext blendContext)
        {
            VistaDebugger.OpenScope("Detail Instances", DebugScopeType.BlendPass);

            List<DetailTemplate> generatedTemplates = new List<DetailTemplate>();
            List<ComputeBuffer> generatedBuffers = new List<ComputeBuffer>();
            biomeBlendedData.GetDetailInstances(generatedTemplates, generatedBuffers);
            List<DetailTemplate> capturedTemplates = new List<DetailTemplate>();
            List<ComputeBuffer> capturedBuffers = new List<ComputeBuffer>();
            snapshot.GetDetailInstances(capturedTemplates, capturedBuffers);

            try
            {
                CaptureDetailInstanceBuffers("Generated Before Seal", generatedTemplates, generatedBuffers);
                VistaDebugger.Capture("Seal Mask", sealMask);
                CaptureDetailInstanceBuffers("Captured", capturedTemplates, capturedBuffers);

                for (int i = 0; i < generatedBuffers.Count; ++i)
                {
                    if (generatedBuffers[i] != null)
                    {
                        BiomeBufferBlend.DispatchBufferMask<InstanceSample>(
                            blendContext, generatedBuffers[i], sealMask, flipMask: true);
                    }
                }

                for (int i = 0; i < capturedBuffers.Count; ++i)
                {
                    ComputeBuffer capturedBuffer = capturedBuffers[i];
                    if (capturedBuffer == null)
                    {
                        continue;
                    }

                    DetailTemplate generatedTemplate = FindEquivalentTemplate(
                        snapshotProvider, capturedTemplates[i], generatedTemplates);
                    DetailTemplate resultTemplate = generatedTemplate != null
                        ? generatedTemplate
                        : capturedTemplates[i];
                    ComputeBuffer maskedBuffer = BufferHelper.Clone(capturedBuffer);
                    try
                    {
                        BiomeBufferBlend.DispatchBufferMask<InstanceSample>(
                            blendContext, maskedBuffer, sealMask, flipMask: false);
                        biomeBlendedData.AddDetailInstance(resultTemplate, maskedBuffer);
                    }
                    catch
                    {
                        maskedBuffer.Release();
                        throw;
                    }
                }

                biomeBlendedData.GetDetailInstances(generatedTemplates, generatedBuffers);
                CaptureDetailInstanceBuffers("Final", generatedTemplates, generatedBuffers);
            }
            finally
            {
                VistaDebugger.CloseScope();
            }
        }

        private static void CaptureDetailInstanceBuffers(
            string label,
            List<DetailTemplate> templates,
            List<ComputeBuffer> buffers)
        {
            VistaDebugger.CaptureString(
                $"{label} Template Order",
                string.Join("\n", templates.ConvertAll(template => template != null ? template.name : "Unnamed Template")));
            for (int i = 0; i < buffers.Count; ++i)
            {
                if (buffers[i] != null)
                {
                    VistaDebugger.Capture(
                        $"{label} {i}", buffers[i], DebugBufferInterpretation.InstanceSample);
                }
            }
        }

        private static void SuppressGenericBuffers(
            RenderTexture sealMask,
            BiomeData biomeBlendedData,
            BiomeBufferBlend.BufferBlendContext blendContext)
        {
            VistaDebugger.OpenScope("Generic Buffers", DebugScopeType.BlendPass);

            List<string> labels = new List<string>();
            List<ComputeBuffer> buffers = new List<ComputeBuffer>();
            biomeBlendedData.GetGenericBuffers(labels, buffers);
            try
            {
                VistaDebugger.CaptureString("Label Order", string.Join("\n", labels));
                CaptureGenericBuffers("Generated Before Seal", buffers);
                VistaDebugger.Capture("Seal Mask", sealMask);

                for (int i = 0; i < buffers.Count; ++i)
                {
                    if (buffers[i] != null)
                    {
                        BiomeBufferBlend.DispatchBufferMask<PositionSample>(
                            blendContext, buffers[i], sealMask, flipMask: true);
                    }
                }

                CaptureGenericBuffers("Final", buffers);
            }
            finally
            {
                VistaDebugger.CloseScope();
            }
        }

        private static void CaptureGenericBuffers(string label, List<ComputeBuffer> buffers)
        {
            for (int i = 0; i < buffers.Count; ++i)
            {
                if (buffers[i] != null)
                {
                    VistaDebugger.Capture(
                        $"{label} {i}", buffers[i], DebugBufferInterpretation.PositionSample);
                }
            }
        }

        private static void ReleaseRenderTexture(RenderTexture texture)
        {
            if (texture == null)
            {
                return;
            }
            texture.Release();
            Object.DestroyImmediate(texture);
        }

        private static void ReleaseRenderTextures(List<RenderTexture> textures)
        {
            for (int i = 0; i < textures.Count; ++i)
            {
                ReleaseRenderTexture(textures[i]);
            }
        }
    }
}
#endif
