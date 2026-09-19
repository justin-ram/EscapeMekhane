#if VISTA
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Tree Output",
        path = "IO/Tree Output",
        icon = "",
        documentation = "",
        keywords = "",
        description = "Output tree instances of a prefab (and its variants) to the terrain.")]
    public class TreeOutputNode : TreeOutputNodeBase
    {
        [SerializeAsset]
        private TreeTemplate m_treeTemplate;
        public override TreeTemplate treeTemplate
        {
            get
            {
                return m_treeTemplate;
            }
            set
            {
                m_treeTemplate = value;
            }
        }
        public TreeOutputNode() : base()
        {
            m_treeTemplate = null;
        }
    }
}
#endif


