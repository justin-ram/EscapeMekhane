#if VISTA
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Base class for full screen wizard pages, the ones that fill the whole window with no hub nav,
    /// for example onboarding. Provides a scroll view body, fill it in <see cref="OnBuildBody"/>. The
    /// page owns its <see cref="title"/> and renders it itself if it wants one. The window only toggles
    /// body visibility, it never rebuilds the body. For pages that live inside the hub layout, derive
    /// from <see cref="HubPage"/> instead.
    /// </summary>
    public abstract class WizardPage : IWizardPage
    {
        private VisualElement m_body;

        /// <summary>The host window. Valid from <see cref="OnPush"/> onward.</summary>
        protected WizardWindow host { get; private set; }

        public VisualElement body => m_body;

        /// <summary>Title shown by the page itself, the window does not display it.</summary>
        public abstract string title { get; }

        public virtual void OnPush(WizardWindow host)
        {
            this.host = host;

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical) { name = GetType().Name };
            scroll.AddToClassList("wizard-body");
            m_body = scroll;

            OnBuildBody(scroll.contentContainer);
        }

        /// <summary>Build the page body. Add content to <paramref name="content"/>.</summary>
        protected abstract void OnBuildBody(VisualElement content);

        public virtual bool OnPop(WizardWindow host) { return true; }
        public virtual void OnObscured(WizardWindow host) { }
        public virtual void OnUnobscured(WizardWindow host) { }
        public virtual void OnWizardClose(WizardWindow host) { }
    }
}
#endif
