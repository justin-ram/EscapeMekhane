#if VISTA
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Runtime core of a template spawn: instantiate a template's biome prefab as a <b>disconnected</b> clone
    /// under a manager, then let each biome in it clone its owned references in memory. No editor dependency, so
    /// it works in a build. The editor-side factory wraps this to add persistence, undo, selection, and recents.
    /// </summary>
    public static class BiomeTemplateSpawner
    {
        /// <summary>
        /// Spawn <paramref name="template"/>'s biome prefab under the manager carried by <paramref name="context"/>
        /// and return the spawned root GameObject. The prefab may hold a single biome on its root or several
        /// biomes under a plain root, so the root object is the one handle that always covers the whole spawn;
        /// callers locate the biomes through <c>GetComponentInChildren</c>. The caller owns the context: it
        /// creates it with the target manager and reads back the in-memory clones each receiver registered on it,
        /// to persist (editor) or ignore (runtime). The context is a class, so it is mutated in place, no
        /// <c>ref</c> needed. Throws if a required argument is null or the template is invalid (no prefab, or no
        /// <see cref="IBiome"/> anywhere in the prefab hierarchy); the caller is expected to pass a validated
        /// template (see <see cref="BiomeTemplate.IsValid"/>), so those throws mark a broken precondition, not a
        /// normal outcome.
        /// </summary>
        public static GameObject Spawn(BiomeTemplate template, BiomeTemplateSpawnContext context)
        {
            if (context == null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }

            // Spawn is a public runtime entry point, so it owns its preconditions even though the gallery and
            // factory validate first. An invalid template reaching here is a broken precondition, not a normal
            // outcome, so fail loud rather than silently produce nothing. The check runs before any Instantiate,
            // so a bad template can never leave an orphan clone parented under the manager.
            if (!template.IsValid(out string templateError))
            {
                throw new System.ArgumentException(templateError, nameof(template));
            }

            VistaManager manager = context.manager;

            // Object.Instantiate on a prefab yields a plain, disconnected clone (never a prefab instance or
            // variant), in both the editor and a build, so the user can edit it freely.
            GameObject clone = Object.Instantiate(template.biomePrefab);
            clone.name = template.biomePrefab.name + " (Cloned from Template)";

            if (manager != null)
            {
                // Reparenting moves the clone into the manager's scene as well.
                clone.transform.SetParent(manager.transform, true);
            }

            // Notify every active component in the clone that opts into the spawn callback, in GetComponentsInChildren
            // order, so each one clones its owned references in memory and registers them in the context, or runs
            // any other per spawn behavior. This is not just the biome: the prefab may carry other components that
            // own their own assets or drive custom spawn logic (a helper owning a cloned asset, a points of
            // interest generator). Components without the contract are simply skipped. Inactive ones are skipped
            // too: a disabled component or GameObject is the author opting it out of the spawn.
            foreach (IBiomeTemplateSpawnCallbackReceiver receiver in clone.GetComponentsInChildren<IBiomeTemplateSpawnCallbackReceiver>())
            {
                receiver.OnSpawnedFromBiomeTemplate(template, context);
            }

            return clone;
        }
    }
}
#endif
