#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Tree Slot Output",
        path = "IO/Tree Slot Output",
        icon = "",
        documentation = "",
        keywords = "tree, instance, prefab, slot",
        description = "Output tree instances through a named tree slot. The graph provides a default template, while compatible terrain tools can let users select another tree template.")]
    public class TreeSlotOutputNode : TreeOutputNodeBase
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
        private TreeTemplate m_treeTemplate;
        [NonExposable]
        public override TreeTemplate treeTemplate
        {
            get { return m_treeTemplate; }
            set { m_treeTemplate = value; }
        }

        public TreeSlotOutputNode()
        {
            m_slotName = "Tree";
            m_treeTemplate = null;
        }
    }
}
#endif
