#if VISTA
using Pinwheel.Vista.Graphics;
using System.Collections;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Curvature",
        path = "Masking/Curvature",
        icon = "",
        documentation = "",
        keywords = "convex, concave, convexity, concavity, curvature",
        description = "Measure how much each pixel rises above or sinks below the average height of its surrounding area.\nRadius and deviation are defined in world meters, so the result is consistent across graph resolutions and biome sizes.")]
    public class CurvatureNode : ImageNodeBase
    {
        public readonly MaskSlot inputSlot = new MaskSlot("Height", SlotDirection.Input, 0);
        public readonly MaskSlot curvatureSlot = new MaskSlot("Convexity", SlotDirection.Output, 100);
        public readonly MaskSlot concavitySlot = new MaskSlot("Concavity", SlotDirection.Output, 101);

        public const float MIN_RADIUS = 0.1f;
        public const float MAX_RADIUS = 10f;
        public const float MIN_MAX_DEVIATION = 0.01f;
        public const float MAX_MAX_DEVIATION = 100f;

        [SerializeField]
        private float m_radius;
        public float radius
        {
            get
            {
                return m_radius;
            }
            set
            {
                m_radius = Mathf.Clamp(value, MIN_RADIUS, MAX_RADIUS);
            }
        }

        [SerializeField]
        private float m_maxDeviation;
        public float maxDeviation
        {
            get
            {
                return m_maxDeviation;
            }
            set
            {
                m_maxDeviation = Mathf.Clamp(value, MIN_MAX_DEVIATION, MAX_MAX_DEVIATION);
            }
        }

        private static readonly string SHADER_NAME = "Hidden/Vista/Graph/Curvature";
        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int LOW_PASS_TEX = Shader.PropertyToID("_LowPassTex");
        private static readonly int RADIUS = Shader.PropertyToID("_Radius");
        private static readonly int DEVIATION_SCALE = Shader.PropertyToID("_DeviationScale");
        private static readonly int SIGN = Shader.PropertyToID("_Sign");
        private static readonly int PASS_BLUR = 0;
        private static readonly int PASS_COMPOSITE = 1;

        private static readonly string TEMP_DOWNSAMPLE_NAME = "~CurvatureDownsample";
        private static readonly string TEMP_LOW_PASS_NAME = "~CurvatureLowPass";

        // The low pass grid keeps at least this many texels per radius so the local average stays clean.
        private const float SAMPLES_PER_RADIUS = 4f;

        public CurvatureNode() : base()
        {
            m_radius = 5f;
            m_maxDeviation = 2f;
        }

        public override IEnumerator Execute(GraphContext context)
        {
            ExecuteImmediate(context);
            yield return null;
        }

        public override void ExecuteImmediate(GraphContext context)
        {
            int baseResolution = context.GetArg(Args.RESOLUTION).intValue;
            SlotRef inputRefLink = context.GetInputLink(m_id, inputSlot.id);
            Texture inputTexture = context.GetTexture(inputRefLink);
            int inputResolution;
            if (inputTexture == null)
            {
                inputTexture = Texture2D.blackTexture;
                inputResolution = baseResolution;
            }
            else
            {
                inputResolution = inputTexture.width;
            }

            int resolution = this.CalculateResolution(baseResolution, inputResolution);

            Vector4 worldBounds = context.GetArg(Args.WORLD_BOUNDS).vectorValue;
            float terrainHeight = Mathf.Max(1f, context.GetArg(Args.TERRAIN_HEIGHT).floatValue);
            float worldSize = Mathf.Max(1f, worldBounds.z);

            float nativePixelSize = worldSize / resolution;
            // Keep the radius meaningful when it is smaller than what the resolution can represent.
            float effectiveRadius = Mathf.Max(m_radius, 2f * nativePixelSize);
            // The low pass grid is locked to world meters so every resolution computes the same average surface.
            float blurPixelSize = Mathf.Max(nativePixelSize, effectiveRadius / SAMPLES_PER_RADIUS);
            // Render target resolutions must be a multiple of 8, round up to the next boundary.
            int blurResolution = Utilities.MultipleOf8(Mathf.CeilToInt(worldSize / blurPixelSize));
            blurResolution = Mathf.Clamp(blurResolution, 8, resolution);
            float blurTexelRadius = effectiveRadius * blurResolution / worldSize;

            // Progressive halving avoids aliasing that a single big downsample step would introduce.
            // The whole chain is small, roughly one third of the input size, so it is kept alive
            // until the blur pass has consumed the final grid, then released in one batch.
            Texture currentTexture = inputTexture;
            int currentResolution = inputResolution;
            int downsampleStepCount = 0;
            while (currentResolution > blurResolution * 2)
            {
                currentResolution = Utilities.MultipleOf8(currentResolution / 2);
                string tempName = TEMP_DOWNSAMPLE_NAME + downsampleStepCount;
                DataPool.RtDescriptor halfDesc = DataPool.RtDescriptor.Create(currentResolution, currentResolution, RenderTextureFormat.RFloat);
                RenderTexture halfRt = context.CreateTemporaryRT(halfDesc, tempName);
                Drawing.Blit(currentTexture, halfRt);
                currentTexture = halfRt;
                downsampleStepCount += 1;
            }
            if (currentResolution != blurResolution)
            {
                string tempName = TEMP_DOWNSAMPLE_NAME + downsampleStepCount;
                DataPool.RtDescriptor gridDesc = DataPool.RtDescriptor.Create(blurResolution, blurResolution, RenderTextureFormat.RFloat);
                RenderTexture gridRt = context.CreateTemporaryRT(gridDesc, tempName);
                Drawing.Blit(currentTexture, gridRt);
                currentTexture = gridRt;
                downsampleStepCount += 1;
            }

            Material material = new Material(ShaderUtilities.Find(SHADER_NAME));
            context.RegisterDestroyLater(material);

            DataPool.RtDescriptor lowPassDesc = DataPool.RtDescriptor.Create(blurResolution, blurResolution, RenderTextureFormat.RFloat);
            RenderTexture lowPassRt = context.CreateTemporaryRT(lowPassDesc, TEMP_LOW_PASS_NAME);
            material.SetTexture(MAIN_TEX, currentTexture);
            material.SetFloat(RADIUS, blurTexelRadius);
            Drawing.DrawQuad(lowPassRt, material, PASS_BLUR);
            for (int stepIndex = 0; stepIndex < downsampleStepCount; ++stepIndex)
            {
                context.ReleaseTemporary(TEMP_DOWNSAMPLE_NAME + stepIndex);
            }

            // Height values are normalized against terrain height, rescale so max deviation is expressed in meters.
            float deviationScale = terrainHeight / m_maxDeviation;
            material.SetTexture(MAIN_TEX, inputTexture);
            material.SetTexture(LOW_PASS_TEX, lowPassRt);
            material.SetFloat(DEVIATION_SCALE, deviationScale);

            SlotRef curvatureRef = new SlotRef(m_id, curvatureSlot.id);
            DataPool.RtDescriptor curvatureDesc = DataPool.RtDescriptor.Create(resolution, resolution, RenderTextureFormat.RFloat);
            RenderTexture curvatureRt = context.CreateRenderTarget(curvatureDesc, curvatureRef);
            material.SetFloat(SIGN, 1f);
            Drawing.DrawQuad(curvatureRt, material, PASS_COMPOSITE);

            SlotRef concavityRef = new SlotRef(m_id, concavitySlot.id);
            if (context.GetReferenceCount(concavityRef) > 0)
            {
                DataPool.RtDescriptor concavityDesc = DataPool.RtDescriptor.Create(resolution, resolution, RenderTextureFormat.RFloat);
                RenderTexture concavityRt = context.CreateRenderTarget(concavityDesc, concavityRef);
                material.SetFloat(SIGN, -1f);
                Drawing.DrawQuad(concavityRt, material, PASS_COMPOSITE);
            }

            context.ReleaseReference(inputRefLink);
            context.ReleaseTemporary(TEMP_LOW_PASS_NAME);
        }

        public override void Bypass(GraphContext context)
        {
            return;
        }
    }
}
#endif
