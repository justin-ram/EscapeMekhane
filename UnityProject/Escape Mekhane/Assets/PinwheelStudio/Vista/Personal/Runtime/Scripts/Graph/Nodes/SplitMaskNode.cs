#if VISTA
using Pinwheel.Vista.Graphics;
using System.Collections;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Split Mask",
        path = "General/Split Mask",
        icon = "",
        documentation = "",
        keywords = "split, distribute, variants, detail, grass, density, mask, weight",
        description = "Split a mask proportionally among multiple outputs. Higher integer weights receive a larger share; the output masks sum to the input.")]
    public class SplitMaskNode : ImageNodeBase, IHasDynamicSlotCount
    {
        public readonly MaskSlot inputSlot = new MaskSlot("Input", SlotDirection.Input, 0);

        [SerializeField]
        private MaskSlot[] m_outputSlots;
        public MaskSlot[] outputSlots { get { EnsureSlots(); return m_outputSlots; } }

        [SerializeField]
        private int[] m_weights;
        public int[] weights { get { EnsureSlots(); return m_weights; } }

        [SerializeField]
        private int m_outputCount;
        [NonExposable]
        public int outputCount
        {
            get { return m_outputCount; }
            set
            {
                int count = Mathf.Clamp(value, MIN_OUTPUT, MAX_OUTPUT);
                if (m_outputCount == count)
                    return;
                m_outputCount = count;
                UpdateSlots();
                slotsChanged?.Invoke(this);
            }
        }

        public const int MIN_OUTPUT = 2;
        public const int MAX_OUTPUT = 100;
        public const int MAX_WEIGHT = 1000000;
        public event IHasDynamicSlotCount.SlotsChangedHandler slotsChanged;

        private const string SHADER_NAME = "Hidden/Vista/Graph/SplitMask";
        private static readonly int SOURCE_MAP = Shader.PropertyToID("_SourceMap");
        private static readonly int FRACTION = Shader.PropertyToID("_Fraction");
        private const int PASS = 0;
        private Material m_material;

        public SplitMaskNode() : base()
        {
            m_outputCount = MIN_OUTPUT;
            UpdateSlots();
        }

        private void UpdateSlots()
        {
            m_outputCount = Mathf.Clamp(m_outputCount, MIN_OUTPUT, MAX_OUTPUT);
            int[] oldWeights = m_weights;
            m_weights = new int[m_outputCount];
            m_outputSlots = new MaskSlot[m_outputCount];
            for (int i = 0; i < m_outputCount; ++i)
            {
                m_weights[i] = oldWeights != null && i < oldWeights.Length ? Mathf.Clamp(oldWeights[i], 1, MAX_WEIGHT) : 1;
                m_outputSlots[i] = new MaskSlot($"Output {i + 1}", SlotDirection.Output, 100 + i);
            }
        }

        public void SetWeight(int index, int weight)
        {
            EnsureSlots();
            m_weights[index] = Mathf.Clamp(weight, 1, MAX_WEIGHT);
        }

        private void EnsureSlots()
        {
            if (m_outputSlots == null || m_weights == null || m_outputSlots.Length != m_outputCount || m_weights.Length != m_outputCount)
                UpdateSlots();
        }

        public override ISlot[] GetInputSlots() { return new ISlot[] { inputSlot }; }
        public override ISlot[] GetOutputSlots() { return outputSlots; }
        public override ISlot GetSlot(int id)
        {
            if (id == inputSlot.id)
                return inputSlot;
            EnsureSlots();
            int index = id - 100;
            return index >= 0 && index < m_outputSlots.Length ? m_outputSlots[index] : null;
        }

        public override IEnumerator Execute(GraphContext context)
        {
            ExecuteImmediate(context);
            yield return null;
        }

        public override void ExecuteImmediate(GraphContext context)
        {
            EnsureSlots();
            SlotRef inputRef = context.GetInputLink(m_id, inputSlot.id);
            Texture source = context.GetTexture(inputRef);
            int baseResolution = context.GetArg(Args.RESOLUTION).intValue;
            int inputResolution = source != null ? source.width : baseResolution;
            if (source == null)
                source = Texture2D.blackTexture;

            int resolution = this.CalculateResolution(baseResolution, inputResolution);
            DataPool.RtDescriptor desc = DataPool.RtDescriptor.Create(resolution, resolution);
            int totalWeight = 0;
            for (int i = 0; i < m_weights.Length; ++i)
                totalWeight += m_weights[i];

            m_material = new Material(ShaderUtilities.Find(SHADER_NAME));
            context.RegisterDestroyLater(m_material);
            m_material.SetTexture(SOURCE_MAP, source);

            for (int i = 0; i < m_outputSlots.Length; ++i)
            {
                SlotRef outputRef = new SlotRef(m_id, m_outputSlots[i].id);
                if (context.GetReferenceCount(outputRef) <= 0)
                    continue;

                RenderTexture target = context.CreateRenderTarget(desc, outputRef);
                m_material.SetFloat(FRACTION, (float)m_weights[i] / totalWeight);
                Drawing.DrawQuad(target, m_material, PASS);
            }
            context.ReleaseReference(inputRef);
        }
    }
}
#endif
