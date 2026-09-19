#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// A reusable biome preset for the wizard's template gallery. A template is defined at the <b>biome</b>
    /// level, not the graph level: it references a <b>biome prefab</b> that captures the whole biome
    /// configuration (graph, blend mode, external input, child objects, ...), so it generalizes to any biome
    /// type, procedural now and non-procedural later. Selecting it spawns a disconnected, fully editable biome
    /// (see <see cref="BiomeTemplateSpawner"/>).
    /// </summary>
    /// <remarks>
    /// The prefab must contain at least one component implementing <see cref="IBiome"/>, on its root or on any
    /// child, so it can be spawned as a biome; this is what <see cref="IsValid"/> checks. A template may carry
    /// several biomes under a plain root, for example a decoration template offering the same placement graph
    /// with different anchor shapes. Discovery filters on this so the gallery never shows a broken template.
    /// Fixing up owned references at spawn is a separate, opt in concern, handled by any component that
    /// implements <see cref="IBiomeTemplateSpawnCallbackReceiver"/>.
    /// </remarks>
    [CreateAssetMenu(menuName = "Vista/Biome Template", order = -9000)]
    public class BiomeTemplate : ScriptableObject
    {
        [SerializeField]
        [Tooltip("The biome prefab to spawn. It must contain at least one component implementing IBiome, on its root or on any child.")]
        private GameObject m_biomePrefab;
        /// <summary>The biome prefab this template spawns.</summary>
        public GameObject biomePrefab
        {
            get { return m_biomePrefab; }
        }

        [SerializeField]
        private SelfDescription m_info = new SelfDescription();
        /// <summary>Gallery-facing description (title, summary, usage guide, screenshot).</summary>
        public SelfDescription info
        {
            get { return m_info; }
        }

        [SerializeField]
        private int m_sortingNumber = 1000;
        /// <summary>Controls the order in which templates appear in the gallery. Lower values appear first.</summary>
        public int sortingNumber
        {
            get { return m_sortingNumber; }
        }

        /// <summary>
        /// A template is valid when its prefab resolves and at least one component implementing
        /// <see cref="IBiome"/> exists anywhere in the prefab hierarchy, so it can be spawned as a biome.
        /// Invalid templates must never be spawned or shown (see discovery). <paramref name="error"/> holds the
        /// reason when false.
        /// </summary>
        /// <remarks>
        /// Validity does not guarantee the spawned biome fully clones its owned references. That is opt in per
        /// component (<see cref="IBiomeTemplateSpawnCallbackReceiver"/>) and cannot be known statically, so it is
        /// the template author's responsibility.
        /// </remarks>
        public bool IsValid(out string error)
        {
            if (m_biomePrefab == null)
            {
                error = "No biome prefab assigned.";
                return false;
            }

            // Include inactive objects: activeInHierarchy is unreliable on prefab assets, and a disabled biome
            // is an authoring state, not a broken template.
            IBiome biome = m_biomePrefab.GetComponentInChildren<IBiome>(true);
            if (biome == null)
            {
                error = "The biome prefab has no IBiome component anywhere in its hierarchy.";
                return false;
            }

            error = null;
            return true;
        }

#if UNITY_EDITOR
        // Warn the author as soon as an assigned prefab would make an unspawnable template. Only warns once a
        // prefab is set, so a freshly created, still-empty template does not nag.
        private void OnValidate()
        {
            if (m_biomePrefab != null && !IsValid(out string error))
            {
                Debug.LogWarning(string.Format("Biome Template \"{0}\" is invalid and will be hidden from the gallery: {1}", name, error), this);
            }
        }
#endif
    }
}
#endif
