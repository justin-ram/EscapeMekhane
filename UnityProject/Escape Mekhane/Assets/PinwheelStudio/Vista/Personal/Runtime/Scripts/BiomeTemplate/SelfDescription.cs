#if VISTA
using System;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Human-readable self description for a Vista asset: a title, a short description, a longer usage guide,
    /// and a preview screenshot. Deliberately a single shared, serializable type so the same shape powers both
    /// a graph's self description and a <see cref="BiomeTemplate"/>'s gallery entry (one definition, one
    /// drawer). See the 3.2 template-gallery / graph-system design.
    /// </summary>
    [Serializable]
    public class SelfDescription
    {
        [SerializeField]
        private string m_title;
        /// <summary>A short, human-readable title shown in lists and headers.</summary>
        public string title
        {
            get { return m_title; }
            set { m_title = value; }
        }

        [SerializeField]
        [TextArea(2, 4)]
        private string m_description;
        /// <summary>A one or two line summary of what this produces.</summary>
        public string description
        {
            get { return m_description; }
            set { m_description = value; }
        }

        [SerializeField]
        [TextArea(3, 8)]
        private string m_usageGuide;
        /// <summary>A longer note on how to use it and what to tweak first.</summary>
        public string usageGuide
        {
            get { return m_usageGuide; }
            set { m_usageGuide = value; }
        }

        [SerializeField]
        private Texture2D m_screenshot;
        /// <summary>A preview image of the result, used as a thumbnail and a larger preview.</summary>
        public Texture2D screenshot
        {
            get { return m_screenshot; }
            set { m_screenshot = value; }
        }

        [SerializeField]
        private string m_documentationUrl;
        /// <summary>
        /// Optional link to a full online guide for this template. The inline usage guide stays the primary
        /// source, this is the supplementary go deeper affordance. Empty means no link is shown.
        /// </summary>
        public string documentationUrl
        {
            get { return m_documentationUrl; }
            set { m_documentationUrl = value; }
        }

        /// <summary>
        /// True when every field is empty, so a caller can fall back to another source (e.g. a graph's own
        /// description). The documentation link is deliberately not counted, a bare URL with no title, summary,
        /// guide, or screenshot is not a usable description and should still trigger the fallback.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return string.IsNullOrEmpty(m_title)
                    && string.IsNullOrEmpty(m_description)
                    && string.IsNullOrEmpty(m_usageGuide)
                    && m_screenshot == null;
            }
        }
    }
}
#endif
