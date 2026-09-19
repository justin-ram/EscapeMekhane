#if VISTA
using Pinwheel.Vista.Graph;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Defines an optional tile capability for capturing persisted state and reconciling snapshot identity.
    /// </summary>
    /// <remarks>
    /// Snapshot data uses the regular <see cref="BiomeData"/> representation so it can participate in the
    /// same blending and population pipeline as generated biome data. The caller owns the returned data and
    /// must dispose it when the tile population pass finishes. A tile capture is generation-scoped; callers
    /// must finish using one snapshot before starting another capture on the same tile.
    /// </remarks>
    public interface ITileSnapshotProvider : ITile
    {
        /// <summary>
        /// Captures every persisted channel supported by this tile backend.
        /// </summary>
        /// <param name="transientResources">Registry that owns temporary templates created by the capture.</param>
        /// <param name="cancellationSignal">Optional signal used to stop the progressive capture between work bands.</param>
        /// <returns>A progressive request containing a caller-owned snapshot in Vista's standard biome-data format.</returns>
        BiomeDataRequest CaptureSnapshot(
            TransientResourceRegistry transientResources,
            CancellationSignal cancellationSignal = null);

        /// <summary>
        /// Tests whether two terrain layers represent the same native layer for this backend.
        /// </summary>
        bool IsEquivalent(TerrainLayer first, TerrainLayer second);

        /// <summary>
        /// Tests whether two tree templates represent the same native tree prototype for this backend.
        /// </summary>
        bool IsEquivalent(TreeTemplate first, TreeTemplate second);

        /// <summary>
        /// Tests whether two detail templates represent the same native detail prototype for this backend.
        /// Density is not part of prototype identity.
        /// </summary>
        bool IsEquivalent(DetailTemplate first, DetailTemplate second);
    }
} 
#endif
