#if VISTA
using UnityEngine;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pinwheel.Vista.UnityTerrain
{
    /// <summary>
    /// Represents unity terrain system.
    /// </summary>
    public class UnityTerrainSystem : ITerrainSystem
    {
        /// <summary>
        /// Gets or sets member.
        /// </summary>
        public string terrainLabel
        {
            get
            {
                return "Unity Terrain";
            }
        }

        /// <summary>
        /// Gets the biome output channels Unity Terrain consumes.
        /// </summary>
        /// <remarks>
        /// The tile implements the geometry populator, which the default reads as height, hole, and mesh density. Unity
        /// Terrain has no use for mesh density, so the bit is cleared here. The tile also implements no albedo, metallic, or
        /// detail instance populator, so those channels are already absent from the derived set.
        /// </remarks>
        public BiomeDataMask supportedData
        {
            get
            {
                return TerrainSystemUtilities.DeriveSupportedData(GetTileComponentType()) & ~BiomeDataMask.MeshDensityMap;
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod]
#endif
        /// <summary>
        /// Handles the initialize callback.
        /// </summary>
        public static void OnInitialize()
        {
            VistaManager.RegisterTerrainSystem<UnityTerrainSystem>();
        }

        /// <summary>
        /// Gets terrain component type.
        /// </summary>
        /// <returns>Get terrain component type result.</returns>
        public Type GetTerrainComponentType()
        {
            return typeof(Terrain);
        }

        /// <summary>
        /// Gets tile component type.
        /// </summary>
        /// <returns>Get tile component type result.</returns>
        public Type GetTileComponentType()
        {
            return typeof(TerrainTile);
        }

        /// <summary>
        /// Sets up tile.
        /// </summary>
        /// <param name="manager">Manager instance coordinating generation.</param>
        /// <param name="target">Target object to read from or write to.</param>
        /// <returns>Setup tile result.</returns>
        public ITile SetupTile(VistaManager manager, GameObject target)
        {
            Terrain terrain = target.GetComponent<Terrain>();
            if (terrain == null)
            {
                return null;
            }
            TerrainTile tile = target.GetComponent<TerrainTile>();
            if (tile == null)
            {
#if UNITY_EDITOR
                tile = Undo.AddComponent<TerrainTile>(target);
#else
                tile = target.AddComponent<TerrainTile>();
#endif
            }
#if UNITY_EDITOR
            Undo.RecordObject(tile, "Modify Terrain Tile");
#endif
            tile.managerId = manager.id;
            return tile;
        }

        /// <summary>
        /// Creates a grid of connected Unity Terrains described by the context.
        /// </summary>
        /// <param name="context">The grid description, holding the tile counts, the tile size, the texture density, and the parent.</param>
        /// <returns>The created tiles indexed as grid[column, row].</returns>
        /// <remarks>
        /// Each tile gets a fresh in memory <see cref="TerrainData"/> sized from the context. The parent is the
        /// pivot of the grid. A Unity Terrain pivots at its lower left corner, so the first tile starts at the
        /// parent and the grid extends along positive x and positive z. Adjacent tiles are linked with
        /// <see cref="Terrain.SetNeighbors"/> so their level of detail seams match. This is pure runtime work.
        /// The caller saves the terrain data to assets afterward.
        /// </remarks>
        public GameObject[,] CreateTerrainGrid(TerrainGridCreationContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            int columns = context.tileCount.x;
            int rows = context.tileCount.y;
            float width = context.tileSize.x;
            float height = context.tileSize.y;
            float length = context.tileSize.z;

            GameObject[,] grid = new GameObject[columns, rows];
            for (int column = 0; column < columns; ++column)
            {
                for (int row = 0; row < rows; ++row)
                {
                    TerrainData terrainData = new TerrainData();
                    terrainData.size = new Vector3(width, height, length);
                    terrainData.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

                    GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
                    terrainObject.name = string.Format("Terrain_{0}_{1}", column, row);
                    if (context.parent != null)
                    {
                        terrainObject.transform.SetParent(context.parent, false);
                    }
                    // The parent is the pivot. The first tile sits at the parent origin and the grid grows
                    // along positive x by column and positive z by row.
                    terrainObject.transform.localPosition = new Vector3(column * width, 0f, row * length);

                    grid[column, row] = terrainObject;

                    Terrain terrainComponent = terrainObject.GetComponent<Terrain>();
                    terrainComponent.drawInstanced = true;
                }
            }

            ConnectNeighbors(grid);
            return grid;
        }

        /// <summary>
        /// Links each tile in the grid to its four edge neighbors so their level of detail seams match.
        /// </summary>
        /// <param name="grid">The terrain objects indexed as grid[column, row].</param>
        private static void ConnectNeighbors(GameObject[,] grid)
        {
            int columns = grid.GetLength(0);
            int rows = grid.GetLength(1);
            for (int column = 0; column < columns; column++)
            {
                for (int row = 0; row < rows; row++)
                {
                    Terrain center = GetTerrain(grid, column, row);
                    if (center == null)
                    {
                        continue;
                    }
                    // Columns run along the x axis and rows along the z axis, so left and right step the column
                    // while top and bottom step the row, matching SetNeighbors(left, top, right, bottom).
                    Terrain left = GetTerrain(grid, column - 1, row);
                    Terrain right = GetTerrain(grid, column + 1, row);
                    Terrain top = GetTerrain(grid, column, row + 1);
                    Terrain bottom = GetTerrain(grid, column, row - 1);
                    center.SetNeighbors(left, top, right, bottom);
                }
            }
        }

        /// <summary>
        /// Gets the Unity Terrain at a grid cell, or null when the cell is out of range or empty.
        /// </summary>
        private static Terrain GetTerrain(GameObject[,] grid, int column, int row)
        {
            if (column < 0 || column >= grid.GetLength(0) || row < 0 || row >= grid.GetLength(1))
            {
                return null;
            }
            GameObject terrainObject = grid[column, row];
            return terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
        }

        /// <summary>
        /// Converts a desired texture density into a heightmap resolution Unity Terrain accepts.
        /// </summary>
        /// <param name="texturePixelsPerMeter">Desired pixels per meter.</param>
        /// <param name="width">Tile width in meters.</param>
        /// <param name="length">Tile length in meters.</param>
        /// <returns>A valid Unity heightmap resolution, a power of two plus one between 33 and 4097.</returns>
        private static int ResolveHeightmapResolution(float texturePixelsPerMeter, float width, float length)
        {
            float largestSide = Mathf.Max(width, length);
            int target = Mathf.CeilToInt(texturePixelsPerMeter * largestSide);
            int resolution = Mathf.NextPowerOfTwo(target) + 1;
            resolution = Mathf.Clamp(resolution, 33, 4097);
            return resolution;
        }
    }
}
#endif


