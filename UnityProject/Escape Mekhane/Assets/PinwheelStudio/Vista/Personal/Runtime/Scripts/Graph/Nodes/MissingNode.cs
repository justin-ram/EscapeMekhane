#if VISTA
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    /// <summary>
    /// In memory placeholder used when a serialized node type cannot be resolved in the current project.
    /// </summary>
    [System.Serializable]
    public class MissingNode : ExecutableNodeBase, IPreservedSerializedData
    {
        [SerializeField]
        private Serializer.JsonObject m_preservedData;
        public Serializer.JsonObject preservedSerializedData
        {
            get
            {
                return m_preservedData;
            }
        }

        public string missingTypeName
        {
            get
            {
                return m_preservedData.typeInfo.fullName;
            }
        }

        private List<ISlot> m_syntheticSlots;

        public MissingNode() : base()
        {
            m_syntheticSlots = new List<ISlot>();
        }

        public void SetPreservedSerializedData(Serializer.JsonObject preservedData)
        {
            m_preservedData = preservedData;
        }

        public void AddSyntheticSlot(ISlot slot)
        {
            if (slot == null)
            {
                return;
            }

            for (int i = 0; i < m_syntheticSlots.Count; ++i)
            {
                ISlot existingSlot = m_syntheticSlots[i];
                if (existingSlot != null && existingSlot.id == slot.id)
                {
                    return;
                }
            }

            m_syntheticSlots.Add(slot);
        }

        public void AddSyntheticSlot(Type slotType, SlotDirection direction, int slotId)
        {
            ISlot slot = CreateSyntheticSlot(slotType, direction, slotId);
            AddSyntheticSlot(slot);
        }

        public override ISlot GetSlot(int id)
        {
            for (int i = 0; i < m_syntheticSlots.Count; ++i)
            {
                ISlot slot = m_syntheticSlots[i];
                if (slot != null && slot.id == id)
                {
                    return slot;
                }
            }

            return null;
        }

        public override ISlot[] GetInputSlots()
        {
            return GetSyntheticSlots(SlotDirection.Input);
        }

        public override ISlot[] GetOutputSlots()
        {
            return GetSyntheticSlots(SlotDirection.Output);
        }

        private ISlot[] GetSyntheticSlots(SlotDirection direction)
        {
            List<ISlot> slots = new List<ISlot>();
            for (int i = 0; i < m_syntheticSlots.Count; ++i)
            {
                ISlot slot = m_syntheticSlots[i];
                if (slot != null && slot.direction == direction)
                {
                    slots.Add(slot);
                }
            }

            return slots.ToArray();
        }

        public override void ExecuteImmediate(GraphContext context)
        {
            Debug.LogWarning($"Cannot execute missing node type [{missingTypeName}]. Install the module that provides this node to restore its behavior.");
        }

        public override void Bypass(GraphContext context)
        {
        }

        private static ISlot CreateSyntheticSlot(Type slotType, SlotDirection direction, int slotId)
        {
            string slotName = $"Slot {slotId}";

            try
            {
                ISlot slot = Activator.CreateInstance(slotType, slotName, direction, slotId) as ISlot;
                if (slot != null)
                {
                    return slot;
                }
            }
            catch (Exception)
            {
            }

            return new MaskSlot(slotName, direction, slotId);
        }
    }
}
#endif
