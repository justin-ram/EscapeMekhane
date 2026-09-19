#if VISTA
namespace Pinwheel.Vista.Graph
{
    public abstract class TreeOutputNodeBase : InstanceOutputNodeBase
    {
        public abstract TreeTemplate treeTemplate { get; set; }

        public override bool isBypassed
        {
            get { return false; }
            set { m_isBypassed = false; }
        }
    }
}
#endif
