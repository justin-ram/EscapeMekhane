#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Defines an authored polygonal area without prescribing boundary falloff behavior.
    /// </summary>
    public interface IPolygonalArea
    {
        GameObject gameObject { get; }
        Transform transform { get; }
        Vector3[] anchors { get; set; }
        Bounds worldBounds { get; }

        bool IsOverlap(Bounds area);
    }

    /// <summary>
    /// Extends a polygonal area with an authored falloff region.
    /// </summary>
    public interface IPolygonalAreaWithFalloff : IPolygonalArea
    {
        FalloffDirection falloffDirection { get; set; }
        float falloffDistance { get; set; }
        Vector3[] falloffAnchors { get; }
    }
}
#endif
