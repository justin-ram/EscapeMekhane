#if VISTA
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    /// <summary>
    /// Owns transient resources created during one graph execution.
    /// </summary>
    public sealed class TransientResourceRegistry : IDisposable
    {
        private readonly List<ResourceEntry> m_resources;
        private readonly HashSet<object> m_registeredResources;
        private bool m_isDisposed;

        /// <summary>
        /// Creates an empty transient-resource registry.
        /// </summary>
        public TransientResourceRegistry()
        {
            m_resources = new List<ResourceEntry>();
            m_registeredResources = new HashSet<object>(ReferenceEqualityComparer.instance);
        }

        /// <summary>
        /// Transfers ownership of a Unity object to the registry.
        /// </summary>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="resource"/> is null or has already been destroyed.
        /// </exception>
        public void RegisterDestroyLater(UnityEngine.Object resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            Register(resource, new DestroyableTransientResource(resource));
        }

        /// <summary>
        /// Transfers ownership of a disposable object to the registry.
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="resource"/> is null.</exception>
        public void RegisterDisposeLater(IDisposable resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            Register(resource, resource);
        }

        /// <summary>
        /// Transfers ownership of a loaded Resources asset to the registry.
        /// </summary>
        public void RegisterUnloadLater(UnityEngine.Object resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            Register(resource, new UnloadableTransientResource(resource));
        }

        /// <summary>
        /// Releases a registered Unity object without destroying it.
        /// </summary>
        /// <param name="resource">Resource whose ownership returns to the caller.</param>
        /// <returns><see langword="true"/> when the registry owned and released the resource.</returns>
        public bool Unregister(UnityEngine.Object resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            return UnregisterResource(resource);
        }

        /// <summary>
        /// Releases a registered disposable object without disposing it.
        /// </summary>
        /// <param name="resource">Resource whose ownership returns to the caller.</param>
        /// <returns><see langword="true"/> when the registry owned and released the resource.</returns>
        public bool Unregister(IDisposable resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            return UnregisterResource(resource);
        }

        /// <summary>
        /// Disposes registered resources in reverse registration order.
        /// </summary>
        /// <remarks>
        /// Every resource is given a chance to dispose. When one or more resources fail, their
        /// exceptions are reported together after cleanup has finished. Repeated calls have no effect.
        /// </remarks>
        public void Dispose()
        {
            if (m_isDisposed)
            {
                return;
            }

            m_isDisposed = true;
            List<Exception> exceptions = null;

            for (int i = m_resources.Count - 1; i >= 0; --i)
            {
                try
                {
                    m_resources[i].disposable.Dispose();
                }
                catch (Exception e)
                {
                    if (exceptions == null)
                    {
                        exceptions = new List<Exception>();
                    }
                    exceptions.Add(e);
                }
            }

            m_resources.Clear();
            m_registeredResources.Clear();

            if (exceptions != null)
            {
                throw new AggregateException("One or more transient resources failed to dispose.", exceptions);
            }
        }

        /// <summary>
        /// Records an owned resource and the disposable object responsible for releasing it.
        /// </summary>
        /// <remarks>
        /// For an <see cref="IDisposable"/> resource, both arguments reference the same object. A
        /// <see cref="UnityEngine.Object"/> is not disposable, so its overload creates a new wrapper
        /// and passes the Unity object separately as <paramref name="resourceIdentity"/>.
        /// The registry must compare that original object because two wrappers around the same Unity
        /// resource have different identities and would otherwise bypass duplicate detection. When
        /// the original object is already registered, its first registration remains authoritative
        /// and the duplicate registration is ignored.
        /// </remarks>
        /// <param name="resourceIdentity">The original object whose ownership was transferred.</param>
        /// <param name="disposable">The object to dispose when the registry is disposed.</param>
        private void Register(object resourceIdentity, IDisposable disposable)
        {
            if (m_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TransientResourceRegistry));
            }

            if (!m_registeredResources.Add(resourceIdentity))
            {
                return;
            }

            m_resources.Add(new ResourceEntry(resourceIdentity, disposable));
        }

        private bool UnregisterResource(object resourceIdentity)
        {
            if (m_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TransientResourceRegistry));
            }
            if (!m_registeredResources.Remove(resourceIdentity))
            {
                return false;
            }

            for (int i = m_resources.Count - 1; i >= 0; --i)
            {
                if (ReferenceEquals(m_resources[i].identity, resourceIdentity))
                {
                    m_resources.RemoveAt(i);
                    break;
                }
            }
            return true;
        }

        private readonly struct ResourceEntry
        {
            public readonly object identity;
            public readonly IDisposable disposable;

            public ResourceEntry(object identity, IDisposable disposable)
            {
                this.identity = identity;
                this.disposable = disposable;
            }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer instance = new ReferenceEqualityComparer();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        private sealed class DestroyableTransientResource : IDisposable
        {
            private UnityEngine.Object m_resource;

            public DestroyableTransientResource(UnityEngine.Object resource)
            {
                m_resource = resource;
            }

            public void Dispose()
            {
                if (m_resource == null)
                {
                    Debug.LogWarning($"Cannot dispose {nameof(DestroyableTransientResource)} because its resource is null.");
                    return;
                }

                UnityEngine.Object resource = m_resource;
                m_resource = null;
                Utilities.Destroy(resource);
            }
        }

        private sealed class UnloadableTransientResource : IDisposable
        {
            private UnityEngine.Object m_resource;

            public UnloadableTransientResource(UnityEngine.Object resource)
            {
                m_resource = resource;
            }

            public void Dispose()
            {
                if (m_resource == null)
                {
                    Debug.LogWarning($"Cannot dispose {nameof(UnloadableTransientResource)} because its resource is null.");
                    return;
                }

                UnityEngine.Object resource = m_resource;
                m_resource = null;
                Resources.UnloadAsset(resource);
            }
        }
    }
}
#endif
