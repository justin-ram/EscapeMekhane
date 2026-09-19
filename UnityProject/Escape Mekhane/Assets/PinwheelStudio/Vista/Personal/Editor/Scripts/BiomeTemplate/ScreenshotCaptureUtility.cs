#if VISTA
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor
{
    /// <summary>
    /// Editor utility for grabbing a screenshot of the current Scene View content and saving it as a JPG asset.
    /// It works like a screen snip: it reads the already composited pixels of the Scene View region off the
    /// screen (via <see cref="EditorScreenshot"/>, the same path the graph viewports use), so it is render
    /// pipeline agnostic and captures exactly what is on screen, gizmos and overlays included only when the user
    /// has them enabled. It never changes those settings, the author frames the shot by navigating and toggling
    /// them beforehand. The capture is returned at the Scene View's own resolution, unscaled.
    /// </summary>
    public static class ScreenshotCaptureUtility
    {
        /// <summary>
        /// Snip the active Scene View's rendered surface off the screen and return it at that surface's own
        /// resolution, or null when there is no active Scene View (or its camera rect cannot be resolved). The
        /// returned texture is readable and owned by the caller.
        /// </summary>
        public static Texture2D CaptureSceneView()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return null;
            }

            // The actual rendered scene surface, excluding the tab and toolbar band, is the "camera rect"
            // element inside the Scene View. Its screen rect is that window local bound offset by the window's
            // screen position, the same way the graph viewport snips itself.
            VisualElement cameraRect = sceneView.rootVisualElement.Q("unity-scene-view-camera-rect");
            if (cameraRect == null)
            {
                return null;
            }

            Rect surfaceBound = cameraRect.worldBound;
            Vector2 screenPosition = surfaceBound.position + sceneView.position.position;
            int width = (int)surfaceBound.width;
            int height = (int)surfaceBound.height;
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            return EditorScreenshot.Capture(screenPosition, width, height);
        }

        /// <summary>
        /// Encode <paramref name="texture"/> as a JPG, write it to <paramref name="assetPath"/> (a project
        /// relative path ending in .jpg), import it, and return the imported <see cref="Texture2D"/>. Overwrites
        /// an existing file at that path in place, so a re-capture keeps the same asset and any references to it.
        /// </summary>
        public static Texture2D SaveAsJpg(Texture2D texture, string assetPath, int quality = 90)
        {
            byte[] jpg = texture.EncodeToJPG(quality);
            File.WriteAllBytes(assetPath, jpg);
            AssetDatabase.ImportAsset(assetPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }
    }
}
#endif
