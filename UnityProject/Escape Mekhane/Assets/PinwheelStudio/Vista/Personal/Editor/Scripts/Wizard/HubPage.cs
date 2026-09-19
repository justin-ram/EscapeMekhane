#if VISTA
using System;
using System.Collections.Generic;
using Pinwheel.VistaEditor.UIElements;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>Top level hub destinations, one per left nav item.</summary>
    public enum HubNavId
    {
        Home,
        CreateInScene,
        CreateAsset,
        Tools,
    }

    /// <summary>
    /// Abstract base for every page that lives inside the hub. It renders the shared two pane layout,
    /// a persistent left nav plus a right content pane, the way an HTML layout template renders the same
    /// chrome around different page bodies. A concrete page only declares two things: which nav item is
    /// <see cref="HighlightedNav"/>, and what goes in the right pane via <see cref="BuildContent"/>.
    ///
    /// There is no separate, shared sidebar element to keep in sync. Each hub page builds its own copy
    /// of the nav with its own item highlighted, baked in at push time. Navigation is the plain window
    /// stack: clicking a nav item pushes that destination, and back simply reveals the previous page,
    /// whose nav is already highlighted correctly. It reads like multi level navigation but it is just a
    /// flat stack of pages that happen to share this layout.
    ///
    /// A page that needs multi step content in the right pane manages those steps itself, they are not
    /// on the window stack.
    /// </summary>
    public abstract class HubPage : IWizardPage
    {
        private readonly struct NavEntry
        {
            public readonly HubNavId id;
            public readonly string label;
            public readonly Func<HubPage> factory;

            public NavEntry(HubNavId id, string label, Func<HubPage> factory)
            {
                this.id = id;
                this.label = label;
                this.factory = factory;
            }
        }

        // The left nav. Order here is the order shown. Each entry knows how to create its destination
        // root page, so a nav click is just a push of that page.
        private static readonly NavEntry[] s_nav =
        {
            new NavEntry(HubNavId.Home, "Home", () => new HomePage()),
            new NavEntry(HubNavId.CreateInScene, "Create in Scene", () => new CreateInScenePage()),
            new NavEntry(HubNavId.CreateAsset, "Create Asset", () => new CreateAssetPage()),
            new NavEntry(HubNavId.Tools, "Tools", () => new ToolsPage()),
        };

        private VisualElement m_body;

        /// <summary>The host window. Valid from <see cref="OnPush"/> onward.</summary>
        protected WizardWindow host { get; private set; }

        public VisualElement body => m_body;

        /// <summary>Title shown at the top of the right content pane.</summary>
        public abstract string title { get; }

        /// <summary>Which left nav item is highlighted while this page is shown. A drill down page returns its section, for example a template detail returns <see cref="HubNavId.CreateInScene"/>.</summary>
        protected abstract HubNavId HighlightedNav { get; }

        public virtual void OnPush(WizardWindow host)
        {
            this.host = host;

            VisualElement root = new VisualElement() { name = GetType().Name };
            root.AddToClassList("hub-page");
            root.Add(BuildNav());
            root.Add(BuildContentPane());
            m_body = root;
        }

        private VisualElement BuildNav()
        {
            VisualElement nav = new VisualElement() { name = "hub-nav" };
            nav.AddToClassList("hub-nav");

            foreach (NavEntry entry in s_nav)
            {
                HubNavId id = entry.id;
                Func<HubPage> factory = entry.factory;
                ClickableElement item = new ClickableElement(() => OnNavClicked(factory)) { text = entry.label };
                item.AddToClassList("hub-nav-item");
                if (id == HighlightedNav)
                {
                    item.AddToClassList("hub-nav-item--active");
                }
                nav.Add(item);
            }

            return nav;
        }

        private VisualElement BuildContentPane()
        {
            VisualElement pane = new VisualElement() { name = "hub-content" };
            pane.AddToClassList("hub-content");

            ScrollView scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("hub-content-scroll");
            pane.Add(scroll);

            // Breadcrumb of the page stack, above the title. Shown only when there is somewhere to go
            // back to. The current page is the last crumb and is not clickable.
            BuildBreadcrumb(scroll.contentContainer);

            // Title strip: the title on the left, a slot the page can fill on the right (edition info,
            // a primary action such as Create, and so on).
            VisualElement strip = new VisualElement() { name = "hub-title-strip" };
            strip.AddToClassList("hub-title-strip");

            Label header = new Label(title);
            header.AddToClassList("hub-content-title");
            strip.Add(header);

            VisualElement actions = new VisualElement() { name = "hub-title-strip-actions" };
            actions.AddToClassList("hub-title-strip__actions");
            strip.Add(actions);
            BuildTitleStripActions(actions);

            scroll.contentContainer.AddToClassList("hub-content-body");
            scroll.contentContainer.Add(strip);

            BuildContent(scroll.contentContainer);
            return pane;
        }

        /// <summary>Add content to the right of the title strip, for example edition info or a primary action button. Empty by default.</summary>
        protected virtual void BuildTitleStripActions(VisualElement container) { }

        private void BuildBreadcrumb(VisualElement container)
        {
            IReadOnlyList<string> trail = host.pageTitles;
            if (trail.Count <= 1)
                return;

            VisualElement bar = new VisualElement() { name = "hub-breadcrumb" };
            bar.AddToClassList("hub-breadcrumb");

            for (int i = 0; i < trail.Count; i++)
            {
                bool isCurrent = i == trail.Count - 1;
                if (isCurrent)
                {
                    Label current = new Label(trail[i]);
                    current.AddToClassList("hub-breadcrumb__current");
                    bar.Add(current);
                }
                else
                {
                    // Roll back to this crumb: pop everything above it. popCount is fixed for this page,
                    // its ancestors do not change while it is on top.
                    int popCount = trail.Count - 1 - i;
                    ClickableElement crumb = new ClickableElement(() => host.PopPages(popCount)) { text = trail[i] };
                    crumb.AddToClassList("hub-breadcrumb__item");
                    crumb.Faded();
                    bar.Add(crumb);

                    Label sep = new Label(">");
                    sep.AddToClassList("hub-breadcrumb__sep");
                    bar.Add(sep);
                }
            }

            container.Add(bar);
        }

        private void OnNavClicked(Func<HubPage> factory)
        {
            // Just push the destination. Section roots are nav level pages, so the window collapses the
            // stack to it, the reset is a property of the page, not of this click. Clicking the current
            // section also backs out of any drill down within it.
            host.PushPage(factory());
        }

        /// <summary>Fill the right content pane. The page title header is already placed above this.</summary>
        protected abstract void BuildContent(VisualElement content);

        public virtual bool OnPop(WizardWindow host) { return true; }
        public virtual void OnObscured(WizardWindow host) { }
        public virtual void OnUnobscured(WizardWindow host) { }
        public virtual void OnWindowFocus(WizardWindow host) { }
        public virtual void OnWizardClose(WizardWindow host) { }
    }
}
#endif
