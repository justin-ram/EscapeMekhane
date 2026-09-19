#if VISTA
using System;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Helpers for reasoning about what a terrain backend can consume from a biome.
    /// </summary>
    public static class TerrainSystemUtilities
    {
        /// <summary>
        /// Derives the set of biome output channels a tile type can consume from the populator interfaces it implements.
        /// </summary>
        /// <param name="tileComponentType">The tile component type, usually from <see cref="ITerrainSystem.GetTileComponentType"/>.</param>
        /// <returns>
        /// The union of channels backed by an implemented populator interface. A null type returns every channel so an
        /// unknown backend never strips anything.
        /// </returns>
        /// <remarks>
        /// The geometry populator covers height, hole, and mesh density together. A backend that consumes geometry but not
        /// mesh density, such as Unity Terrain, clears the mesh density bit in its own <see cref="ITerrainSystem.supportedData"/>.
        /// </remarks>
        public static BiomeDataMask DeriveSupportedData(Type tileComponentType)
        {
            if (tileComponentType == null)
            {
                return (BiomeDataMask)(~0);
            }

            BiomeDataMask mask = 0;
            if (typeof(IGeometryPopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.HeightMap | BiomeDataMask.HoleMap | BiomeDataMask.MeshDensityMap;
            }
            if (typeof(IAlbedoMapPopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.AlbedoMap;
            }
            if (typeof(IMetallicMapPopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.MetallicMap;
            }
            if (typeof(ILayerWeightsPopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.LayerWeightMaps;
            }
            if (typeof(ITreePopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.TreeInstances;
            }
            if (typeof(IDetailDensityPopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.DetailDensityMaps;
            }
            if (typeof(IDetailInstancePopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.DetailInstances;
            }
            if (typeof(IObjectPopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.ObjectInstances;
            }
            if (typeof(IGenericTexturePopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.GenericTextures;
            }
            if (typeof(IGenericBufferPopulator).IsAssignableFrom(tileComponentType))
            {
                mask |= BiomeDataMask.GenericBuffers;
            }
            return mask;
        }
    }
}
#endif
