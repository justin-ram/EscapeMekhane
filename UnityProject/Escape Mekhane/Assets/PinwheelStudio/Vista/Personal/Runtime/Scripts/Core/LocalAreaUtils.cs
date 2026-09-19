#if VISTA
using Pinwheel.Vista.Geometric;
using Pinwheel.Vista.Graphics;
using System;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Provides shared operations for <see cref="IPolygonalArea"/> implementations without owning their serialized data.
    /// </summary>
    /// <remarks>
    /// Implementers declare their own fields and properties. This helper remains stateless so adopting it never requires
    /// changing a component's base class or serialized field ownership.
    /// </remarks>
    public static class LocalAreaUtils
    {
        public static Vector3[] CloneAnchors(Vector3[] source)
        {
            Vector3[] clone = new Vector3[source.Length];
            source.CopyTo(clone, 0);
            return clone;
        }

        public static Vector3[] CalculateFalloffAnchors(
            Vector3[] anchors,
            float falloffDistance,
            FalloffDirection falloffDirection)
        {
            if (anchors == null)
            {
                return null;
            }
            return AnchorUtilities.GetFalloff(anchors, falloffDistance, falloffDirection);
        }

        public static Bounds CalculateWorldBounds(IPolygonalArea area)
        {
            IPolygonalAreaWithFalloff areaWithFalloff = area as IPolygonalAreaWithFalloff;
            FalloffDirection falloffDirection = areaWithFalloff != null
                ? areaWithFalloff.falloffDirection
                : FalloffDirection.Inner;
            return CalculateWorldBounds(
                area.transform,
                area.anchors,
                falloffDirection == FalloffDirection.Outer ? areaWithFalloff.falloffAnchors : null,
                falloffDirection);
        }

        public static Bounds CalculateWorldBounds(
            Transform transform,
            Vector3[] anchors,
            Vector3[] falloffAnchors,
            FalloffDirection falloffDirection)
        {
            Vector3[] outerAnchors = falloffDirection == FalloffDirection.Outer
                ? falloffAnchors
                : anchors;
            if (outerAnchors == null || outerAnchors.Length == 0)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;
            foreach (Vector3 anchor in outerAnchors)
            {
                Vector3 worldPosition = transform.TransformPoint(anchor);
                minX = Mathf.Min(minX, worldPosition.x);
                minY = Mathf.Min(minY, worldPosition.y);
                minZ = Mathf.Min(minZ, worldPosition.z);
                maxX = Mathf.Max(maxX, worldPosition.x);
                maxY = Mathf.Max(maxY, worldPosition.y);
                maxZ = Mathf.Max(maxZ, worldPosition.z);
            }

            Vector3 center = new Vector3(minX + maxX, minY + maxY, minZ + maxZ) * 0.5f;
            Vector3 size = new Vector3(maxX - minX, maxY - minY, maxZ - minZ);
            return new Bounds(center, size);
        }

        public static bool IsOverlap(IPolygonalArea polygonalArea, Bounds area)
        {
            IPolygonalAreaWithFalloff areaWithFalloff = polygonalArea as IPolygonalAreaWithFalloff;
            Vector3[] outerAnchors = areaWithFalloff != null && areaWithFalloff.falloffDirection == FalloffDirection.Outer
                ? areaWithFalloff.falloffAnchors
                : polygonalArea.anchors;
            Vector2[] localAreaVertices = new Vector2[outerAnchors.Length];
            for (int i = 0; i < localAreaVertices.Length; ++i)
            {
                localAreaVertices[i] = polygonalArea.transform.TransformPoint(outerAnchors[i]).XZ();
            }
            Polygon2D localAreaPolygon = new Polygon2D(localAreaVertices);

            Vector2[] areaVertices = new Vector2[4];
            areaVertices[0] = new Vector2(area.min.x, area.min.z);
            areaVertices[1] = new Vector2(area.min.x, area.max.z);
            areaVertices[2] = new Vector2(area.max.x, area.max.z);
            areaVertices[3] = new Vector2(area.max.x, area.min.z);
            Polygon2D areaPolygon = new Polygon2D(areaVertices);

            return Polygon2D.IsOverlap(localAreaPolygon, areaPolygon);
        }

        /// <summary>
        /// Allocates a render texture suitable for a polygonal local-area mask.
        /// </summary>
        /// <param name="resolution">Width and height of the render texture.</param>
        /// <returns>A created, linear RFloat render texture owned by the caller.</returns>
        public static RenderTexture AllocatePolygonalMaskRT(int resolution)
        {
            if (resolution <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(resolution), "Mask resolution must be greater than zero.");
            }

            RenderTexture mask = GraphicsUtils.CreateBlankRT(resolution, RenderTextureFormat.RFloat);
            mask.name = "Polygonal Mask";
            return mask;
        }

        /// <summary>
        /// Renders a polygonal area into its own world-bounds texture space, including falloff when supported.
        /// </summary>
        /// <param name="area">Area whose polygon and world bounds define the mask.</param>
        /// <param name="targetRt">Caller-owned render texture that receives the mask.</param>
        public static void RenderPolygonalMask(IPolygonalArea area, RenderTexture targetRt)
        {
            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }
            RenderPolygonalMask(area, area.worldBounds, targetRt);
        }

        /// <summary>
        /// Renders a polygonal area directly into a supplied world-bounds texture space, including falloff when supported.
        /// </summary>
        public static void RenderPolygonalMask(IPolygonalArea area, Bounds targetWorldBounds, RenderTexture targetRt)
        {
            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }
            if (targetRt == null)
            {
                throw new ArgumentNullException(nameof(targetRt));
            }
            if (!targetRt.IsCreated())
            {
                throw new ArgumentException("Target render texture must be created before rendering.", nameof(targetRt));
            }

            Vector3[] anchors = area.anchors;
            if (anchors == null || anchors.Length < 3)
            {
                throw new ArgumentException("A polygonal area mask requires at least three anchors.", nameof(area));
            }
            if (targetWorldBounds.size.x <= 0 || targetWorldBounds.size.z <= 0)
            {
                throw new ArgumentException("Target world bounds must have a non-zero size on the X and Z axes.", nameof(targetWorldBounds));
            }

            IPolygonalAreaWithFalloff areaWithFalloff = area as IPolygonalAreaWithFalloff;
            Vector3[] falloffAnchors = areaWithFalloff != null
                ? areaWithFalloff.falloffAnchors
                : anchors;
            if (falloffAnchors == null || falloffAnchors.Length != anchors.Length)
            {
                throw new ArgumentException("Falloff anchors must correspond one-to-one with the area anchors.", nameof(area));
            }

            PolygonMaskRenderer.Configs configs = new PolygonMaskRenderer.Configs();
            configs.vertices = NormalizeAnchors(area.transform, anchors, targetWorldBounds);
            configs.falloffVertices = NormalizeAnchors(area.transform, falloffAnchors, targetWorldBounds);
            configs.falloffTexture = null;
            configs.falloffDirection = areaWithFalloff != null
                ? areaWithFalloff.falloffDirection
                : FalloffDirection.Inner;
            PolygonMaskRenderer.Render(targetRt, configs);
        }

        private static Vector2[] NormalizeAnchors(Transform transform, Vector3[] anchors, Bounds worldBounds)
        {
            Vector2[] normalizedAnchors = new Vector2[anchors.Length];
            for (int i = 0; i < anchors.Length; ++i)
            {
                Vector3 worldPoint = transform.TransformPoint(anchors[i]);
                normalizedAnchors[i] = new Vector2(
                    (worldPoint.x - worldBounds.min.x) / worldBounds.size.x,
                    (worldPoint.z - worldBounds.min.z) / worldBounds.size.z);
            }
            return normalizedAnchors;
        }

    }
}
#endif
