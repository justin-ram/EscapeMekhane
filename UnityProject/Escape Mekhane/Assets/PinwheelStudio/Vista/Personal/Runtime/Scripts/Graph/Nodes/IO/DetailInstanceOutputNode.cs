#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Detail Instance Output",
        path = "IO/Detail Instance Output",
        icon = "",
        documentation = "",
        keywords = "",
        description = "Output grass/detail instances to the terrain. Unity terrains don't use detail instances, use Detail Density Output Node instead.")]
    public class DetailInstanceOutputNode : DetailInstanceOutputNodeBase
    {
        [SerializeAsset]
        private DetailTemplate m_detailTemplate;
        public override DetailTemplate detailTemplate
        {
            get { return m_detailTemplate; }
            set { m_detailTemplate = value; }
        }

        public DetailInstanceOutputNode()
        {
            m_detailTemplate = null;
        }
    }
}
#endif
