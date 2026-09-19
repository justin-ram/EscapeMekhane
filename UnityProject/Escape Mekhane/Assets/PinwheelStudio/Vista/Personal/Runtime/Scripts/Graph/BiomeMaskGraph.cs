#if VISTA
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    [HelpURL("https://docs.pinwheelstud.io/vista/docs/big-world-creation-biome-mask-graph.html")]
    /// <summary>
    /// Specialized terrain graph used to post-process the biome mask generated for a Local Procedural Biome.
    /// </summary>
    /// <remarks>
    /// This graph does not replace the main terrain graph. It runs after the biome's base or adjusted
    /// mask has already been rendered, using that mask as the dedicated biome-mask input and evaluating
    /// only the output named by <see cref="GraphConstants.BIOME_MASK_OUTPUT_NAME"/>.
    /// </remarks>
    public class BiomeMaskGraph : TerrainDataGraph
    {
        internal static readonly List<string> ACCEPTED_NODE_CATEGORIES = new List<string>()
        {
            "Base Shape",
            "Adjustments"
        };

        internal static readonly List<Type> ACCEPTED_NODE_TYPES = new List<Type>()
        {
            typeof(InputNode),
            typeof(OutputNode),
            typeof(SetVariableNode),
            typeof(GetVariableNode),
            typeof(CombineNode),
            typeof(MathNode),
            typeof(ValueCheckNode),
            typeof(LoadTextureNode),
            typeof(AnchorNode),
            typeof(FalloffDetailNode)
        };

        /// <summary>
        /// Determines whether a node type is allowed inside a biome mask graph.
        /// </summary>
        /// <param name="t">
        /// The node type being added or validated.
        /// </param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="t"/> belongs to the limited set of shape,
        /// adjustment, variable, I/O, and utility nodes supported by biome-mask post-processing;
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The restriction keeps biome mask graphs focused on 2D mask authoring. Categories such as
        /// <c>Base Shape</c> and <c>Adjustments</c> are accepted by metadata, while a few additional
        /// utility nodes are whitelisted explicitly.
        /// </remarks>
        public override bool AcceptNodeType(Type t)
        {
            NodeMetadataAttribute meta = NodeMetadata.Get(t);
            if (meta != null)
            {
                string category = meta.GetCategory();
                if (ACCEPTED_NODE_CATEGORIES.Contains(category))
                    return true;
            }

            if (ACCEPTED_NODE_TYPES.Contains(t))
                return true;

            return false;
        }

    }
}
#endif


