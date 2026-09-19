#if VISTA
namespace Pinwheel.Vista.Graph
{
    /// <summary>
    /// Exposes an original serialized payload that should be re emitted without modification.
    /// </summary>
    public interface IPreservedSerializedData
    {
        Serializer.JsonObject preservedSerializedData
        {
            get;
        }
    }
}
#endif
