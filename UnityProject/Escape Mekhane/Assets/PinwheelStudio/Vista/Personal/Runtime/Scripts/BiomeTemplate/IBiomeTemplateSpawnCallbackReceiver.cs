#if VISTA
namespace Pinwheel.Vista
{
    /// <summary>
    /// Implemented by <b>any</b> component on a <see cref="BiomeTemplate"/>'s biome prefab that needs to run
    /// custom logic when the prefab is spawned as a disconnected clone. After the clone is created, the spawner
    /// calls <see cref="OnSpawnedFromBiomeTemplate"/> on <b>every</b> component in the clone that implements this
    /// interface (see <see cref="BiomeTemplateSpawner"/>), in <c>GetComponentsInChildren</c> order.
    /// </summary>
    /// <remarks>
    /// This is deliberately <b>not</b> a biome level contract. The biome component clones its own owned graph,
    /// but the prefab may carry other components that own their own references or drive their own spawn
    /// behavior, for example a helper that owns a cloned asset the biome does not, or a generator that seeds
    /// points of interest. Each such component implements this interface and handles only its own concern.
    /// <para>
    /// Pure runtime contract, no editor dependency. A receiver clones its owned references <b>in memory</b>,
    /// reassigns them to itself, and registers each clone in the context so the caller can persist them (editor)
    /// or discard them (runtime). It must never touch the AssetDatabase. Cloning is what keeps editing the new
    /// biome from mutating the template. Shared library assets stay referenced and are left alone.
    /// </para>
    /// </remarks>
    public interface IBiomeTemplateSpawnCallbackReceiver
    {
        /// <summary>
        /// React to this component's owning object being spawned from a template. Clone any owned references in
        /// memory, reassign them to this component, and register each clone in <paramref name="context"/> for
        /// the caller to persist. <paramref name="template"/> is the source template being instantiated. Run any
        /// other per spawn behavior here. Must not touch the AssetDatabase.
        /// </summary>
        /// <remarks>
        /// Both parameters are required, never null: the spawner guarantees a validated template and a live
        /// context, and any other caller must supply the same. The context is what coordinates clone sharing
        /// across receivers in one spawn, so a receiver that clones owned references should treat a null
        /// argument as a broken precondition and throw, not degrade to cloning in isolation, and not tolerate a
        /// null template just because it happens not to read it today.
        /// </remarks>
        void OnSpawnedFromBiomeTemplate(BiomeTemplate template, BiomeTemplateSpawnContext context);
    }
}
#endif
