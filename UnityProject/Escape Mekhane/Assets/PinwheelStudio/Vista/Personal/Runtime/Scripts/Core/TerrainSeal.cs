#if VISTA
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Keeps manual terrain edits inside a polygonal region when Vista regenerates the surrounding terrain.
    /// </summary>
    /// <remarks>
    /// Place the Seal around the region you want to edit, keep the work fully inside its hard boundary, and
    /// regenerate normally. Vista preserves supported terrain data automatically; no manual capture step is required.
    /// </remarks>
    [ExecuteInEditMode]
    [AddComponentMenu("Vista/Terrain Seal")]
    public class TerrainSeal : MonoBehaviour, IPolygonalArea
    {
        private static readonly HashSet<TerrainSeal> s_allInstances = new HashSet<TerrainSeal>();

        /// <summary>
        /// Gets all enabled Terrain Seals currently registered in loaded scenes.
        /// </summary>
        public static IEnumerable<TerrainSeal> allInstances
        {
            get
            {
                return s_allInstances;
            }
        }

        [SerializeField]
        protected Vector3[] m_anchors;

        /// <summary>
        /// Gets or sets the local-space anchor points that outline the region protected by this Seal.
        /// </summary>
        /// <remarks>
        /// The getter returns a copy. Modify that copy and assign it back to update the region. Assigning
        /// <see langword="null"/> clears all anchors.
        /// </remarks>
        public Vector3[] anchors
        {
            get
            {
                return LocalAreaUtils.CloneAnchors(m_anchors);
            }
            set
            {
                if (value == null)
                {
                    m_anchors = new Vector3[0];
                }
                else
                {
                    m_anchors = new Vector3[value.Length];
                    value.CopyTo(m_anchors, 0);
                }
            }
        }

        /// <summary>
        /// Gets the world-space axis-aligned bounds enclosing the Seal polygon.
        /// </summary>
        public Bounds worldBounds
        {
            get
            {
                return LocalAreaUtils.CalculateWorldBounds(this);
            }
        }

        /// <summary>
        /// Determines whether the Seal polygon overlaps the horizontal area of the supplied world-space bounds.
        /// </summary>
        /// <param name="area">World-space bounds to test against the Seal.</param>
        /// <returns><see langword="true"/> when the two areas overlap on the XZ plane; otherwise, <see langword="false"/>.</returns>
        public bool IsOverlap(Bounds area)
        {
            return LocalAreaUtils.IsOverlap(this, area);
        }

        private void OnEnable()
        {
            s_allInstances.Add(this);
        }

        private void OnDisable()
        {
            s_allInstances.Remove(this);
        }

        /// <summary>
        /// Resets the protected region to a 1,000-by-1,000-unit local-space square centered on this transform.
        /// </summary>
        public void Reset()
        {
            m_anchors = new Vector3[]
            {
                new Vector3(-500, 0, -500),
                new Vector3(-500, 0, 500),
                new Vector3(500, 0, 500),
                new Vector3(500, 0, -500)
            };
        }
    }
}
#endif
