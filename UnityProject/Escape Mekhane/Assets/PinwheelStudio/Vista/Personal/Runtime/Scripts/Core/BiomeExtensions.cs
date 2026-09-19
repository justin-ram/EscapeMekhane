#if VISTA
using System;
using UnityEngine;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Provides convenience generation entry points built on <see cref="VistaManager.Generate"/>.
    /// </summary>
    /// <remarks>These methods select tiles only; request replacement and cancellation remain owned by the Manager.</remarks>
    public static class VMUtils
    {
        /// <summary>Requests generation for every tile currently collected by the Manager.</summary>
        /// <param name="manager">Manager that owns the generation request.</param>
        /// <returns>The task representing this exact all-tiles request.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="manager"/> is <see langword="null"/>.</exception>
        public static GenerationTask GenerateAll(this VistaManager manager)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));

            return manager.Generate(manager.GetTiles());
        }

        /// <summary>Requests generation for one tile.</summary>
        /// <param name="manager">Manager that owns the generation request.</param>
        /// <param name="tile">The complete tile set for this request, consisting of this single tile.</param>
        /// <returns>The task representing this exact single-tile request.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="manager"/> or <paramref name="tile"/> is <see langword="null"/>.
        /// </exception>
        public static GenerationTask Generate(this VistaManager manager, ITile tile)
        {
            if (manager == null)
                throw new ArgumentNullException(nameof(manager));
            if (tile == null)
                throw new ArgumentNullException(nameof(tile));

            return manager.Generate(new ITile[] { tile });
        }
    }

    /// <summary>
    /// Helper extension methods for updating biome state and resolving the owning <see cref="VistaManager"/>.
    /// </summary>
    public static class BiomeExtensions
    {
        internal delegate void ChangedHandler(IBiome biome);
        internal static event ChangedHandler changedCallback;

        /// <summary>
        /// Notifies editor-side biome services that this biome's affected terrain region may have changed.
        /// </summary>
        internal static void NotifyChanged(this IBiome biome)
        {
            if (biome == null)
                throw new ArgumentNullException(nameof(biome));

            changedCallback?.Invoke(biome);
        }

        /// <summary>
        /// Updates the obsolete <see cref="IBiome.updateCounter"/> compatibility property.
        /// </summary>
        /// <param name="b">Biome to mark as changed.</param>
        /// <remarks>
        /// The counter is no longer observed by Vista and this method does not request generation. Call a Manager generation
        /// API explicitly.
        /// </remarks>
        [Obsolete("Biome update counters are no longer used. Request generation explicitly instead.")]
        public static void MarkChanged(this IBiome b)
        {
#pragma warning disable CS0618
            b.updateCounter = System.DateTime.Now.Ticks;
#pragma warning restore CS0618
        }

        /// <summary>
        /// Requests regeneration for the manager associated with the specified biome.
        /// </summary>
        /// <param name="b">Biome whose owning manager should regenerate.</param>
        /// <remarks>
        /// This method resolves a manager through <see cref="GetVistaManagerInstance(IBiome)"/> and then calls <see cref="VMUtils.GenerateAll"/>.
        /// If no manager can be resolved, nothing happens.
        /// </remarks>
        public static void GenerateBiomesInGroup(this IBiome b)
        {
            VistaManager manager = GetVistaManagerInstance(b);
            if (manager != null)
            {
                manager.GenerateAll();
            }
        }

        /// <summary>
        /// Resolves the <see cref="VistaManager"/> that owns a biome.
        /// </summary>
        /// <param name="b">Biome to resolve from.</param>
        /// <returns>
        /// The nearest parent <see cref="VistaManager"/> when one exists; otherwise a manager matched by
        /// <see cref="BiomeVMConnector.managerId"/>; otherwise <see langword="null"/>.
        /// </returns>
        /// <remarks>
        /// The parent lookup path handles standard biome hierarchies. The connector fallback supports biomes that are
        /// not parented under a manager but are linked by id instead.
        /// </remarks>
        public static VistaManager GetVistaManagerInstance(this IBiome b)
        {
            Component biomeComponent = b as Component;
            if (biomeComponent == null)
            {
                return null;
            }

            VistaManager manager = biomeComponent.GetComponentInParent<VistaManager>();
            if (manager == null)
            {
                BiomeVMConnector connector = biomeComponent.GetComponent<BiomeVMConnector>();
                if (connector != null)
                {
                    foreach (VistaManager vm in VistaManager.allInstances)
                    {
                        if (string.Equals(vm.id, connector.managerId))
                        {
                            manager = vm;
                            break;
                        }
                    }
                }
            }
            return manager;
        }
    }
}
#endif


