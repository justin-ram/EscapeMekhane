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
        title = "Points",
        path = "General/Points",
        description = "Generate a grid of points with desired spacing in meters. Use in conjunction with Offset and Spread node to have a randomly looking points set.")]
    public class PointsNode : ExecutableNodeBase
    {
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

        public PointsNode() : base()
        {
            m_spacing = new Vector2(100, 100);
        }
        public override void ExecuteImmediate(GraphContext context)
        {
            Vector4 biomeBounds = context.GetArg(Args.WORLD_BOUNDS).vectorValue;
            Vector2 lowerLeftPoint;
            Vector2Int gridDimension = VistaLib.CalculatePointGrid(biomeBounds, m_spacing, out lowerLeftPoint);

            int instanceCount = gridDimension.x * gridDimension.y;
            if (instanceCount == 0)
                return;

            SlotRef outputRef = new SlotRef(m_id, outputSlot.id);
            DataPool.BufferDescriptor desc = DataPool.BufferDescriptor.Create(instanceCount * PositionSample.SIZE);
            ComputeBuffer positionOutputBuffer = context.CreateBuffer(desc, outputRef);

            VistaLib.GeneratePoints(positionOutputBuffer, biomeBounds, m_spacing, lowerLeftPoint, gridDimension);
        }
    }
}
#endif


