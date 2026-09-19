#if VISTA
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Records a biome as "recent" when its graph is saved to disk. Hooks OnWillSaveAssets, which fires on
    /// an actual save (far less often than per edit, and only when the user has real changes worth saving),
    /// then records the loaded biome(s) whose terrain graph is among the saved assets. One scene scan per
    /// save. (Biome creation records itself at the create site.)
    /// </summary>
    public class RecentBiomesTracker : AssetModificationProcessor
    {
        private static string[] OnWillSaveAssets(string[] paths)
        {
            if (paths == null)
                return paths;

            HashSet<GraphAsset> savedGraphs = null;
            foreach (string path in paths)
            {
                GraphAsset graph = AssetDatabase.LoadAssetAtPath<GraphAsset>(path);
                if (graph != null)
                {
                    savedGraphs ??= new HashSet<GraphAsset>();
                    savedGraphs.Add(graph);
                }
            }

            if (savedGraphs == null)
                return paths;

            // Graph edits only apply to procedural biomes (any IProceduralBiome).
            foreach (MonoBehaviour behaviour in Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (behaviour is IProceduralBiome biome && biome.terrainGraph != null && savedGraphs.Contains(biome.terrainGraph))
                {
                    RecentBiomes.Record(biome);
                }
            }

            return paths;
        }
    }
}
#endif
