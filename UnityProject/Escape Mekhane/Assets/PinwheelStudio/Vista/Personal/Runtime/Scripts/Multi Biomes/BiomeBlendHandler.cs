#if VISTA
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Diagnostics;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pinwheel.Vista.BigWorld
{
    /// <summary>
    /// Entry point for compositing ordered terrain-data sources. Registered as a callback on
    /// <see cref="VistaManager.blendBiomeDataCallback"/> and shared by biome generation and manual workflows.
    /// </summary>
    internal static class BiomeBlendHandler
    {
#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [UnityEngine.RuntimeInitializeOnLoadMethod]
#endif
        private static void OnInitialize()
        {
            VistaManager.blendBiomeDataCallback += Blend;
        }

        /// <summary>
        /// Blends all channels from the given ordered sources into a single composite <see cref="BiomeData"/>.
        /// </summary>
        /// <param name="configs">
        /// Ordered blend settings parallel to <paramref name="srcDatas"/>.
        /// </param>
        /// <param name="srcDatas">
        /// Ordered list of terrain data sources. Sources are composited in list order, so later entries
        /// are layered on top of earlier ones according to their blend options.
        /// </param>
        /// <returns>
        /// A new <see cref="BiomeData"/> containing all composited channels. The caller owns the
        /// returned object and is responsible for releasing its GPU resources.
        /// </returns>
        internal static BiomeData Blend(List<BiomeBlendConfig> configs, List<BiomeData> srcDatas)
        {
            if (configs == null)
                throw new System.ArgumentNullException(nameof(configs));
            if (srcDatas == null)
                throw new System.ArgumentNullException(nameof(srcDatas));
            if (configs.Count != srcDatas.Count)
                throw new System.ArgumentException("Blend configs and source data must have the same count.");

            BiomeData data = new BiomeData();

            VistaDebugger.OpenScope("Biome Blend", DebugScopeType.BlendPass);

            // Texture channels. One shader load covers all texture blend dispatches.
            BiomeTextureBlend.BlendContext textureBlendContext = BiomeTextureBlend.Begin();
            BiomeTextureBlend.BlendHeightMap(data, configs, srcDatas, textureBlendContext);
            BiomeTextureBlend.BlendHoleMap(data, configs, srcDatas, textureBlendContext);
            BiomeTextureBlend.BlendMeshDensityMap(data, configs, srcDatas, textureBlendContext);
            BiomeTextureBlend.BlendAlbedoMap(data, configs, srcDatas, textureBlendContext);
            BiomeTextureBlend.BlendMetallicMap(data, configs, srcDatas, textureBlendContext);
            BiomeTextureBlend.BlendGenericTextures(data, configs, srcDatas, textureBlendContext);
            BiomeTextureBlend.BlendTextureWeights(data, configs, srcDatas, textureBlendContext);
            BiomeTextureBlend.BlendDensityMaps(data, configs, srcDatas, textureBlendContext);

            // Buffer channels. Uses a separate shader and context from the texture pass.
            BiomeBufferBlend.BufferBlendContext bufferBlendContext = BiomeBufferBlend.Begin();
            BiomeBufferBlend.BlendTreeBuffer(data, configs, srcDatas, textureBlendContext, bufferBlendContext);
            BiomeBufferBlend.BlendDetailInstanceBuffer(data, configs, srcDatas, textureBlendContext, bufferBlendContext);
            BiomeBufferBlend.BlendObjectBuffer(data, configs, srcDatas, textureBlendContext, bufferBlendContext);
            BiomeBufferBlend.BlendGenericBuffer(data, configs, srcDatas, textureBlendContext, bufferBlendContext);
            BiomeBufferBlend.End(bufferBlendContext);
            BiomeTextureBlend.End(textureBlendContext);

            VistaDebugger.CloseScope();
            return data;
        }
    }
}
#endif
