#if VISTA
using UnityEngine;

namespace Pinwheel.VistaEditor.Preview
{
    /// <summary>
    /// Draws a texture as a world-space mask overlay on terrains owned by one backend.
    /// </summary>
    public interface ITilePreviewRenderer
    {
        /// <summary>
        /// Draws the mask on every terrain owned by this backend that overlaps the mask region.
        /// </summary>
        /// <param name="camera">Camera that renders the overlay.</param>
        /// <param name="maskBoundsWS">World-space XZ bounds represented by the complete mask texture.</param>
        /// <param name="mask">Mask texture to visualize.</param>
        /// <param name="color">Display color multiplied by the mask.</param>
        /// <param name="animated">Whether to animate the backend's processing treatment.</param>
        void Draw(Camera camera, Bounds maskBoundsWS, Texture mask, Color color, bool animated = false);
    }
}
#endif
