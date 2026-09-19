#if VISTA
using UnityEditor;
using Pinwheel.Vista.Graph;
using System.Collections.Generic;

namespace Pinwheel.VistaEditor.Graph
{
    public class TerrainGraphCommandHandler : GraphCommandHandlerBase<TerrainDataGraph>
    {
        protected override void CopyGraphData(TerrainDataGraph from, TerrainDataGraph to)
        {
            string toName = to.name;
            string json = EditorJsonUtility.ToJson(from);
            EditorJsonUtility.FromJsonOverwrite(json, to);
            to.name = toName;
        }
    }
}
#endif
