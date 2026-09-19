#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Defines a tile capability for drawing one persisted terrain-layer weight into a world-space canvas.
    /// </summary>
    public interface ISceneTextureWeightProvider
    {
        /// <summary>
        /// Draws this tile's weight for the requested terrain layer into a destination representing a world-space rectangle.
        /// Tiles that do not contain an equivalent layer leave the destination unchanged.
        /// </summary>
        void OnCollectSceneTextureWeight(TerrainLayer terrainLayer, RenderTexture targetRt, Rect requestedWorldRect);
    }
}
#endif
