#if VISTA
using Pinwheel.Vista;
using Pinwheel.VistaEditor.QuickActions;
using Pinwheel.VistaEditor.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Hub home, the default destination. Dashboard content is being rebuilt.
    /// </summary>
    public class HomePage : HubPage, INavLevelPage
    {
        public override string title => "Vista";

        protected override HubNavId HighlightedNav => HubNavId.Home;

        private SectionHeader m_recentBiomesHeader;
        private RecentBiomesView m_recentBiomesView;
        //private SectionHeader m_newsHeader;
        //private NewsView m_newsView;

        protected override void BuildTitleStripActions(VisualElement container)
        {
            string tier = EditorCommon.IsProEdition() ? "Pro" : EditorCommon.IsIndieEdition() ? "Indie" : "Personal";
            container.Add(VistaUI.Label(EditorCommon.GetVersionString() + " | " + tier + " Edition").Faded());

            // Upgrade nudge, only for editions below Pro.
            if (!EditorCommon.IsProEdition())
            {
                ClickableElement upgrade = VistaUI.Clickable("Upgrade", () =>
                {
                    NetUtils.TrackClick("upgrade-cta", UILocation.Wizard_Home);
                    Application.OpenURL(Links.VISTA_PRO);
                }).Link();
                upgrade.AddToClassList("home-upgrade-cta");
                container.Add(upgrade);
            }
        }

        protected override void BuildContent(VisualElement content)
        {
            QuickActionsView quickActions = new QuickActionsView(host);

            SectionHeader header = new SectionHeader("Quick Actions");
            header.Actions.Add(VistaUI.Clickable("Manage", () => ShowManageMenu(quickActions)).Chip());
            content.Add(header);
            content.Add(quickActions);

            // Recent Biomes. The page owns the header and decides whether to show the section at all,
            // based on whether any valid recent biome remains (invalid entries are pruned by CleanUpAndGet).
            m_recentBiomesHeader = new SectionHeader("Recent Biomes");
            m_recentBiomesView = new RecentBiomesView();
            content.Add(m_recentBiomesHeader);
            content.Add(m_recentBiomesView);

            // Keep the section live while it is in a panel, and drop the subscriptions when it leaves so
            // there is no leak. RecentBiomes.changed covers create/save; the scene events cover load and
            // unload, after which a row's resolved biome would otherwise be stale.
            m_recentBiomesView.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                // Unsubscribe first so a re-attach cannot double-subscribe.
                RecentBiomes.changed -= RefreshRecentBiomes;
                RecentBiomes.changed += RefreshRecentBiomes;
                EditorSceneManager.sceneOpened -= OnSceneOpened;
                EditorSceneManager.sceneOpened += OnSceneOpened;
                EditorSceneManager.sceneClosed -= OnSceneClosed;
                EditorSceneManager.sceneClosed += OnSceneClosed;
                RefreshRecentBiomes();
            });
            m_recentBiomesView.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                RecentBiomes.changed -= RefreshRecentBiomes;
                EditorSceneManager.sceneOpened -= OnSceneOpened;
                EditorSceneManager.sceneClosed -= OnSceneClosed;
            });

            // Status. A "works best with Vista" report (capability, dependencies, integrations). Always
            // shown, built once, a domain reload (e.g. installing an integration) rebuilds the page anyway.
            // Sits above News: it is always present and about the user's own setup, so the conditional,
            // promotional News feed goes last where it can hide without leaving a gap.
            content.Add(new SectionHeader("Status"));
            content.Add(new SystemStatusView());

            // News. Remote feed, the page owns the header and hides the section until items exist. The
            // first fetch is async (a cached payload shows immediately), and NewsService.changed fires when
            // a fetch updates the cache.
            // m_newsHeader = new SectionHeader("News");
            // m_newsView = new NewsView();
            // content.Add(m_newsHeader);
            // content.Add(m_newsView);

            // m_newsView.RegisterCallback<AttachToPanelEvent>(_ =>
            // {
            //     NewsService.changed -= RefreshNews;
            //     NewsService.changed += RefreshNews;
            //     NewsService.EnsureFetched();
            //     RefreshNews();
            // });
            // m_newsView.RegisterCallback<DetachFromPanelEvent>(_ => NewsService.changed -= RefreshNews);
        }

        // The wizard regained focus, so the user may have unloaded a scene or deleted a biome elsewhere
        // (events the section does not otherwise hear). Re-resolve so the rows match the current state.
        public override void OnWindowFocus(WizardWindow host)
        {
            RefreshRecentBiomes();
        }

        private void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RefreshRecentBiomes();
        }

        private void OnSceneClosed(Scene scene)
        {
            RefreshRecentBiomes();
        }

        private void RefreshRecentBiomes()
        {
            bool hasAny = RecentBiomes.CleanUpAndGet().Count > 0;
            DisplayStyle display = hasAny ? DisplayStyle.Flex : DisplayStyle.None;
            m_recentBiomesHeader.style.display = display;
            m_recentBiomesView.style.display = display;
            if (hasAny)
                m_recentBiomesView.Refresh();
        }

        // private void RefreshNews()
        // {
        //     bool hasNews = NewsService.Get().Count > 0;
        //     DisplayStyle display = hasNews ? DisplayStyle.Flex : DisplayStyle.None;
        //     m_newsHeader.style.display = display;
        //     m_newsView.style.display = display;
        //     if (hasNews)
        //         m_newsView.Refresh();
        // }

        private static void ShowManageMenu(QuickActionsView view)
        {
            GenericMenu menu = new GenericMenu();
            foreach (QuickAction action in QuickActionRegistry.All())
            {
                QuickAction captured = action;
                bool pinned = QuickActionPins.IsPinned(action.Id);
                // "Category/Label" makes GenericMenu build a submenu per category.
                menu.AddItem(new GUIContent(action.Category + "/" + action.Label), pinned, () =>
                {
                    if (pinned)
                        QuickActionPins.Unpin(captured.Id);
                    else
                        QuickActionPins.Pin(captured.Id);
                    SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.QUICK_ACTIONS_MANAGED);
                    view.Refresh();
                });
            }
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("Reset to Defaults"), false, () =>
            {
                QuickActionPins.ResetToDefaults();
                SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.QUICK_ACTIONS_MANAGED);
                view.Refresh();
            });
            menu.ShowAsContext();
        }
    }
}
#endif
