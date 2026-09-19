#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Detail Density Slot Output",
        path = "IO/Detail Density Slot Output",
        icon = "",
        documentation = "",
        keywords = "grass, detail, density, slot",
        description = "Output a detail density map through a named grass slot for terrain tools to assign.")]
    public class DetailDensitySlotOutputNode : DetailDensityOutputNodeBase
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

        public DetailDensitySlotOutputNode()
        {
            m_slotName = "Detail";
            m_detailTemplate = null;
        }
    }
}
#endif
