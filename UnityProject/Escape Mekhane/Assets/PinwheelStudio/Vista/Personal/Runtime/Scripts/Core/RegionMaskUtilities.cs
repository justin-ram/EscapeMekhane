#if VISTA
using Pinwheel.Vista.Graphics;
using System;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Provides focused operations for remapping world-space region masks into another bounds space.
    /// </summary>
    public static class RegionMaskUtilities
    {
        private static readonly string BLIT_SHADER_NAME = "Hidden/Vista/BiomeDataBlit";
        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int RENDER_TARGET_SIZE = Shader.PropertyToID("_RenderTargetSize");
        private static readonly int SRC_BOUNDS = Shader.PropertyToID("_SrcBounds");
        private static readonly int DEST_BOUNDS = Shader.PropertyToID("_DestBounds");
        private static readonly int PASS = 0;

        /// <summary>
        /// Extracts the part of a source mask represented by target world bounds into a new square render texture.
        /// </summary>
        /// <param name="sourceMask">Mask representing <paramref name="sourceWorldBounds"/> in world XZ space.</param>
        /// <param name="sourceWorldBounds">World-space bounds represented by the source mask.</param>
        /// <param name="targetWorldBounds">World-space bounds that the returned mask should represent.</param>
        /// <param name="targetResolution">Width and height of the returned mask.</param>
        /// <returns>
        /// A created, linear RFloat render texture owned by the caller. Pixels outside the source bounds are zero.
        /// </returns>
        public static RenderTexture ExtractRegionMask(
            Texture sourceMask,
            Bounds sourceWorldBounds,
            Bounds targetWorldBounds,
            int targetResolution)
        {
            if (sourceMask == null)
            {
                throw new ArgumentNullException(nameof(sourceMask));
            }
            if (sourceMask.width <= 0 || sourceMask.height <= 0)
            {
                throw new ArgumentException("Source mask dimensions must be greater than zero.", nameof(sourceMask));
            }
            ValidateBounds(sourceWorldBounds, nameof(sourceWorldBounds));
            ValidateBounds(targetWorldBounds, nameof(targetWorldBounds));
            if (targetResolution <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(targetResolution), "Target resolution must be greater than zero.");
            }

            RenderTexture targetMask = new RenderTexture(
                targetResolution,
                targetResolution,
                0,
                RenderTextureFormat.RFloat,
                RenderTextureReadWrite.Linear);
            targetMask.name = $"Region Mask {targetResolution}x{targetResolution}";
            targetMask.filterMode = FilterMode.Bilinear;
            targetMask.wrapMode = TextureWrapMode.Clamp;
            targetMask.Create();
            GraphicsUtils.ClearWithZeros(targetMask);

            Material material = null;
            try
            {
                material = new Material(ShaderUtilities.Find(BLIT_SHADER_NAME));
                material.SetTexture(MAIN_TEX, sourceMask);
                material.SetVector(RENDER_TARGET_SIZE, new Vector4(targetResolution, targetResolution, 0, 0));
                material.SetVector(SRC_BOUNDS, ToXZRect(sourceWorldBounds));
                material.SetVector(DEST_BOUNDS, ToXZRect(targetWorldBounds));
                Drawing.DrawQuad(targetMask, material, PASS);
                return targetMask;
            }
            catch
            {
                targetMask.Release();
                UnityEngine.Object.DestroyImmediate(targetMask);
                throw;
            }
            finally
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material);
                }
            }
        }

        private static Vector4 ToXZRect(Bounds bounds)
        {
            return new Vector4(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
        }

        private static void ValidateBounds(Bounds bounds, string parameterName)
        {
            if (bounds.size.x <= 0 || bounds.size.z <= 0)
            {
                throw new ArgumentException("Bounds size must be greater than zero on the X and Z axes.", parameterName);
            }
        }
    }
}
#endif
