#if VISTA
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    /// <summary>
    /// Serializes graph elements into lightweight JSON payloads that also carry the concrete runtime type needed for reconstruction.
    /// </summary>
    public static class Serializer
    {
        public const int CURRENT_VERSION = 1;

        /// <summary>
        /// Temporarily assigns <see cref="target"/> so serialization warnings can be logged against a specific Unity object.
        /// </summary>
        public struct TargetScope : IDisposable
        {
            private readonly UnityEngine.Object m_previousTarget;

            /// <summary>
            /// Sets the temporary logging target used by this serializer.
            /// </summary>
            /// <param name="target">Unity object to associate with serialization warnings during the scope lifetime.</param>
            public TargetScope(UnityEngine.Object target)
            {
                m_previousTarget = Serializer.target;
                Serializer.target = target;
            }

            /// <summary>
            /// Clears the temporary logging target when the scope ends.
            /// </summary>
            public void Dispose()
            {
                Serializer.target = m_previousTarget;
            }
        }

        [Serializable]
        /// <summary>
        /// Stores the minimal type information needed to locate a serialized object's runtime type again.
        /// </summary>
        public struct TypeInfo
        {
            [SerializeField]
            /// <summary>
            /// Full type name used to search through loaded assemblies during deserialization.
            /// </summary>
            public string fullName;

            /// <summary>
            /// Indicates whether this record contains enough data to attempt a type lookup.
            /// </summary>
            public bool IsValid
            {
                get
                {
                    return !string.IsNullOrEmpty(fullName);
                }
            }
        }

        [Serializable]
        /// <summary>
        /// Combines serialized JSON data with the concrete type information required to reconstruct the original object.
        /// </summary>
        public struct JsonObject : IEquatable<JsonObject>
        {
            [SerializeField]
            /// <summary>
            /// Serialized runtime type descriptor.
            /// </summary>
            public TypeInfo typeInfo;

            [SerializeField]
            /// <summary>
            /// Raw JSON payload produced by <see cref="JsonUtility.ToJson(object)"/>.
            /// </summary>
            public string jsonData;

            [SerializeField]
            /// <summary>
            /// Serialized data format version.
            /// </summary>
            public int version;

            /// <summary>
            /// Compares both the serialized type name and the JSON payload.
            /// </summary>
            /// <param name="other">Serialized object to compare against.</param>
            public bool Equals(JsonObject other)
            {
                return String.Compare(typeInfo.fullName, other.typeInfo.fullName) == 0 &&
                        String.Compare(jsonData, other.jsonData) == 0 &&
                        version == other.version;
            }

            /// <summary>
            /// Serializes this wrapper itself into JSON for debugging or nested storage.
            /// </summary>
            public override string ToString()
            {
                return JsonUtility.ToJson(this);
            }
        }

        /// <summary>
        /// Returns a sentinel serialized value representing an empty element.
        /// </summary>
        public static JsonObject NullElement
        {
            get
            {
                return new JsonObject()
                {
                    typeInfo = new TypeInfo(),
                    jsonData = null,
                    version = 0
                };
            }
        }

        /// <summary>
        /// Unity object associated with warning logs emitted by the batch serialize and deserialize helpers.
        /// </summary>
        public static UnityEngine.Object target { get; set; }

        /// <summary>
        /// Converts a runtime type into the compact type record stored inside <see cref="JsonObject"/>.
        /// </summary>
        /// <param name="type">Runtime type to record.</param>
        public static TypeInfo GetTypeAsSerializedData(Type type)
        {
            return new TypeInfo
            {
                fullName = type.FullName
            };
        }

        /// <summary>
        /// Resolves a runtime type by searching the currently loaded assemblies for the stored full type name.
        /// </summary>
        /// <param name="typeInfo">Serialized type descriptor.</param>
        /// <returns>The matching runtime type, or <see langword="null"/> if no loaded assembly defines it.</returns>
        public static Type GetTypeFromSerializedData(TypeInfo typeInfo)
        {
            if (!typeInfo.IsValid)
                return null;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type type = assembly.GetType(typeInfo.fullName);
                if (type != null)
                    return type;
            }

            return null;
        }

        /// <summary>
        /// Serializes one object into a <see cref="JsonObject"/> that preserves both its JSON payload and concrete type.
        /// </summary>
        /// <typeparam name="T">Compile-time type of the item being serialized.</typeparam>
        /// <param name="item">Object to serialize.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="item"/> is <see langword="null"/>.</exception>
        public static JsonObject Serialize<T>(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item", $"Cannot serialize null item");
            }

            // Some in memory objects are placeholders for serialized graph nodes whose real runtime type cannot be resolved
            // in the current project. A common case is a graph authored with a module that is not installed, such as a Pro
            // only node opened in a Personal project. If we serialized the placeholder instance itself, we would write the
            // placeholder type name and its reduced in memory state back into the asset. That would permanently replace the
            // original payload, so installing the missing module later would not be able to reconstruct the real node.
            // Instead, placeholders expose the exact JsonObject that was read from disk before type resolution failed.
            // Returning that preserved payload here keeps the serialized type name, raw json data, and original version
            // byte for byte intact across load and save. This branch is intentionally an early return because preserved
            // payloads must bypass all normal serialization logic, including restamping the current version.
            if (item is IPreservedSerializedData preservedSerializedData)
            {
                return preservedSerializedData.preservedSerializedData;
            }

            TypeInfo typeInfo = GetTypeAsSerializedData(item.GetType());
            string jsonData = JsonUtility.ToJson(item);

            JsonObject serializedElement = new JsonObject()
            {
                typeInfo = typeInfo,
                jsonData = jsonData,
                // Newly emitted payloads always use the current serializer format version.
                version = CURRENT_VERSION
            };
            return serializedElement;
        }

        /// <summary>
        /// Reconstructs one object from its serialized wrapper and optional constructor arguments.
        /// </summary>
        /// <typeparam name="T">Expected base type of the deserialized object.</typeparam>
        /// <param name="item">Serialized wrapper containing the runtime type and JSON payload.</param>
        /// <param name="constructorArgs">Constructor arguments used when creating the instance before overwriting fields from JSON.</param>
        /// <exception cref="ArgumentException">Thrown when the serialized type is invalid or cannot be resolved from loaded assemblies.</exception>
        public static T Deserialize<T>(JsonObject item, params object[] constructorArgs) where T : class
        {
            if (!item.typeInfo.IsValid)
            {
                throw new ArgumentException($"Cannot deserialize the item, object type is invalid.");
            }

            Type type = GetTypeFromSerializedData(item.typeInfo);
            if (type == null)
            {
                throw new ArgumentException($"Cannot deserialize the item, the type [{item.typeInfo.fullName}] is not exist.");
            }

            T instance;
            try
            {
                CultureInfo culture = CultureInfo.CurrentCulture;
                BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                instance = Activator.CreateInstance(type, flags, null, constructorArgs, culture) as T;
            }
            catch
            {
                throw;
            }

            if (instance != null && !string.IsNullOrEmpty(item.jsonData))
            {
                JsonUtility.FromJsonOverwrite(item.jsonData, instance);
            }
            return instance;
        }

        /// <summary>
        /// Serializes a sequence into a list of <see cref="JsonObject"/> wrappers, skipping failed items and logging warnings instead of aborting the whole batch.
        /// </summary>
        /// <typeparam name="T">Compile-time type of the items being serialized.</typeparam>
        /// <param name="items">Sequence to serialize.</param>
        public static List<JsonObject> Serialize<T>(IEnumerable<T> items)
        {
            List<JsonObject> serializedItems = new List<JsonObject>();
            if (items == null)
            {
                return serializedItems;
            }

            foreach (T i in items)
            {
                try
                {
                    serializedItems.Add(Serialize(i));
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e, target);
                }
            }
            return serializedItems;
        }

        /// <summary>
        /// Deserializes a sequence of serialized wrappers, skipping failed items and logging warnings instead of aborting the whole batch.
        /// </summary>
        /// <typeparam name="T">Expected base type of the deserialized objects.</typeparam>
        /// <param name="items">Serialized wrappers to reconstruct.</param>
        /// <param name="constructorArgs">Constructor arguments forwarded to each object creation call.</param>
        public static List<T> Deserialize<T>(IEnumerable<JsonObject> items, params object[] constructorArgs) where T : class
        {
            List<T> deserializedItems = new List<T>();
            if (items == null)
            {
                return deserializedItems;
            }

            foreach (JsonObject i in items)
            {
                try
                {
                    T item = Deserialize<T>(i, constructorArgs);
                    deserializedItems.Add(item);
                }
                catch (Exception e)
                {
                    Debug.LogWarning(e, target);
                }
            }
            return deserializedItems;
        }

        /// <summary>
        /// Deserializes a sequence of serialized wrappers, using a fallback factory when type resolution fails for an item.
        /// </summary>
        /// <typeparam name="T">Expected base type of the deserialized objects.</typeparam>
        /// <param name="items">Serialized wrappers to reconstruct.</param>
        /// <param name="fallbackFactory">Factory that can create an in memory placeholder from the original serialized payload.</param>
        /// <param name="constructorArgs">Constructor arguments forwarded to each object creation call.</param>
        public static List<T> Deserialize<T>(IEnumerable<JsonObject> items, Func<JsonObject, T> fallbackFactory, params object[] constructorArgs) where T : class
        {
            List<T> deserializedItems = new List<T>();
            if (items == null)
            {
                return deserializedItems;
            }

            foreach (JsonObject i in items)
            {
                try
                {
                    T item = Deserialize<T>(i, constructorArgs);
                    deserializedItems.Add(item);
                }
                catch (Exception e)
                {
                    if (fallbackFactory == null)
                    {
                        Debug.LogWarning(e, target);
                        continue;
                    }

                    try
                    {
                        T fallbackItem = fallbackFactory(i);
                        if (fallbackItem != null)
                        {
                            string warningMessage = $"Cannot deserialize item of type [{i.typeInfo.fullName}]. A fallback placeholder was created and the original serialized data was preserved.";
                            Debug.LogWarning(warningMessage, target);
                            deserializedItems.Add(fallbackItem);
                        }
                        else
                        {
                            Debug.LogWarning(e, target);
                        }
                    }
                    catch (Exception fallbackException)
                    {
                        Debug.LogWarning(e, target);
                        Debug.LogWarning(fallbackException, target);
                    }
                }
            }
            return deserializedItems;
        }
    }
}
#endif


