#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Sizes its <see cref="LocalProceduralBiome"/> polygon to cover a fixed world-space area (width by length),
    /// centered on the biome, after it is spawned from a template. The explicit-size counterpart to
    /// <see cref="SnapBiomeToTilesOnSpawn"/>, for when the biome should cover a set area rather than fit the
    /// terrain. Only touches the XZ polygon, so it composes with a separate height component.
    /// </summary>
    /// <remarks>
    /// Implements <see cref="IBiomeTemplateSpawnCallbackReceiver"/> so it runs on spawn, and offers a context-menu
    /// re-apply. Pure runtime, no editor dependency. Use at most one XZ sizing component per biome (this or
    /// <see cref="SnapBiomeToTilesOnSpawn"/>), they both write the anchor polygon.
    /// </remarks>
    [RequireComponent(typeof(LocalProceduralBiome))]
    [AddComponentMenu("Vista/Biome Template/Set Biome Area On Spawn")]
    public class SetBiomeAreaOnSpawn : MonoBehaviour, IBiomeTemplateSpawnCallbackReceiver
    {
        [SerializeField]
        [Tooltip("The world-space area the biome should cover: X width, Y length.")]
        private Vector2 m_worldArea = new Vector2(1000f, 1000f);
        /// <summary>The world-space area the biome should cover (X width, Y length).</summary>
        public Vector2 worldArea
        {
            get { return m_worldArea; }
            set { m_worldArea = value; }
        }

        /// <summary>Sizes the biome polygon as part of the template spawn.</summary>
        public void OnSpawnedFromBiomeTemplate(BiomeTemplate template, BiomeTemplateSpawnContext context)
        {
            // Both arguments are contract required, never null (see IBiomeTemplateSpawnCallbackReceiver), even
            // though this component reads neither today.
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }
            ApplyArea();
        }

        /// <summary>
        /// Set the biome's anchor polygon to a <see cref="worldArea"/> rectangle centered on the biome, in the
        /// same winding as <c>LocalProceduralBiome.Reset</c>. Local space with unit XZ scale (the spawn
        /// transform), so the local extents map one to one to world units.
        /// </summary>
        [ContextMenu("Apply Area")]
        public void ApplyArea()
        {
            LocalProceduralBiome biome = GetComponent<LocalProceduralBiome>();
            if (biome == null)
            {
                return;
            }

            float halfWidth = Mathf.Max(0f, m_worldArea.x) * 0.5f;
            float halfLength = Mathf.Max(0f, m_worldArea.y) * 0.5f;
            biome.anchors = new Vector3[]
            {
                new Vector3(-halfWidth, 0f, -halfLength),
                new Vector3(-halfWidth, 0f, halfLength),
                new Vector3(halfWidth, 0f, halfLength),
                new Vector3(halfWidth, 0f, -halfLength),
            };
        }
    }
}
#endif
