#if VISTA
using System;
using UnityEngine;
using Pinwheel.Vista.Graphics;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Input",
        path = "IO/Graph Input",
        icon = "",
        documentation = "",
        keywords = "",
        description = "A generic entry point of the graph which will be used to take data from the outside into the graph with sub-graph or in code.\nThis will create a corresponding slot in sub-graph node.")]
    public class InputNode : ExecutableNodeBase, IHasDynamicSlotCount, ISerializationCallbackReceiver
    {
        [System.NonSerialized]
        private ISlot m_outputSlot;
        public ISlot outputSlot
        {
            get
            {
                return m_outputSlot;
            }
        }

        [SerializeField]
        private Serializer.JsonObject m_outputSlotData;

        [SerializeField]
        private string m_inputName;
        public string inputName
        {
            get
            {
                return m_inputName;
            }
            set
            {
                m_inputName = value;
            }
        }

        [SerializeAsset]
        private Texture m_fallbackTexture;
        /// <summary>
        /// Gets or sets the texture used when no external texture is registered for this input.
        /// </summary>
        public Texture fallbackTexture
        {
            get
            {
                return m_fallbackTexture;
            }
            set
            {
                m_fallbackTexture = value;
            }
        }

        [System.NonSerialized]
        private string m_fallbackWarning;
        /// <summary>
        /// Gets the warning produced when the most recent execution used the fallback texture.
        /// </summary>
        internal string fallbackWarning
        {
            get
            {
                return m_fallbackWarning;
            }
        }

        [System.NonSerialized]
        private Type m_slotType;
        public Type slotType
        {
            get
            {
                return m_slotType;
            }
        }

        [SerializeField]
        private string m_slotTypeName;
        public event IHasDynamicSlotCount.SlotsChangedHandler slotsChanged;
        public InputNode() : base()
        {
            m_slotType = typeof(MaskSlot);
            CreateOutputSlot();
        }
        public void SetSlotType(Type t)
        {
            Type oldValue = m_slotType;
            Type newValue = t;
            m_slotType = newValue;
            if (oldValue != newValue)
            {
                CreateOutputSlot();
                if (slotsChanged != null)
                {
                    slotsChanged.Invoke(this);
                }
            }
        }

        private void CreateOutputSlot()
        {
            int id = 100;
            m_outputSlot = SlotProvider.Create(m_slotType, m_inputName, SlotDirection.Output, id);
        }
        public override void ExecuteImmediate(GraphContext context)
        {
            m_fallbackWarning = null;
            if (m_outputSlot == null)
                return;

            SlotRef outputRef = new SlotRef(m_id, outputSlot.id);
            if (m_outputSlot is BufferSlot)
            {
                if (string.IsNullOrEmpty(m_inputName))
                    return;

                context.SetExternal(outputRef, inputName);
                if (context.IsTargetNode(m_id))
                {
                    ComputeBuffer sourceBuffer = context.GetBuffer(outputRef);
                    if (sourceBuffer != null)
                    {
                        DataPool.BufferDescriptor desc = DataPool.BufferDescriptor.Create(sourceBuffer.count);
                        ComputeBuffer outputBuffer = context.CreateBuffer(desc, outputRef, false);
                        BufferHelper.Copy(sourceBuffer, outputBuffer);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(m_inputName) && context.HasExternalTexture(inputName))
            {
                context.SetExternal(outputRef, inputName);
                if (context.IsTargetNode(m_id))
                {
                    RenderTexture sourceTexture = context.GetTexture(outputRef);
                    DataPool.RtDescriptor desc = DataPool.RtDescriptor.Create(sourceTexture.width, sourceTexture.height, sourceTexture.format);
                    RenderTexture outputTexture = context.CreateRenderTarget(desc, outputRef, false);
                    Drawing.Blit(sourceTexture, outputTexture);
                }
            }
            else if (m_fallbackTexture != null)
            {
                m_fallbackWarning = string.IsNullOrEmpty(m_inputName) ?
                    "External input is not named. Using the fallback texture." :
                    $"External input '{m_inputName}' was not provided. Using the fallback texture.";
                int resolution = context.GetArg(Args.RESOLUTION).intValue;
                RenderTextureFormat format = m_outputSlot is ColorTextureSlot ?
                    RenderTextureFormat.ARGB32 :
                    RenderTextureFormat.RFloat;
                DataPool.RtDescriptor desc = DataPool.RtDescriptor.Create(resolution, resolution, format);
                RenderTexture outputTexture = context.CreateRenderTarget(desc, outputRef, false);
                Drawing.Blit(m_fallbackTexture, outputTexture);
            }
            else if (!string.IsNullOrEmpty(m_inputName))
            {
                context.SetExternal(outputRef, inputName);
            }
        }
        public void OnBeforeSerialize()
        {
            if (m_outputSlot != null)
            {
                m_outputSlotData = Serializer.Serialize<ISlot>(m_outputSlot);
            }
            else
            {
                m_outputSlotData = default;
            }

            if (m_slotType != null)
            {
                m_slotTypeName = m_slotType.FullName;
            }
        }
        public void OnAfterDeserialize()
        {
            if (!m_outputSlotData.Equals(default))
            {
                m_outputSlot = Serializer.Deserialize<ISlot>(m_outputSlotData);
            }
            else
            {
                m_outputSlot = null;
            }

            if (!string.IsNullOrEmpty(m_slotTypeName))
            {
                m_slotType = Type.GetType(m_slotTypeName);
            }
        }
        public override ISlot[] GetInputSlots()
        {
            return new ISlot[0];
        }
        public override ISlot[] GetOutputSlots()
        {
            if (m_outputSlot != null)
            {
                return new ISlot[1] { m_outputSlot };
            }
            else
            {
                return new ISlot[0];
            }
        }
        public override ISlot GetSlot(int id)
        {
            if (m_outputSlot != null && m_outputSlot.id == id)
            {
                return m_outputSlot;
            }
            return null;
        }
    }
}
#endif


