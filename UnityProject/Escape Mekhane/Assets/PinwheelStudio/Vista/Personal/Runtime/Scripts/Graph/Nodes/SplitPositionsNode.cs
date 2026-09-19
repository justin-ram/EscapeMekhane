#if VISTA
using System.Collections;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Split Positions",
        path = "General/Split Positions",
        icon = "",
        documentation = "",
        keywords = "split, distribute, variants, position",
        description = "Distribute each valid position to exactly one output. Higher integer weights make an output more likely.")]
    public class SplitPositionsNode : ExecutableNodeBase, IHasDynamicSlotCount, IHasSeed
    {
        public readonly BufferSlot inputSlot = new BufferSlot("Input", SlotDirection.Input, 0);

        [SerializeField]
        private BufferSlot[] m_outputSlots;
        public BufferSlot[] outputSlots { get { return m_outputSlots; } }

        [SerializeField]
        private int[] m_weights;
        public int[] weights { get { return m_weights; } }

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

        [SerializeField]
        private int m_seed;
        public int seed { get { return m_seed; } set { m_seed = value; } }

        public const int MIN_OUTPUT = 2;
        public const int MAX_OUTPUT = 100;
        public const int MAX_WEIGHT = 1000000;
        public event IHasDynamicSlotCount.SlotsChangedHandler slotsChanged;

        private const string SHADER_NAME = "Vista/Shaders/Graph/SplitPositions";
        private static readonly int SOURCE = Shader.PropertyToID("_SrcBuffer");
        private static readonly int DEST = Shader.PropertyToID("_DestBuffer");
        private static readonly int BASE_INDEX = Shader.PropertyToID("_BaseIndex");
        private static readonly int SAMPLE_COUNT = Shader.PropertyToID("_SampleCount");
        private static readonly int RANGE_START = Shader.PropertyToID("_RangeStart");
        private static readonly int RANGE_END = Shader.PropertyToID("_RangeEnd");
        private static readonly int TOTAL_WEIGHT = Shader.PropertyToID("_TotalWeight");
        private static readonly int SEED = Shader.PropertyToID("_Seed");
        private const int KERNEL = 0;
        private const int THREADS_PER_GROUP = 8;
        // Keep each dispatch below Vista's 64K-sample chunk limit; the shader uses 8 threads per group.
        private const int MAX_SAMPLES_PER_DISPATCH = 64000;
        private const int MAX_GROUPS_PER_DISPATCH = MAX_SAMPLES_PER_DISPATCH / THREADS_PER_GROUP;

        public SplitPositionsNode() : base()
        {
            m_outputCount = MIN_OUTPUT;
            UpdateSlots();
        }

        private void UpdateSlots()
        {
            m_outputCount = Mathf.Clamp(m_outputCount, MIN_OUTPUT, MAX_OUTPUT);
            int[] oldWeights = m_weights;
            m_weights = new int[m_outputCount];
            m_outputSlots = new BufferSlot[m_outputCount];
            for (int i = 0; i < m_outputCount; ++i)
            {
                m_weights[i] = oldWeights != null && i < oldWeights.Length ? Mathf.Clamp(oldWeights[i], 1, MAX_WEIGHT) : 1;
                m_outputSlots[i] = new BufferSlot($"Output {i + 1}", SlotDirection.Output, 100 + i);
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
        public override ISlot[] GetOutputSlots()
        {
            EnsureSlots();
            return m_outputSlots;
        }
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
            ComputeBuffer input = context.GetBuffer(inputRef);
            if (input == null)
                return;
            if (input.count % PositionSample.SIZE != 0)
            {
                Debug.LogError($"Cannot parse position buffer, node id {m_id}");
                context.ReleaseReference(inputRef);
                return;
            }

            int sampleCount = input.count / PositionSample.SIZE;
            int totalWeight = 0;
            for (int i = 0; i < m_weights.Length; ++i)
                totalWeight += m_weights[i];

            ComputeShader shader = Resources.Load<ComputeShader>(SHADER_NAME);
            context.RegisterUnloadLater(shader);
            shader.SetBuffer(KERNEL, SOURCE, input);
            shader.SetInt(SAMPLE_COUNT, sampleCount);
            shader.SetInt(TOTAL_WEIGHT, totalWeight);
            shader.SetInt(SEED, m_seed ^ context.GetArg(Args.SEED).intValue);

            int rangeStart = 1;
            for (int i = 0; i < m_outputSlots.Length; ++i)
            {
                int rangeEnd = rangeStart + m_weights[i] - 1;
                SlotRef outputRef = new SlotRef(m_id, m_outputSlots[i].id);
                DataPool.BufferDescriptor desc = DataPool.BufferDescriptor.Create(input.count);
                ComputeBuffer output = context.CreateBuffer(desc, outputRef);
                shader.SetBuffer(KERNEL, DEST, output);
                shader.SetInt(RANGE_START, rangeStart);
                shader.SetInt(RANGE_END, rangeEnd);

                int remainingGroups = (sampleCount + THREADS_PER_GROUP - 1) / THREADS_PER_GROUP;
                int dispatchIndex = 0;
                while (remainingGroups > 0)
                {
                    int groups = Mathf.Min(MAX_GROUPS_PER_DISPATCH, remainingGroups);
                    shader.SetInt(BASE_INDEX, dispatchIndex * MAX_SAMPLES_PER_DISPATCH);
                    shader.Dispatch(KERNEL, groups, 1, 1);
                    remainingGroups -= groups;
                    dispatchIndex += 1;
                }
                rangeStart = rangeEnd + 1;
            }
            context.ReleaseReference(inputRef);
        }
    }
}
#endif
