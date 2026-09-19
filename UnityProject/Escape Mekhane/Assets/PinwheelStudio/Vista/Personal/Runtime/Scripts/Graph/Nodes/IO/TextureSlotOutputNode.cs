#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Texture Slot Output",
        path = "IO/Texture Slot Output",
        icon = "",
        documentation = "",
        keywords = "texture, layer, weight, splat, slot",
        description = "Output a terrain texture weight map through a named texture slot. The graph provides a default terrain layer, while compatible terrain tools can let users select another layer.")]
    public class TextureSlotOutputNode : TextureOutputNodeBase
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
        private TerrainLayer m_terrainLayer;
        [NonExposable]
        public override TerrainLayer terrainLayer
        {
            get { return m_terrainLayer; }
            set { m_terrainLayer = value; }
        }

        public TextureSlotOutputNode()
        {
            m_slotName = "Texture";
            m_terrainLayer = null;
        }
    }
}
#endif
