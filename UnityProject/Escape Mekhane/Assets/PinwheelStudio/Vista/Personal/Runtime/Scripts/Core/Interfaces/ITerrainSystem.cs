#if VISTA
using System;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Defines the integration contract between Vista and a terrain backend.
    /// </summary>
    /// <remarks>
    /// Terrain-system implementations identify the backend's terrain and tile component types and ensure that target
    /// terrain objects are equipped with a compatible <see cref="ITile"/> adapter for Vista generation.
    /// </remarks>
    public interface ITerrainSystem
    {
        /// <summary>
        /// Gets the display label used for this terrain system in setup and selection workflows.
        /// </summary>
        string terrainLabel { get; }
        /// <summary>
        /// Gets the biome output channels this backend can actually consume.
        /// </summary>
        /// <remarks>
        /// The default derives the set from the populator interfaces the tile component implements. A backend that
        /// consumes geometry but ignores a sub channel, such as Unity Terrain with mesh density, overrides this to clear
        /// the unused bit. The manager unions this across the terrain systems present in a generation, then intersects the
        /// union with each biome's data mask so a channel is generated only when some present backend consumes it and the
        /// user asked for it.
        /// </remarks>
        BiomeDataMask supportedData => TerrainSystemUtilities.DeriveSupportedData(GetTileComponentType());
        /// <summary>
        /// Gets the component type that represents a terrain object in this backend.
        /// </summary>
        /// <returns>The backend terrain component type.</returns>
        Type GetTerrainComponentType();
        /// <summary>
        /// Gets the component type that implements <see cref="ITile"/> for this backend.
        /// </summary>
        /// <returns>The backend tile component type.</returns>
        Type GetTileComponentType();
        /// <summary>
        /// Ensures a compatible tile component is configured on a target terrain object.
        /// </summary>
        /// <param name="manager">Manager that owns the tile.</param>
        /// <param name="target">Target object containing terrain components.</param>
        /// <returns>The configured tile, or <see langword="null"/> when setup is not possible.</returns>
        ITile SetupTile(VistaManager manager, GameObject target);
        /// <summary>
        /// Creates a grid of connected terrain objects for this backend, described by <paramref name="context"/>.
        /// </summary>
        /// <remarks>
        /// Each tile is sized and positioned into the grid, parented under the context parent when one is given,
        /// and the tiles' edges are stitched to their neighbors before the grid is returned. This is pure runtime
        /// work. No asset files are written, the caller saves any backend data to assets afterward when it runs in
        /// the editor. The tiles are not bound to any manager, the caller binds each one through <see cref="SetupTile"/>.
        /// </remarks>
        /// <param name="context">The grid description, holding the tile counts, the tile size, the texture density, and the parent.</param>
        /// <returns>The created tiles indexed as grid[column, row], or an empty array when creation is not possible.</returns>
        GameObject[,] CreateTerrainGrid(TerrainGridCreationContext context);
    }
}
#endif


