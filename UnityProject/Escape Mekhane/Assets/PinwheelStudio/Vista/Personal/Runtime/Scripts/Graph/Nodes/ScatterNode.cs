#if VISTA
using System.Collections;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Scatter",
        path = "General/Scatter",
        icon = "",
        documentation = "",
        keywords = "scatter, sow, spawn, distribute, points, density, instance, grass, detail",
        description = "Scatter a set of points across the biome, driven by a density mask. Combines a jittered point grid with density based thinning, so a single node replaces a Points, Spread and Thin Out chain. Feed the output into an instance output node such as Detail Instance Output.")]
    public class ScatterNode : ExecutableNodeBase, IHasSeed
    {
        public readonly MaskSlot densityInputSlot = new MaskSlot("Density", SlotDirection.Input, 0);
        public readonly BufferSlot outputSlot = new BufferSlot("Output", SlotDirection.Output, 100);

        [SerializeField]
        private Vector2 m_spacing;
        public Vector2 spacing
        {
            get
            {
                return m_spacing;
            }
            set
            {
                float x = Mathf.Max(1, value.x);
                float y = Mathf.Max(1, value.y);
                m_spacing.Set(x, y);
            }
        }

        [SerializeField]
        private int m_spreadCount;
        public int spreadCount
        {
            get
            {
                return m_spreadCount;
            }
            set
            {
                m_spreadCount = Mathf.Max(0, Utilities.MultipleOf8(Mathf.Clamp(value, 0, 15)) - 1);
            }
        }

        [SerializeField]
        private float m_spreadDistance;
        public float spreadDistance
        {
            get
            {
                return m_spreadDistance;
            }
            set
            {
                m_spreadDistance = Mathf.Clamp01(value);
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

        [SerializeField]
        private float m_densityMultiplier;
        public float densityMultiplier
        {
            get
            {
                return m_densityMultiplier;
            }
            set
            {
                m_densityMultiplier = Mathf.Clamp(value, 0f, 2f);
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

        private static readonly string TEMP_GRID_BUFFER_NAME = "~ScatterGrid";
        private static readonly string TEMP_SPREAD_BUFFER_NAME = "~ScatterSpread";

        public ScatterNode() : base()
        {
            m_spacing = new Vector2(100, 100);
            m_spreadCount = 7;
            m_spreadDistance = 0.05f;
            m_keepSourcePoints = true;
            m_densityMultiplier = 1f;
            m_seed = 0;
        }
        public override IEnumerator Execute(GraphContext context)
        {
            ExecuteImmediate(context);
            yield return null;
        }
        public override void ExecuteImmediate(GraphContext context)
        {
            Vector4 biomeBounds = context.GetArg(Args.WORLD_BOUNDS).vectorValue;
            int baseSeed = context.GetArg(Args.SEED).intValue;

            Vector2 lowerLeftPoint;
            Vector2Int gridDimension = VistaLib.CalculatePointGrid(biomeBounds, m_spacing, out lowerLeftPoint);
            int gridInstanceCount = gridDimension.x * gridDimension.y;
            if (gridInstanceCount == 0)
                return;

            SlotRef densityRefLink = context.GetInputLink(m_id, densityInputSlot.id);
            Texture densityTexture = context.GetTexture(densityRefLink);

            DataPool.BufferDescriptor gridDesc = DataPool.BufferDescriptor.Create(gridInstanceCount * PositionSample.SIZE);
            ComputeBuffer gridBuffer = context.CreateTemporaryBuffer(gridDesc, TEMP_GRID_BUFFER_NAME);
            VistaLib.GeneratePoints(gridBuffer, biomeBounds, m_spacing, lowerLeftPoint, gridDimension);

            DataPool.BufferDescriptor spreadDesc = DataPool.BufferDescriptor.Create(gridBuffer.count * (m_spreadCount + 1));
            ComputeBuffer spreadBuffer = context.CreateTemporaryBuffer(spreadDesc, TEMP_SPREAD_BUFFER_NAME);
            VistaLib.Spread(gridBuffer, spreadBuffer, null, m_spreadDistance, m_spreadCount, m_keepSourcePoints, m_seed, baseSeed);

            SlotRef outputRef = new SlotRef(m_id, outputSlot.id);
            DataPool.BufferDescriptor outputDesc = DataPool.BufferDescriptor.Create(spreadBuffer.count);
            ComputeBuffer outputBuffer = context.CreateBuffer(outputDesc, outputRef);
            VistaLib.ThinOut(spreadBuffer, outputBuffer, densityTexture, m_densityMultiplier, m_seed, baseSeed);

            context.ReleaseReference(densityRefLink);
            context.ReleaseTemporary(TEMP_GRID_BUFFER_NAME);
            context.ReleaseTemporary(TEMP_SPREAD_BUFFER_NAME);
        }
    }
}
#endif
