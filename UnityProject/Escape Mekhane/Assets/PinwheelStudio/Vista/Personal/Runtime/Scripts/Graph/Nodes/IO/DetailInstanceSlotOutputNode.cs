#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Detail Instance Slot Output",
        path = "IO/Detail Instance Slot Output",
        icon = "",
        documentation = "",
        keywords = "grass, detail, instance, slot",
        description = "Output detail instances through a named detail slot for terrain tools to assign.")]
    public class DetailInstanceSlotOutputNode : DetailInstanceOutputNodeBase
    {
        [SerializeField]
        private string m_slotName;
        [NonExposable]
        public string slotName
        {
            get { return m_slotName; }
            set { m_slotName = value; }
        }

        [SerializeAsset]
        private DetailTemplate m_detailTemplate;
        [NonExposable]
        public override DetailTemplate detailTemplate
        {
            get { return m_detailTemplate; }
            set { m_detailTemplate = value; }
        }

        public DetailInstanceSlotOutputNode()
        {
            m_slotName = "Detail";
            m_detailTemplate = null;
        }
    }
}
#endif
