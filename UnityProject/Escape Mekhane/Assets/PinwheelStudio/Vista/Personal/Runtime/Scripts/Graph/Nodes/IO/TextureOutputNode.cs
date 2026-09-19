#if VISTA
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [NodeMetadata(
        title = "Texture Output",
        path = "IO/Texture Output",
        icon = "",
        documentation = "",
        keywords = "",
        description = "Output the terrain's weight map (or splat control map, alpha map) of a terrain layer.\nTips: Use Weight Blend node to adjust the weight map to ensure the final result is not overshoot.")]
    public class TextureOutputNode : TextureOutputNodeBase
    {
        [SerializeAsset]
        private TerrainLayer m_terrainLayer;
        public override TerrainLayer terrainLayer
        {
            get
            {
                return m_terrainLayer;
            }
            set
            {
                m_terrainLayer = value;
            }
        }

        public TextureOutputNode() : base()
        {
        }
    }
}
#endif


