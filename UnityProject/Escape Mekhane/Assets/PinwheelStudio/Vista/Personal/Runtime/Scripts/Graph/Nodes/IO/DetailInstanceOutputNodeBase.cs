#if VISTA
namespace Pinwheel.Vista.Graph
{
    public abstract class DetailInstanceOutputNodeBase : InstanceOutputNodeBase
    {
        public abstract DetailTemplate detailTemplate { get; set; }

        public override bool isBypassed
        {
            get { return false; }
            set { m_isBypassed = false; }
        }
    }
}
#endif
