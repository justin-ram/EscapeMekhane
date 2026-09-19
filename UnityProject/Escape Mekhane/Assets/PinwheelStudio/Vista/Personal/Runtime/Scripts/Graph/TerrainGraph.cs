#if VISTA
using UnityEngine;
using System;
using System.Collections.Generic;

namespace Pinwheel.Vista.Graph
{
    /// <summary>
    /// Terrain-data graph authored for biome generation.
    /// </summary>
    [CreateAssetMenu(menuName = "Vista/Terrain Graph", order = -10000)]
    [HelpURL("https://docs.pinwheelstud.io/vista/docs/terrain-graph.html")]
    public class TerrainGraph : TerrainDataGraph
    {
        internal static readonly List<Type> REJECTED_NODE_TYPES = new List<Type>()
        {
            typeof(TextureSlotOutputNode),
            typeof(TreeSlotOutputNode),
            typeof(DetailDensitySlotOutputNode),
            typeof(DetailInstanceSlotOutputNode)
        };

        public override bool AcceptNodeType(Type t)
        {
            if (REJECTED_NODE_TYPES.Contains(t))
                return false;

            return base.AcceptNodeType(t);
        }
    }
}
#endif
