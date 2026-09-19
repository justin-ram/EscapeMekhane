#if VISTA
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// A single page in the Vista Wizard. The window is a plain browser like shell: it owns a thin
    /// toolbar with a back button (shown only when more than one page is on the stack) and a container
    /// that holds page bodies. The window does not own the page title or any page chrome, a page owns
    /// and renders its own <see cref="title"/> inside its <see cref="body"/>.
    ///
    /// <see cref="body"/> is built once, when the page is pushed, and reused across hide and show. It is
    /// only read by the window after <see cref="OnPush"/> has run.
    ///
    /// Pages come in two shapes. Full screen pages (for example onboarding) derive from
    /// <see cref="WizardPage"/> and fill the whole window. Hub pages derive from <see cref="HubPage"/>,
    /// which renders the shared left nav plus right content layout, the equivalent of an HTML layout
    /// template, and bakes its own highlighted nav item in. Back simply reveals the previous page, whose
    /// body already carries the correct nav highlight, so there is no shared nav state to synchronize.
    ///
    /// Pages should hold no heavy resources. If a page must own something disposable, release it in
    /// <see cref="OnPop"/>, which is also called during window teardown (with its return value ignored).
    /// </summary>
    public interface IWizardPage
    {
        /// <summary>The page body. Valid after <see cref="OnPush"/>. The window adds this to its container once and shows or hides it during navigation.</summary>
        VisualElement body { get; }

        /// <summary>Human readable page title. The page renders this itself, the window does not display it.</summary>
        string title { get; }

        /// <summary>Called once when the page is pushed onto the stack. Build <see cref="body"/> here. The page is the top of the stack when this runs.</summary>
        void OnPush(WizardWindow host) { }

        /// <summary>Called when leaving this page for good, either by back navigation or window teardown. Release page resources here. Return false to veto interactive back navigation. The return value is ignored during teardown.</summary>
        bool OnPop(WizardWindow host) { return true; }

        /// <summary>Called when another page is pushed on top of this one. The window hides <see cref="body"/> after this returns.</summary>
        void OnObscured(WizardWindow host) { }

        /// <summary>Called when the page above this one is popped and this becomes the top again. The window shows <see cref="body"/> around this call.</summary>
        void OnUnobscured(WizardWindow host) { }

        /// <summary>Called on the current top page when the wizard window regains focus. Use it to refresh state that may have changed in the editor while the window was unfocused. No-op by default.</summary>
        void OnWindowFocus(WizardWindow host) { }

        /// <summary>Called on the current top page when the window is closing.</summary>
        void OnWizardClose(WizardWindow host) { }
    }
}
#endif
