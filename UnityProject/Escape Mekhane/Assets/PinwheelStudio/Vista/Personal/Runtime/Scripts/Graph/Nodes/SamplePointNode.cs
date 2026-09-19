#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graphics;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Sample Point",
        path = "General/Sample Point",
        icon = "",
        documentation = "",
        keywords = "point, position, sample, index",
        description = "Select a position sample using a normalized index and output its components as constant maps.")]
    public class SamplePointNode : ImageNodeBase
    {
        public readonly BufferSlot inputSlot = new BufferSlot("Positions", SlotDirection.Input, 0);
        public readonly MaskSlot xSlot = new MaskSlot("X", SlotDirection.Output, 100);
        public readonly MaskSlot ySlot = new MaskSlot("Y", SlotDirection.Output, 101);
        public readonly MaskSlot zSlot = new MaskSlot("Z", SlotDirection.Output, 102);
        public readonly MaskSlot validSlot = new MaskSlot("Valid", SlotDirection.Output, 103);

        [SerializeField]
        private float m_index;
        public float index
        {
            get { return m_index; }
            set { m_index = Mathf.Clamp01(value); }
        }

        private static readonly string SHADER_NAME = "Hidden/Vista/Graph/SamplePoint";
        private static readonly int POSITIONS = Shader.PropertyToID("_Positions");
        private static readonly int SAMPLE_INDEX = Shader.PropertyToID("_SampleIndex");
        private static readonly int CHANNEL_OFFSET = Shader.PropertyToID("_ChannelOffset");
        private static readonly int HAS_SAMPLE = Shader.PropertyToID("_HasSample");
        private static readonly int PASS = 0;

        public SamplePointNode() : base()
        {
            m_index = 0;
            m_resolutionOverride = ResolutionOverrideOptions.RelativeToGraph;
            m_resolutionMultiplier = 1;
        }

        public override void ExecuteImmediate(GraphContext context)
        {
            SlotRef inputRef = context.GetInputLink(m_id, inputSlot.id);
            ComputeBuffer positions = context.GetBuffer(inputRef);
            int sampleCount = context.GetArg(Args.STROKE_POINT_COUNT).intValue;
            bool hasSample = positions != null && sampleCount > 0 && positions.count >= sampleCount * PositionSample.SIZE;
            int sampleIndex = hasSample ? Mathf.RoundToInt(m_index * (sampleCount - 1)) : 0;

            int baseResolution = context.GetArg(Args.RESOLUTION).intValue;
            int resolution = this.CalculateResolution(baseResolution, baseResolution);
            DataPool.RtDescriptor descriptor = DataPool.RtDescriptor.Create(resolution, resolution);

            Material material = new Material(ShaderUtilities.Find(SHADER_NAME));
            context.RegisterDestroyLater(material);
            material.SetInt(HAS_SAMPLE, hasSample ? 1 : 0);
            material.SetInt(SAMPLE_INDEX, sampleIndex);
            if (hasSample)
                material.SetBuffer(POSITIONS, positions);

            DrawOutput(context, material, descriptor, xSlot, 1);
            DrawOutput(context, material, descriptor, ySlot, 2);
            DrawOutput(context, material, descriptor, zSlot, 3);
            DrawOutput(context, material, descriptor, validSlot, 0);

            context.ReleaseReference(inputRef);
        }

        private void DrawOutput(GraphContext context, Material material, DataPool.RtDescriptor descriptor, ISlot slot, int channelOffset)
        {
            material.SetInt(CHANNEL_OFFSET, channelOffset);
            SlotRef outputRef = new SlotRef(m_id, slot.id);
            RenderTexture output = context.CreateRenderTarget(descriptor, outputRef);
            Drawing.DrawQuad(output, material, PASS);
        }
    }
}
#endif
