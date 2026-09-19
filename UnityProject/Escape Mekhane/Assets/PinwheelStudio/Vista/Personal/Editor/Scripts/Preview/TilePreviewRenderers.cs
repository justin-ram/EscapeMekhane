#if VISTA
using System;
using System.Collections.Generic;
using Pinwheel.Vista;
using UnityEngine;

namespace Pinwheel.VistaEditor.Preview
{
    /// <summary>
    /// Collects terrain-backend preview renderers and forwards preview requests to them.
    /// </summary>
    public static class TilePreviewRenderers
    {
        private static readonly Dictionary<Type, ITilePreviewRenderer> s_renderers =
            new Dictionary<Type, ITilePreviewRenderer>();

        /// <summary>
        /// Registers the preview renderer responsible for a terrain tile type.
        /// </summary>
        /// <typeparam name="TTile">Concrete tile type handled by the renderer.</typeparam>
        /// <param name="renderer">Renderer to associate with the tile type.</param>
        public static void Register<TTile>(ITilePreviewRenderer renderer) where TTile : ITile
        {
            Register(typeof(TTile), renderer);
        }

        /// <summary>
        /// Registers the preview renderer responsible for a terrain tile type.
        /// </summary>
        /// <param name="tileType">Concrete tile type handled by the renderer.</param>
        /// <param name="renderer">Renderer to associate with the tile type.</param>
        public static void Register(Type tileType, ITilePreviewRenderer renderer)
        {
            ValidateTileType(tileType);
            if (renderer == null)
                throw new ArgumentNullException(nameof(renderer));
            if (s_renderers.ContainsKey(tileType))
                throw new InvalidOperationException($"A tile preview renderer is already registered for '{tileType.FullName}'.");

            s_renderers.Add(tileType, renderer);
        }

        /// <summary>
        /// Removes the preview renderer registered for a terrain tile type.
        /// </summary>
        /// <typeparam name="TTile">Tile type whose renderer should be removed.</typeparam>
        /// <param name="renderer">Renderer expected to own the current registration.</param>
        /// <returns>True when that renderer owned and removed the registration.</returns>
        public static bool Unregister<TTile>(ITilePreviewRenderer renderer) where TTile : ITile
        {
            return Unregister(typeof(TTile), renderer);
        }

        /// <summary>
        /// Removes the preview renderer registered for a terrain tile type.
        /// </summary>
        /// <param name="tileType">Tile type whose renderer should be removed.</param>
        /// <param name="renderer">Renderer expected to own the current registration.</param>
        /// <returns>True when that renderer owned and removed the registration.</returns>
        public static bool Unregister(Type tileType, ITilePreviewRenderer renderer)
        {
            ValidateTileType(tileType);
            if (!s_renderers.TryGetValue(tileType, out ITilePreviewRenderer registeredRenderer) ||
                !ReferenceEquals(registeredRenderer, renderer))
                return false;

            s_renderers.Remove(tileType);
            return true;
        }

        /// <summary>
        /// Asks every registered terrain backend to draw the mask over its terrains in the mask region.
        /// </summary>
        /// <param name="camera">Camera that renders the overlay.</param>
        /// <param name="maskBoundsWS">World-space XZ bounds represented by the complete mask texture.</param>
        /// <param name="mask">Mask texture to visualize.</param>
        /// <param name="color">Display color multiplied by the mask.</param>
        /// <param name="animated">Whether renderers should animate their processing treatment.</param>
        public static void Draw(Camera camera, Bounds maskBoundsWS, Texture mask, Color color, bool animated = false)
        {
            if (camera == null)
            {
                Debug.LogError("Cannot draw tile previews without a camera.");
                return;
            }
            if (mask == null)
            {
                Debug.LogError("Cannot draw tile previews without a mask.");
                return;
            }

            foreach (ITilePreviewRenderer renderer in s_renderers.Values)
            {
                try
                {
                    renderer.Draw(camera, maskBoundsWS, mask, color, animated);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }

        private static void ValidateTileType(Type tileType)
        {
            if (tileType == null)
                throw new ArgumentNullException(nameof(tileType));
            if (!typeof(ITile).IsAssignableFrom(tileType))
                throw new ArgumentException($"Tile type '{tileType.FullName}' must implement {nameof(ITile)}.", nameof(tileType));
        }
    }
}
#endif
