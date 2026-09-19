#if VISTA
using System;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.VistaEditor.UIElements;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// The Vista Wizard window, a plain browser like shell built with UI Toolkit. It owns a thin toolbar
    /// that holds window level controls (today just a back button, shown only when more than one page is
    /// on the stack) and a container that holds page bodies. It does not own page titles or page chrome,
    /// each page renders its own title and content inside its body.
    ///
    /// Navigation is a single page stack. Each page keeps its body element as a child of the container,
    /// only the top one is visible. Page bodies are built once on push and removed whole on pop, the
    /// window only toggles visibility, there is no per frame or per page rebuild.
    ///
    /// Pages come in two shapes, full screen pages that fill the window (see <see cref="WizardPage"/>,
    /// used by onboarding) and hub pages that render a shared left nav plus right content layout (see
    /// <see cref="HubPage"/>). Both are just <see cref="IWizardPage"/> on the same stack.
    /// </summary>
    public class WizardWindow : EditorWindow, ICanShowReviewPrompt
    {
        private const string USS_PATH = "Vista/USS/Wizard";
        private static readonly Vector2 MIN_SIZE = new Vector2(850, 520);

        // The page the window opens on when nothing is queued (for example after a domain reload). A
        // factory, not an instance, since each page body is built fresh on push. Change this to swap the
        // default destination.
        private static readonly Func<IWizardPage> s_defaultPageFactory = () => new HomePage();

        private readonly Stack<IWizardPage> m_pages = new Stack<IWizardPage>();
        private VisualElement m_pagesContainer;
        private Button m_backButton;
        private IWizardPage m_pendingInitialPage;

        public bool isReviewPromptHostInFocus => focusedWindow == this;

        /// <summary>Number of pages currently on the navigation stack.</summary>
        public int pageCount => m_pages.Count;

        /// <summary>The current top page, or null when the stack is empty.</summary>
        public IWizardPage currentPage => m_pages.Count > 0 ? m_pages.Peek() : null;

        /// <summary>Titles of the pages on the stack, ordered from root (first) to current top (last). For breadcrumb rendering.</summary>
        public IReadOnlyList<string> pageTitles
        {
            get
            {
                IWizardPage[] arr = m_pages.ToArray(); // top..bottom
                string[] titles = new string[arr.Length];
                for (int i = 0; i < arr.Length; i++)
                {
                    titles[i] = arr[arr.Length - 1 - i].title; // root..current
                }
                return titles;
            }
        }

        private void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.Clear();
            root.AddToClassList("vista-wizard");

            StyleSheet uss = Resources.Load<StyleSheet>(USS_PATH);
            if (uss != null)
            {
                root.styleSheets.Add(uss);
            }            

            m_pagesContainer = new VisualElement() { name = "pages-container" };
            m_pagesContainer.AddToClassList("wizard-pages-container");
            root.Add(m_pagesContainer);

            ReviewPromptCoordinator.RegisterHost(this);

            // The in memory stack does not survive a domain reload, so the visual tree is always built
            // fresh here. Start on the requested initial page, or the default destination.
            IWizardPage initial = m_pendingInitialPage ?? s_defaultPageFactory();
            m_pendingInitialPage = null;
            TeardownStack(false);
            PushPage(initial);

            //dev
            // PushPage(new CreateInScenePage());
            // PushPage(new CreateSealPage());
        }

        private void OnDisable()
        {
            ReviewPromptCoordinator.UnregisterHost(this);
            TeardownStack(true);
        }

        public void AttachReviewPrompt(ReviewPrompt prompt)
        {
            if (prompt == null)
                return;

            prompt.RemoveFromHierarchy();
            prompt.SetTrackingLocation(UILocation.Wizard_ReviewPrompt);
            prompt.style.position = Position.Absolute;
            prompt.style.right = 12;
            prompt.style.bottom = 12;
            prompt.SetShown(true);
            rootVisualElement.Add(prompt);
            prompt.BringToFront();
        }

        // Forward window focus to the current page, so a page can refresh state that may have changed in
        // the editor while the wizard was unfocused (for example a scene unloaded or an object deleted).
        private void OnFocus()
        {
            currentPage?.OnWindowFocus(this);
        }

        /// <summary>Push a new page, hide the current one, and make the new page visible. The page body is built once here.</summary>
        public bool PushPage(IWizardPage page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));

            if (m_pages.Count > 0 && m_pages.Peek().GetType() == page.GetType())
                return false;

            // A nav level page (a top level hub destination) starts a fresh stack. Pushing one collapses
            // any history beneath it, so the hub never builds up back history at the top level. Done here
            // rather than at the click site, so it holds however the page gets pushed.
            if (page is INavLevelPage)
            {
                TeardownStack(false);
            }

            if (m_pages.Count > 0)
            {
                IWizardPage current = m_pages.Peek();
                current.OnObscured(this);
                SetPageVisible(current, false);
            }

            m_pages.Push(page);
            page.OnPush(this);

            if (page.body != null)
            {
                m_pagesContainer.Add(page.body);
            }
            SetPageVisible(page, true);
            SyncToolbar();
            return true;
        }

        /// <summary>Pop the current page and reveal the one below. The current page may veto this by returning false from OnPop.</summary>
        public bool PopPage()
        {
            if (m_pages.Count == 0)
                return false;

            IWizardPage current = m_pages.Peek();
            if (!current.OnPop(this))
                return false;

            m_pages.Pop();
            current.body?.RemoveFromHierarchy();

            if (m_pages.Count > 0)
            {
                IWizardPage below = m_pages.Peek();
                SetPageVisible(below, true);
                below.OnUnobscured(this);
            }
            SyncToolbar();
            return true;
        }

        /// <summary>
        /// Pop up to count pages off the top, revealing the page beneath. Clamped so the bottom page
        /// always remains, a caller can never empty the stack. Returns how many were actually popped,
        /// which is fewer than requested if a page vetoes via OnPop partway through.
        /// </summary>
        public int PopPages(int count)
        {
            if (count <= 0)
                return 0;
            count = Math.Min(count, m_pages.Count - 1); // never pop the bottom page away
            if (count <= 0)
                return 0;

            int popped = 0;
            while (popped < count)
            {
                IWizardPage p = m_pages.Peek();
                if (!p.OnPop(this)) // veto stops the run early
                    break;
                m_pages.Pop();
                p.body?.RemoveFromHierarchy();
                popped++;
            }

            if (popped > 0)
            {
                IWizardPage top = m_pages.Peek();
                SetPageVisible(top, true);
                top.OnUnobscured(this);
                SyncToolbar();
            }
            return popped;
        }

        /// <summary>Update the toolbar to match the current stack. Today that is just the back button, which stays visible but is only enabled when there is somewhere to go back to.</summary>
        private void SyncToolbar()
        {
            if (m_backButton == null)
                return;

            m_backButton.SetEnabled(m_pages.Count > 1);
        }

        /// <summary>
        /// Tear the whole stack down. OnPop is called on every page to release resources, its return
        /// value is ignored, no page can veto teardown. When <paramref name="wizardClosing"/> is true,
        /// the top page also gets OnWizardClose first.
        /// </summary>
        private void TeardownStack(bool wizardClosing)
        {
            if (m_pages.Count == 0)
                return;

            if (wizardClosing)
            {
                m_pages.Peek().OnWizardClose(this);
            }

            while (m_pages.Count > 0)
            {
                IWizardPage page = m_pages.Pop();
                page.OnPop(this);
                page.body?.RemoveFromHierarchy();
            }
        }

        /// <summary>Show or hide a page by toggling its body element's display.</summary>
        private static void SetPageVisible(IWizardPage page, bool visible)
        {
            if (page?.body == null)
                return;
            page.body.style.display = visible
                ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex)
                : new StyleEnum<DisplayStyle>(DisplayStyle.None);
        }

        private static WizardWindow CreateWindow()
        {
            WizardWindow window = GetWindow<WizardWindow>();
            Texture2D windowIcon = Resources.Load<Texture2D>("Vista/Textures/VistaIconPadded");
            window.titleContent = new GUIContent($"{EditorCommon.GetEditionString()} {EditorCommon.GetVersionString()}", windowIcon);
            window.minSize = MIN_SIZE;
            return window;
        }

        /// <summary>Open the wizard, on the hub if onboarding is already done, otherwise on the Get Started page. Wired to the Window/Vista/Wizard menu in EditorMenus.</summary>
        public static void Open()
        {
            IWizardPage initial = OnboardingManager.HasShownOnboarding()
                ? new HomePage()
                : (IWizardPage)new GetStartedPage();
            CreateWindow().ShowOn(initial);
        }

        /// <summary>Open the wizard on the onboarding Get Started page. Used by the first run trigger and the Window/Vista/Get Started menu in EditorMenus.</summary>
        public static void ShowGetStartedPage()
        {
            CreateWindow().ShowOn(new GetStartedPage());
        }

        /// <summary>Open the wizard directly on a single page, replacing any existing navigation stack.</summary>
        public static void ShowSinglePage(IWizardPage page)
        {
            CreateWindow().ShowOn(page);
        }

        /// <summary>Replace the whole navigation stack with a single page. Use from a page to leave a flow, for example finishing onboarding and going to the hub.</summary>
        public void ResetTo(IWizardPage page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));
            if (m_pagesContainer == null)
            {
                m_pendingInitialPage = page;
                return;
            }
            TeardownStack(false);
            PushPage(page);
        }

        /// <summary>Bring the window up and reset it to the given page. Works whether or not the visual tree has been built yet.</summary>
        private void ShowOn(IWizardPage page)
        {
            m_pendingInitialPage = page;
            Show();
            Focus();

            // If CreateGUI already ran (window was already open), apply the reset now.
            if (m_pagesContainer != null && m_pendingInitialPage != null)
            {
                m_pendingInitialPage = null;
                ResetTo(page);
            }
        }
    }
}
#endif
