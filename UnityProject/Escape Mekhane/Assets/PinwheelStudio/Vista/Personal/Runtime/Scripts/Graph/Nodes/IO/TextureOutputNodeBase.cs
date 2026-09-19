#if VISTA
using Pinwheel.Vista.Graphics;
using System.Collections;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    public abstract class TextureOutputNodeBase : ImageNodeBase, IOutputNode
    {
        public readonly MaskSlot inputSlot = new MaskSlot("Input", SlotDirection.Input, 0);
        public readonly MaskSlot outputSlot = new MaskSlot("Output", SlotDirection.Output, 100);

        public SlotRef mainOutputSlot
        {
            get { return new SlotRef(m_id, outputSlot.id); }
        }

        public abstract TerrainLayer terrainLayer { get; set; }

        [SerializeField]
        private int m_order;
        public int order
        {
            get { return m_order; }
            set { m_order = value; }
        }

        public override bool isBypassed
        {
            get { return false; }
            set { m_isBypassed = false; }
        }

        protected TextureOutputNodeBase()
        {
            m_order = 0;
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
            DataPool.RtDescriptor descriptor = DataPool.RtDescriptor.Create(
                resolution,
                resolution,
                RenderTextureFormat.RFloat);
            RenderTexture target = context.CreateRenderTarget(descriptor, mainOutputSlot);
            Drawing.Blit01(inputTexture, target);
            context.ReleaseReference(inputRefLink);
        }
    }
}
#endif
