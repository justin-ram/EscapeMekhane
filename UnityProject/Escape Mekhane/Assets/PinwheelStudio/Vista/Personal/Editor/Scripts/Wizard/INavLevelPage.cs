#if VISTA
namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Marker for top level hub destinations, the pages reachable directly from the left nav
    /// (Home, Create in Scene, Create Asset, Tools). Pushing a page that implements this starts a fresh stack:
    /// the window collapses any history beneath it, so the hub never accumulates back history at the top
    /// level. The reset is a property of the page being pushed, not of the click that pushed it.
    ///
    /// It is only a marker. A nav level page still derives from <see cref="HubPage"/> for its layout, and
    /// a page that is not marked simply stacks normally (a drill down, with an active back button).
    /// </summary>
    public interface INavLevelPage
    {
    }
}
#endif
