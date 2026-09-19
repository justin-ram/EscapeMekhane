#if VISTA
using Pinwheel.Vista.Graphics;
using System.Collections;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    public abstract class DetailDensityOutputNodeBase : ImageNodeBase, IOutputNode
    {
        public readonly MaskSlot inputSlot = new MaskSlot("Input", SlotDirection.Input, 0);
        public readonly MaskSlot outputSlot = new MaskSlot("Output", SlotDirection.Output, 100);

        public abstract DetailTemplate detailTemplate { get; set; }
        public SlotRef mainOutputSlot => new SlotRef(m_id, outputSlot.id);

        [SerializeField]
        private float m_densityMultiplier;
        public float densityMultiplier
        {
            get { return m_densityMultiplier; }
            set { m_densityMultiplier = Mathf.Clamp(value, 0f, 2f); }
        }

        public override bool isBypassed
        {
            get { return false; }
            set { m_isBypassed = false; }
        }

        private static readonly string SHADER_NAME = "Hidden/Vista/Graph/DetailDensityOutput";
        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int DENSITY_MULTIPLIER = Shader.PropertyToID("_DensityMultiplier");
        private static readonly int PASS = 0;
        private Material m_material;

        protected DetailDensityOutputNodeBase()
        {
            m_densityMultiplier = 1;
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
            DataPool.RtDescriptor desc = DataPool.RtDescriptor.Create(resolution, resolution);
            SlotRef outputRef = new SlotRef(m_id, outputSlot.id);
            RenderTexture targetRt = context.CreateRenderTarget(desc, outputRef);
            m_material = new Material(ShaderUtilities.Find(SHADER_NAME));
            context.RegisterDestroyLater(m_material);
            m_material.SetTexture(MAIN_TEX, inputTexture);
            m_material.SetFloat(DENSITY_MULTIPLIER, m_densityMultiplier);
            Drawing.DrawQuad(targetRt, m_material, PASS);
            context.ReleaseReference(inputRefLink);
        }
    }
}
#endif
