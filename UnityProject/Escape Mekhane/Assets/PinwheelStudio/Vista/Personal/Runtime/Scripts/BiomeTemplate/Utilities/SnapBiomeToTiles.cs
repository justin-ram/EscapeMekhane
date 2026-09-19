#if VISTA
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Snaps its <see cref="LocalProceduralBiome"/> so the biome polygon covers every terrain tile under the
    /// Vista Manager. Dropped on a biome template's prefab, it fits the spawned biome to whatever terrain the
    /// scene has, so the template covers the full terrain the moment it is created, at any terrain size.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IBiomeTemplateSpawnCallbackReceiver"/>, so it runs automatically right after the
    /// biome is spawned from a template (the spawn broadcasts to every receiver in the clone, using the Manager
    /// carried on the context). It also exposes a context menu re-fit for manual use. Pure runtime, no editor
    /// dependency, so it also works when a biome is spawned from a template at run time.
    /// </remarks>
    [RequireComponent(typeof(LocalProceduralBiome))]
    [AddComponentMenu("Vista/Biome Template/Snap Biome To Tiles On Spawn")]
    public class SnapBiomeToTilesOnSpawn : MonoBehaviour, IBiomeTemplateSpawnCallbackReceiver
    {
        /// <summary>Fits the biome to the spawning Manager's tiles as part of the template spawn.</summary>
        public void OnSpawnedFromBiomeTemplate(BiomeTemplate template, BiomeTemplateSpawnContext context)
        {
            // Both arguments are contract required, never null (see IBiomeTemplateSpawnCallbackReceiver). The
            // manager on the context may still be null, which SnapToTiles handles by leaving the polygon alone.
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }
            SnapToTiles(context.manager);
        }

        /// <summary>Re-fit the biome to the tiles under its Vista Manager, resolved from the hierarchy.</summary>
        [ContextMenu("Snap To Tiles")]
        public void SnapToTiles()
        {
            SnapToTiles(GetComponentInParent<VistaManager>());
        }

        /// <summary>
        /// Set the biome's anchor polygon to the combined XZ bounds of every tile under <paramref name="manager"/>.
        /// Does nothing when there is no manager, no biome, or no tiles, so the biome keeps its authored polygon.
        /// </summary>
        public void SnapToTiles(VistaManager manager)
        {
            if (manager == null)
            {
                return;
            }

            LocalProceduralBiome biome = GetComponent<LocalProceduralBiome>();
            if (biome == null)
            {
                return;
            }

            List<ITile> tiles = manager.GetTiles();
            if (tiles == null || tiles.Count == 0)
            {
                return;
            }

            Bounds combined = tiles[0].worldBounds;
            for (int i = 1; i < tiles.Count; i++)
            {
                combined.Encapsulate(tiles[i].worldBounds);
            }

            // Build a rectangle over the combined XZ extent, in the same winding LocalProceduralBiome.Reset uses,
            // then express it in the biome's local space, anchors are local and the polygon is transformed by the
            // biome transform. Corners sit at the biome's own Y, so with the usual identity spawn transform the
            // local anchors stay flat on the Y = 0 plane.
            float y = transform.position.y;
            Vector3[] worldCorners =
            {
                new Vector3(combined.min.x, y, combined.min.z),
                new Vector3(combined.min.x, y, combined.max.z),
                new Vector3(combined.max.x, y, combined.max.z),
                new Vector3(combined.max.x, y, combined.min.z),
            };

            Vector3[] localCorners = new Vector3[worldCorners.Length];
            for (int i = 0; i < worldCorners.Length; i++)
            {
                localCorners[i] = transform.InverseTransformPoint(worldCorners[i]);
            }

            biome.anchors = localCorners;
        }
    }
}
#endif
