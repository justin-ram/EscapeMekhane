#if VISTA
namespace Pinwheel.Vista
{
    /// <summary>
    /// Contains the resolved settings required to blend one <see cref="BiomeData"/> source.
    /// </summary>
    /// <remarks>
    /// Scene objects and biome ownership are deliberately excluded. Callers resolve any scene transform into the
    /// normalized height offset and scale before invoking the blend pipeline.
    /// </remarks>
    internal struct BiomeBlendConfig
    {
        /// <summary>Gets or sets the diagnostic label for this source.</summary>
        public string label { get; set; }
        /// <summary>Gets or sets the channel blend operators for this source.</summary>
        public BiomeBlendOptions blendOptions { get; set; }
        /// <summary>Gets or sets the already-normalized height offset applied to this source.</summary>
        public float heightOffset { get; set; }
        /// <summary>Gets or sets the height scale applied to this source.</summary>
        public float heightScale { get; set; }

        /// <summary>
        /// Creates resolved blend settings for one data source.
        /// </summary>
        public BiomeBlendConfig(
            string label,
            BiomeBlendOptions blendOptions,
            float heightOffset = 0,
            float heightScale = 1)
        {
            this.label = label;
            this.blendOptions = blendOptions;
            this.heightOffset = heightOffset;
            this.heightScale = heightScale;
        }
    }
}
#endif
