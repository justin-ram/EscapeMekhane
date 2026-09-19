#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Defines the runtime contract for a biome that can contribute data to Vista tiles.
    /// </summary>
    /// <remarks>
    /// A biome supplies data on demand for world-space tile bounds, reports overlap for culling, and participates in the
    /// manager lifecycle before and after a generation pass.
    /// </remarks>
    public interface IBiome
    {
        /// <summary>
        /// Gets the GameObject that owns this biome component.
        /// </summary>
        GameObject gameObject { get; }
        /// <summary>
        /// Gets or sets the biome sort order used when multiple biomes overlap the same tile.
        /// </summary>
        int order { get; set; }
        /// <summary>
        /// Gets or sets the biome change counter used for regeneration tracking.
        /// </summary>
        [System.Obsolete("Biome update counters are no longer used for generation invalidation.")]
        long updateCounter { get; set; }
        /// <summary>
        /// Gets the blend settings used when this biome is combined with overlapping biomes.
        /// </summary>
        BiomeBlendOptions blendOptions { get; }
        /// <summary>
        /// Requests biome output data for the specified world bounds and resolutions.
        /// </summary>
        /// <param name="worldBounds">World-space bounds to generate data for.</param>
        /// <param name="heightMapResolution">Requested height-related output resolution.</param>
        /// <param name="textureResolution">Requested texture-related output resolution.</param>
        /// <param name="consumableDataMask">
        /// Channels the caller is able to consume, intersected with the biome's own data mask so an output is generated only
        /// when the caller can use it and the biome produces it. Defaults to all channels, so a caller that consumes
        /// everything can ignore it.
        /// </param>
        /// <param name="cancellationSignal">
        /// Optional read-only cancellation state owned by the caller. Implementations should pass it to cancellable child
        /// operations, stop at safe checkpoints, release partial output, and complete the returned request before exiting.
        /// A <see langword="null"/> signal means the caller did not request cooperative cancellation.
        /// </param>
        /// <returns>
        /// A request whose <see cref="BiomeDataRequest.data"/> payload contains the requested outputs on success. The request
        /// must reach completion after either successful generation or cancellation cleanup so its caller can safely resume.
        /// </returns>
        BiomeDataRequest RequestData(Bounds worldBounds, int heightMapResolution, int textureResolution, BiomeDataMask consumableDataMask = (BiomeDataMask)(~0), CancellationSignal cancellationSignal = null);
        /// <summary>
        /// Tests whether this biome overlaps the specified world-space area.
        /// </summary>
        /// <param name="area">Area bounds to test.</param>
        /// <returns><see langword="true"/> if overlap exists; otherwise <see langword="false"/>.</returns>
        bool IsOverlap(Bounds area);
        /// <summary>
        /// Called by <see cref="VistaManager"/> before a generation pass starts.
        /// </summary>
        void OnBeforeVMGenerate();
        /// <summary>
        /// Called by <see cref="VistaManager"/> after a generation pass completes successfully.
        /// </summary>
        /// <remarks>This callback is not invoked for a cancelled Manager generation.</remarks>
        void OnAfterVMGenerate();

    }
}
#endif


