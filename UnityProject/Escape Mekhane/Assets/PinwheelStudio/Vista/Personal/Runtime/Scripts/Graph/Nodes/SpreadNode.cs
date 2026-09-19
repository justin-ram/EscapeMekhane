#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Spread",
        path = "General/Spread",
        icon = "",
        documentation = "",
        keywords = "",
        description = "Spread the source positions into many others. Use this node to have a more realistic tree spawning.")]
    public class SpreadNode : ExecutableNodeBase, IHasSeed
    {
        public readonly BufferSlot inputPositionSlot = new BufferSlot("Positions", SlotDirection.Input, 0);
        public readonly MaskSlot distanceSlot = new MaskSlot("Distance", SlotDirection.Input, 1);
        public readonly BufferSlot outputSlot = new BufferSlot("Output", SlotDirection.Output, 100);

        [SerializeField]
        private int m_count;
        public int count
        {
            get
            {
                return m_count;
            }
            set
            {
                m_count = Mathf.Max(0, Utilities.MultipleOf8(Mathf.Clamp(value, 0, 15)) - 1);
            }
        }

        [SerializeField]
        private float m_distance;
        public float distance
        {
            get
            {
                return m_distance;
            }
            set
            {
                m_distance = Mathf.Clamp01(value);
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
        private bool m_keepSourcePoints;
        public bool keepSourcePoints
        {
            get
            {
                return m_keepSourcePoints;
            }
            set
            {
                m_keepSourcePoints = value;
            }
        }

        public SpreadNode() : base()
        {
            m_count = 7;
            m_distance = 0.05f;
            m_seed = 0;
            m_keepSourcePoints = true;
        }
        public override void ExecuteImmediate(GraphContext context)
        {
            SlotRef inputPositionRefLink = context.GetInputLink(m_id, inputPositionSlot.id);
            ComputeBuffer inputBuffer = context.GetBuffer(inputPositionRefLink);
            if (inputBuffer == null)
            {
                return;
            }
            if (inputBuffer.count % PositionSample.SIZE != 0)
            {
                Debug.LogError($"Cannot parse buffer {inputPositionSlot.name}, node id {m_id}");
                return;
            }

            SlotRef distanceMapRefLink = context.GetInputLink(m_id, distanceSlot.id);
            Texture distanceMap = context.GetTexture(distanceMapRefLink);

            SlotRef outputRef = new SlotRef(m_id, outputSlot.id);
            DataPool.BufferDescriptor desc = DataPool.BufferDescriptor.Create(inputBuffer.count * (m_count + 1));
            ComputeBuffer outputBuffer = context.CreateBuffer(desc, outputRef);

            int baseSeed = context.GetArg(Args.SEED).intValue;
            VistaLib.Spread(inputBuffer, outputBuffer, distanceMap, m_distance, m_count, m_keepSourcePoints, m_seed, baseSeed);

            context.ReleaseReference(inputPositionRefLink);
            context.ReleaseReference(distanceMapRefLink);
        }
    }
}
#endif


