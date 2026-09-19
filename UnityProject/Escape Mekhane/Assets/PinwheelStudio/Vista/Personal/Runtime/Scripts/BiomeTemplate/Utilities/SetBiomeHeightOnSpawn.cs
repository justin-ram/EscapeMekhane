#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Matches its <see cref="LocalProceduralBiome"/>'s height to a fixed world-space height after it is spawned
    /// from a template, even when that differs from the Vista Manager's max terrain height. Only touches the Y
    /// scale and the height-blend option, so it composes with any XZ sizing component (for example
    /// <see cref="SnapBiomeToTilesOnSpawn"/> or <see cref="SetBiomeAreaOnSpawn"/>).
    /// </summary>
    /// <remarks>
    /// A biome graph outputs its height against the Manager's <c>terrainMaxHeight</c>. To make the full output
    /// reach <see cref="worldHeight"/> in world space instead, the transform Y is scaled by
    /// <c>worldHeight / terrainMaxHeight</c> and <c>BiomeBlendOptions.useTransformForHeightBlend</c> is enabled,
    /// so height blending actually reads that Y scale (and Y position). For example, a 400 tall biome under a 600
    /// max height gets a 0.66 Y scale. Implements <see cref="IBiomeTemplateSpawnCallbackReceiver"/> so it runs on
    /// spawn (using the Manager on the context), and offers a context-menu re-apply. Pure runtime, no editor
    /// dependency.
    /// </remarks>
    [RequireComponent(typeof(LocalProceduralBiome))]
    [AddComponentMenu("Vista/Biome Template/Set Biome Height On Spawn")]
    public class SetBiomeHeightOnSpawn : MonoBehaviour, IBiomeTemplateSpawnCallbackReceiver
    {
        [SerializeField]
        [Tooltip("The world-space height the biome should cover, in meters.")]
        private float m_worldHeight = 400f;
        /// <summary>The world-space height the biome should cover, in meters.</summary>
        public float worldHeight
        {
            get { return m_worldHeight; }
            set { m_worldHeight = value; }
        }

        /// <summary>Matches the biome height to the spawning Manager's max terrain height as part of the spawn.</summary>
        public void OnSpawnedFromBiomeTemplate(BiomeTemplate template, BiomeTemplateSpawnContext context)
        {
            // Both arguments are contract required, never null (see IBiomeTemplateSpawnCallbackReceiver). The
            // manager on the context may still be null, which ApplyHeight handles by doing nothing.
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }
            ApplyHeight(context.manager);
        }

        /// <summary>Re-apply the world height, resolving the Vista Manager from the hierarchy.</summary>
        [ContextMenu("Apply Height")]
        public void ApplyHeight()
        {
            ApplyHeight(GetComponentInParent<VistaManager>());
        }

        /// <summary>
        /// Scale the biome's transform Y so its full height output lands at <see cref="worldHeight"/> in world
        /// space against <paramref name="manager"/>'s max terrain height, and turn on transform contribution so
        /// height blending reads that Y scale. Does nothing when there is no manager, no biome, or the manager's
        /// max height is zero.
        /// </summary>
        public void ApplyHeight(VistaManager manager)
        {
            if (manager == null || manager.terrainMaxHeight <= 0f)
            {
                return;
            }

            LocalProceduralBiome biome = GetComponent<LocalProceduralBiome>();
            if (biome == null)
            {
                return;
            }

            Vector3 scale = transform.localScale;
            scale.y = Mathf.Max(0f, m_worldHeight) / manager.terrainMaxHeight;
            transform.localScale = scale;

            BiomeBlendOptions options = biome.blendOptions;
            options.useTransformForHeightBlend = true;
            biome.blendOptions = options;
        }
    }
}
#endif
