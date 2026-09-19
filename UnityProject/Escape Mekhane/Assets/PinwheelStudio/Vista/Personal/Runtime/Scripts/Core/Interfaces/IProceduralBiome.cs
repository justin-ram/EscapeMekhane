#if VISTA
using Pinwheel.Vista.Graph;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Extends <see cref="IBiome"/> with a terrain-graph-driven biome source.
    /// </summary>
    public interface IProceduralBiome : IBiome
    {
        /// <summary>
        /// Gets or sets the terrain graph used to generate biome data for this biome.
        /// </summary>
        TerrainGraph terrainGraph { get; set; }

        /// <summary>
        /// Creates the external input provider that feeds this biome's graph in biome context (bounds,
        /// exposed properties, biome specific inputs). Used to open the graph editor in context. Defaults
        /// to null (open the graph without biome context) for procedural biome types that do not supply one.
        /// </summary>
        IExternalInputProvider CreateGraphInputProvider() => null;
    }
}
#endif


