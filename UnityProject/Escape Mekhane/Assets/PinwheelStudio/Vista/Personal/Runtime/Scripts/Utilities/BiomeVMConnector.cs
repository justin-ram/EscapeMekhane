#if VISTA
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Graphics;
using System;

namespace Pinwheel.Vista
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(IBiome))]
    /// <summary>
    /// Connects a biome to a specific <see cref="VistaManager"/> when normal hierarchy lookup is not enough.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Vista usually discovers biomes through the manager's transform hierarchy. This component is the
    /// fallback path for "free" biomes that need to belong to a manager by stored identifier instead of
    /// by parent-child relationship.
    /// </para>
    /// <para>
    /// It participates in manager discovery by listening to <see cref="VistaManager.collectFreeBiomes"/>
    /// and registering its biome only when the querying manager's id matches <see cref="managerId"/>.
    /// </para>
    /// </remarks>
    public class BiomeVMConnector : MonoBehaviour
    {
        /// <summary>
        /// Occurs after a connector changes the Manager identifier used for biome discovery.
        /// </summary>
        public static event Action<BiomeVMConnector> managerIdChanged;

        [SerializeField]
        protected string m_managerId;

        [NonSerialized]
        private string m_lastKnownManagerId;
        [NonSerialized]
        private bool m_isManagerIdInitialized;

        /// <summary>
        /// Identifier of the <see cref="VistaManager"/> that should own the attached biome.
        /// </summary>
        public string managerId
        {
            get
            {
                return m_managerId;
            }
            set
            {
                if (string.Equals(m_managerId, value))
                    return;

                m_managerId = value;
                NotifyManagerIdChanged();
            }
        }

        /// <summary>
        /// Returns the biome component attached to the same GameObject.
        /// </summary>
        /// <returns>
        /// The local <see cref="IBiome"/> required by <see cref="RequireComponent"/>.
        /// </returns>
        public IBiome GetBiome()
        {
            return GetComponent<IBiome>();
        }

        private void OnEnable()
        {
            InitializeManagerId();
            VistaManager.collectFreeBiomes += OnCollectFreeBiome;
        }

        private void OnDisable()
        {
            VistaManager.collectFreeBiomes -= OnCollectFreeBiome;
        }

        private void OnValidate()
        {
            if (!m_isManagerIdInitialized)
            {
                InitializeManagerId();
            }
            else if (!string.Equals(m_lastKnownManagerId, m_managerId))
            {
                NotifyManagerIdChanged();
            }
        }

        private void InitializeManagerId()
        {
            m_lastKnownManagerId = m_managerId;
            m_isManagerIdInitialized = true;
        }

        private void NotifyManagerIdChanged()
        {
            InitializeManagerId();
            managerIdChanged?.Invoke(this);
        }

        private void OnCollectFreeBiome(VistaManager vm, Collector<IBiome> biomes)
        {
            if (string.Equals(vm.id, m_managerId))
            {
                biomes.Add(GetBiome());
            }
        }
    }
}
#endif


