#if VISTA
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Passed to <see cref="IBiomeTemplateSpawnCallbackReceiver.OnSpawnedFromBiomeTemplate"/> during a template spawn. Carries the
    /// runtime inputs (the owning <see cref="VistaManager"/>), acts as the <b>collect sink</b> for objects
    /// the biome cloned and now owns, and is the <b>clone authority</b> that keeps one clone per source asset
    /// across all receivers in the spawn (see <see cref="GetOrCreateClone{TObject}"/>).
    /// </summary>
    /// <remarks>
    /// The biome <b>pushes</b> its in-memory clones here (<see cref="RegisterOwnedObject"/>); the editor caller
    /// <b>pulls and persists</b> them beside the scene; at runtime the caller ignores them and the clones live
    /// in memory. This mirrors how a terrain tile reports owned objects via <c>ITile.GetDataAssets()</c> and
    /// only the editor-side factory writes assets. Purely runtime, no editor dependency, and no persistence
    /// details (target folder, undo) are exposed here, those live in the editor shell.
    /// </remarks>
    public class BiomeTemplateSpawnContext
    {
        private readonly List<Object> m_ownedObjects = new List<Object>();

        /// <summary>The manager that will own the spawned biome, or null.</summary>
        public VistaManager manager { get; }

        /// <summary>Objects the biome cloned and owns, in registration order, for the caller to persist.</summary>
        public IReadOnlyList<Object> ownedObjects
        {
            get { return m_ownedObjects; }
        }

        public BiomeTemplateSpawnContext(VistaManager manager)
        {
            this.manager = manager;
        }

        /// <summary>
        /// Register an in-memory object the biome cloned and now owns. Null and duplicates are ignored. The
        /// caller persists these (editor) or discards them (runtime).
        /// </summary>
        public void RegisterOwnedObject(Object obj)
        {
            if (obj != null && !m_ownedObjects.Contains(obj))
            {
                m_ownedObjects.Add(obj);
            }
        }

        private readonly Dictionary<Object, Object> m_clonesBySource = new Dictionary<Object, Object>();

        /// <summary>
        /// Returns the clone this spawn already made for <paramref name="source"/>, or instantiates one now and
        /// registers it as owned. Keyed by the source asset, so receivers that share an authored asset in the
        /// template end up sharing one clone, preserving the template's reference topology instead of exploding
        /// it into one clone per receiver. <paramref name="created"/> tells the caller whether this call created
        /// the clone, so one time setup (naming, for example) runs exactly once and never rewrites what an
        /// earlier receiver did.
        /// </summary>
        public TObject GetOrCreateClone<TObject>(TObject source, out bool created) where TObject : Object
        {
            if (source == null)
            {
                throw new System.ArgumentNullException(nameof(source));
            }

            if (m_clonesBySource.TryGetValue(source, out Object existingClone))
            {
                created = false;
                return (TObject)existingClone;
            }

            TObject clone = Object.Instantiate(source);
            m_clonesBySource.Add(source, clone);
            RegisterOwnedObject(clone);
            created = true;
            return clone;
        }
    }
}
#endif
