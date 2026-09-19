#if VISTA
using System;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Describes the terrain grid a backend should create. Passed to
    /// <see cref="ITerrainSystem.CreateTerrainGrid(TerrainGridCreationContext)"/>.
    /// </summary>
    /// <remarks>
    /// This carries only the intent of the grid. The tile counts, the size of each tile, and the desired
    /// texture density. The values are validated by the constructor and the fields are read only, so any
    /// instance a backend receives is always valid. The backend converts <see cref="texturePixelsPerMeter"/>
    /// into a concrete heightmap resolution that satisfies its own constraints, for example Unity Terrain
    /// requires a power of two plus one while Polaris accepts any value. Creating the grid is pure runtime
    /// work, no asset files are written.
    /// </remarks>
    public class TerrainGridCreationContext
    {
        /// <summary>
        /// Number of tiles in the grid. The x component is the column count, the y component is the row count.
        /// </summary>
        public readonly Vector2Int tileCount;

        /// <summary>
        /// Size of each tile in meters. The x component is the width, the y component is the height, the z
        /// component is the length.
        /// </summary>
        public readonly Vector3 tileSize;

        /// <summary>
        /// Optional transform the grid is created under, acting as the pivot of the grid. This is usually a
        /// dedicated terrain holder, not the Vista Manager, because the Manager hierarchy is reserved for biomes.
        /// When null the tiles are created at the scene root.
        /// </summary>
        public readonly Transform parent;

        /// <summary>
        /// Creates a validated grid description. Throws when any value would produce a degenerate grid, so an
        /// instance that exists is always usable by a backend.
        /// </summary>
        /// <param name="tileCount">Number of tiles, x for columns and y for rows. Both must be at least one.</param>
        /// <param name="tileSize">Tile size in meters, x for width, y for height, z for length. Every axis must be positive.</param>
        /// <param name="texturePixelsPerMeter">Desired texture density in pixels per meter. Must be positive. Defaults to one.</param>
        /// <param name="parent">Optional transform the grid is created under and pivoted on.</param>
        /// <exception cref="ArgumentException">
        /// Thrown when a tile count is below one, a tile dimension is not positive, or the texture density is not positive.
        /// </exception>
        public TerrainGridCreationContext(Vector2Int tileCount, Vector3 tileSize, Transform parent = null)
        {
            if (tileCount.x < 1 || tileCount.y < 1)
            {
                throw new ArgumentException(string.Format("Tile count must be at least one in each direction, was {0} by {1}.", tileCount.x, tileCount.y));
            }
            if (tileSize.x <= 0f || tileSize.y <= 0f || tileSize.z <= 0f)
            {
                throw new ArgumentException(string.Format("Tile size must be positive on every axis, was {0}.", tileSize));
            }

            this.tileCount = tileCount;
            this.tileSize = tileSize;
            this.parent = parent;
        }
    }
}
#endif
