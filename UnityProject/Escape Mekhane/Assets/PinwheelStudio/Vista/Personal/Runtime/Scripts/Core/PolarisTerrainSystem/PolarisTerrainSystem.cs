#if VISTA
#if GRIFFIN
using UnityEngine;
using System;
using Pinwheel.Griffin;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pinwheel.Vista.PolarisTerrain
{
    /// <summary>
    /// Registers and configures Vista support for Polaris terrains.
    /// </summary>
    /// <remarks>
    /// This terrain-system adapter exposes the Polaris terrain and tile component types to Vista and ensures a compatible
    /// <see cref="PolarisTile"/> is attached to supported targets when a manager sets up terrain integration.
    /// </remarks>
    public class PolarisTerrainSystem : ITerrainSystem
    {
        /// <summary>
        /// Gets the display name used for this terrain-system integration.
        /// </summary>
        public string terrainLabel
        {
            get
            {
                return "Polaris Terrain";
            }
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
#else
        [RuntimeInitializeOnLoadMethod]
#endif
        private static void OnInitialize()
        {
            VistaManager.RegisterTerrainSystem<PolarisTerrainSystem>();
        }

        /// <summary>
        /// Gets the Polaris terrain component type recognized by this integration.
        /// </summary>
        /// <returns><see cref="GStylizedTerrain"/>.</returns>
        public Type GetTerrainComponentType()
        {
            return typeof(GStylizedTerrain);
        }

        /// <summary>
        /// Gets the Vista tile component type used for Polaris terrains.
        /// </summary>
        /// <returns><see cref="PolarisTile"/>.</returns>
        public Type GetTileComponentType()
        {
            return typeof(PolarisTile);
        }

        /// <summary>
        /// Ensures a compatible Polaris tile component is attached to a target terrain object and bound to a manager.
        /// </summary>
        /// <param name="manager">The manager that should own the configured tile.</param>
        /// <param name="target">The GameObject expected to contain a <see cref="GStylizedTerrain"/> component.</param>
        /// <returns>
        /// The configured <see cref="PolarisTile"/> when the target contains a Polaris terrain; otherwise,
        /// <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// If the target does not already have a <see cref="PolarisTile"/>, one is added. In the Unity Editor the component
        /// add and manager-ID assignment are routed through Undo so the setup operation integrates with editor history.
        /// </remarks>
        public ITile SetupTile(VistaManager manager, GameObject target)
        {
            GStylizedTerrain terrainComponent = target.GetComponent<GStylizedTerrain>();
            if (terrainComponent == null)
            {
                return null;
            }
            PolarisTile tile = target.GetComponent<PolarisTile>();
            if (tile == null)
            {
#if UNITY_EDITOR
                tile = Undo.AddComponent<PolarisTile>(target);
#else
                tile = target.AddComponent<PolarisTile>();
#endif
            }
#if UNITY_EDITOR
            Undo.RecordObject(tile, "Modify Polaris Tile");
#endif
            tile.managerId = manager.id;
            return tile;
        }

        /// <summary>
        /// Creates a grid of connected Polaris terrains described by the context.
        /// </summary>
        /// <param name="context">The grid description, holding the tile counts, the tile size, the texture density, and the parent.</param>
        /// <returns>The created tiles indexed as grid[column, row].</returns>
        /// <remarks>
        /// Each tile gets a fresh in memory <see cref="GTerrainData"/> sized from the context and one cloned material,
        /// the way Polaris creates terrains. The parent is the pivot, so the first tile starts at the parent and the
        /// grid extends along positive x and positive z. Tiles share a group id no current terrain uses, then the
        /// static <see cref="GStylizedTerrain.ConnectAdjacentTiles"/> stitches their edges by world position. This is
        /// pure runtime work that uses the long standing Griffin types, so it stays compatible with older Polaris
        /// versions. The caller saves the terrain data and material to assets afterward.
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

            // A group id no current terrain uses, so this grid stitches to itself and not to unrelated terrains.
            int groupId = GetUnusedGroupId();

            GameObject[,] grid = new GameObject[columns, rows];
            for (int column = 0; column < columns; ++column)
            {
                for (int row = 0; row < rows; ++row)
                {
                    GTerrainData data = CreateTerrainData(width, height, length);

                    GameObject terrainObject = new GameObject(string.Format("Terrain_{0}_{1}", column, row));
                    if (context.parent != null)
                    {
                        terrainObject.transform.SetParent(context.parent, false);
                    }
                    // The parent is the pivot. The first tile sits at the parent origin and the grid grows along
                    // positive x by column and positive z by row.
                    terrainObject.transform.localPosition = new Vector3(column * width, 0f, row * length);

                    GStylizedTerrain terrain = terrainObject.AddComponent<GStylizedTerrain>();
                    terrain.GroupId = groupId;
                    terrain.TerrainData = data;

                    grid[column, row] = terrainObject;
                }
            }

            // Polaris stitches edges between terrains that share a group id and sit adjacent in world space.
            GStylizedTerrain.ConnectAdjacentTiles();
            return grid;
        }

        // A fresh terrain data sized to the tile, with one cloned material, mirroring how Polaris's own terrain
        // creation sets up a data object. Uses only the long standing Griffin types for broad version support.
        private static GTerrainData CreateTerrainData(float width, float height, float length)
        {
            GTerrainData data = ScriptableObject.CreateInstance<GTerrainData>();

            // In edit mode the data initializes through OnEnable. In play mode the sub models must be reset
            // explicitly, the same way Polaris's terrain wizard does it.
            if (Application.isPlaying)
            {
                data.Reset();
                data.Geometry.Reset();
                data.Shading.Reset();
                data.Rendering.Reset();
                data.Foliage.Reset();
                data.Mask.Reset();
            }

            data.Geometry.Width = width;
            data.Geometry.Height = height;
            data.Geometry.Length = length;

            //Self created here to avoid the Polaris lazy path, which read GTerrainData file location.
            //At this step TerrainData was not saved yet
            data.GeometryData = ScriptableObject.CreateInstance<GTerrainGeneratedData>();

            // One material per tile, matching Polaris. No sharing or other optimization at this step.
            Material material = GRuntimeSettings.Instance.terrainRendering.GetClonedMaterial(
                GCommon.CurrentRenderPipeline,
                GLightingModel.PBR,
                GTexturingModel.Splat,
                GSplatsModel.Splats4);
            if (material != null)
            {
                data.Shading.CustomMaterial = material;
                data.Shading.UpdateMaterials();
            }

            return data;
        }

        // A group id greater than every active terrain's, so a new grid connects only to its own tiles.
        private static int GetUnusedGroupId()
        {
            int maxGroupId = -1;
            foreach (GStylizedTerrain terrain in GStylizedTerrain.ActiveTerrains)
            {
                if (terrain.GroupId > maxGroupId)
                {
                    maxGroupId = terrain.GroupId;
                }
            }
            return maxGroupId + 1;
        }
    }
}
#endif
#endif


