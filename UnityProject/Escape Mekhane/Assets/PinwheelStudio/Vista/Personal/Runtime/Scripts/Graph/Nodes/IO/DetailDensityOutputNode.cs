#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Detail Density Output",
        path = "IO/Detail Density Output",
        icon = "",
        documentation = "",
        keywords = "grass, detail",
        description = "Output to the terrain's detail density map. \nPolaris terrains don't use density map, use Detail Instance Output Node instead.")]
    public class DetailDensityOutputNode : DetailDensityOutputNodeBase
    {
        [SerializeAsset]
        private DetailTemplate m_detailTemplate;
        public override DetailTemplate detailTemplate
        {
            get { return m_detailTemplate; }
            set { m_detailTemplate = value; }
        }

        public DetailDensityOutputNode()
        {
            m_detailTemplate = null;
        }
    }
}
#endif
