#if VISTA
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Graphics;
using Pinwheel.Vista.Graph;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Generic Mountain",
        path = "Generators/Generic Mountain",
        keywords = "mountain, base, shape, noise, generic",
        description = "Generate a generic mountainous base shape from two noise maps at different scales and octaves.")]
    public class GenericMountainNode : ImageNodeBase, IHasSeed
    {
        public readonly MaskSlot outputSlot = new MaskSlot("Output", SlotDirection.Output, 100);

        [SerializeField]
        private float m_mountainScale;
        public float mountainScale
        {
            get
            {
                return m_mountainScale;
            }
            set
            {
                m_mountainScale = Mathf.Max(1f, value);
            }
        }

        [SerializeField]
        private float m_peakVariationScale;
        public float peakVariationScale
        {
            get
            {
                return m_peakVariationScale;
            }
            set
            {
                m_peakVariationScale = Mathf.Max(1f, value);
            }
        }

        [SerializeField]
        private int m_seed;
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

        [SerializeField]
        private float m_minHeight;
        public float minHeight
        {
            get
            {
                return m_minHeight;
            }
            set
            {
                m_minHeight = Mathf.Clamp01(Mathf.Min(value, m_maxHeight));
            }
        }

        [SerializeField]
        private float m_maxHeight;
        public float maxHeight
        {
            get
            {
                return m_maxHeight;
            }
            set
            {
                m_maxHeight = Mathf.Clamp01(Mathf.Max(value, m_minHeight));
            }
        }

        /// <summary>
        /// Offset applied to the node seed so the peak variation noise never repeats the mountain shape noise.
        /// </summary>
        private static readonly int PEAK_VARIATION_SEED_OFFSET = 7919;

        private static readonly string TEMP_RT_MOUNTAIN_SHAPE_NAME = "~GenericMountain_MountainShape";
        private static readonly string TEMP_RT_PEAK_VARIATION_NAME = "~GenericMountain_PeakVariation";
        private static readonly string TEMP_RT_COMBINED_NAME = "~GenericMountain_Combined";

        public GenericMountainNode() : base()
        {
            m_mountainScale = 750;
            m_peakVariationScale = 1000;
            m_minHeight = 0.05f;
            m_maxHeight = 1f;

            System.Random random = new System.Random();
            m_seed = random.Next(0, 1000);
        }

        public override void ExecuteImmediate(GraphContext context)
        {
            int baseResolution = context.GetArg(Args.RESOLUTION).intValue;
            int resolution = this.CalculateResolution(baseResolution, baseResolution);
            DataPool.RtDescriptor descriptor = DataPool.RtDescriptor.Create(resolution, resolution);

            int baseSeed = context.GetArg(Args.SEED).intValue;
            Vector4 worldBounds = context.GetArg(Args.WORLD_BOUNDS).vectorValue;

            RenderTexture mountainShapeRt = context.CreateTemporaryRT(descriptor, TEMP_RT_MOUNTAIN_SHAPE_NAME);
            NodeLibraryUtilities.NoiseNode.Execute(context, mountainShapeRt, CreateMountainShapeParams(baseSeed, worldBounds));

            RenderTexture peakVariationRt = context.CreateTemporaryRT(descriptor, TEMP_RT_PEAK_VARIATION_NAME);
            NodeLibraryUtilities.NoiseNode.Execute(context, peakVariationRt, CreatePeakVariationParams(baseSeed, worldBounds));

            RenderTexture combinedRt = context.CreateTemporaryRT(descriptor, TEMP_RT_COMBINED_NAME);
            NodeLibraryUtilities.CombineNode.Execute(context, mountainShapeRt, peakVariationRt, Texture2D.whiteTexture, NodeLibraryUtilities.CombineNode.MODE_MUL, combinedRt);

            SlotRef outputRef = new SlotRef(m_id, outputSlot.id);
            RenderTexture targetRt = context.CreateRenderTarget(descriptor, outputRef);
            NodeLibraryUtilities.LevelsNode.Execute(context, combinedRt, targetRt, 0f, 0.5f, 1f, m_minHeight, m_maxHeight);

            context.ReleaseTemporary(TEMP_RT_MOUNTAIN_SHAPE_NAME);
            context.ReleaseTemporary(TEMP_RT_PEAK_VARIATION_NAME);
            context.ReleaseTemporary(TEMP_RT_COMBINED_NAME);
        }

        public override IEnumerator Execute(GraphContext context)
        {
            ExecuteImmediate(context);
            yield return null;
        }

        /// <summary>
        /// Builds the parameters of the background noise, which carries the overall mountain shape together with its details.
        /// </summary>
        private NodeLibraryUtilities.NoiseNode.Params CreateMountainShapeParams(int baseSeed, Vector4 worldBounds)
        {
            NodeLibraryUtilities.NoiseNode.Params parameters = new NodeLibraryUtilities.NoiseNode.Params();
            parameters.offset = Vector2.zero;
            parameters.scale = m_mountainScale;
            parameters.lacunarity = 2.26f;
            parameters.persistence = 0.382f;
            parameters.layerCount = 8;
            parameters.mode = NoiseMode.Perlin01;
            parameters.layerDerivative = NodeLibraryUtilities.NoiseNode.DERIVATIVE_LINEAR;
            parameters.flipSign = true;
            parameters.warpMode = NodeLibraryUtilities.NoiseNode.WARP_NONE;
            parameters.remapCurve = AnimationCurve.EaseInOut(0.1f,0,1,1);
            parameters.applyRemapPerLayer = true;
            parameters.seed = baseSeed ^ m_seed;
            parameters.worldBounds = worldBounds;
            return parameters;
        }

        /// <summary>
        /// Builds the parameters of the foreground noise, a single octave smooth noise that varies the height of the peaks.
        /// </summary>
        private NodeLibraryUtilities.NoiseNode.Params CreatePeakVariationParams(int baseSeed, Vector4 worldBounds)
        {
            NodeLibraryUtilities.NoiseNode.Params parameters = new NodeLibraryUtilities.NoiseNode.Params();
            parameters.offset = Vector2.zero;
            parameters.scale = m_peakVariationScale;
            parameters.lacunarity = 2.25f;
            parameters.persistence = 0.335f;
            parameters.layerCount = 1;
            parameters.mode = NoiseMode.Perlin01;
            parameters.layerDerivative = NodeLibraryUtilities.NoiseNode.DERIVATIVE_LINEAR;
            parameters.flipSign = true;
            parameters.warpMode = NodeLibraryUtilities.NoiseNode.WARP_NONE;
            parameters.remapCurve = AnimationCurve.EaseInOut(0,0,1,1);
            parameters.applyRemapPerLayer = true;
            parameters.seed = baseSeed ^ (m_seed + PEAK_VARIATION_SEED_OFFSET);
            parameters.worldBounds = worldBounds;
            return parameters;
        }
    }
}
#endif
