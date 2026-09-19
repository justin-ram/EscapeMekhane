#if VISTA
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.VistaEditor.Graph;
using Pinwheel.VistaEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Drill down from Create in Scene: choose one of the discovered <see cref="BiomeTemplate"/> assets, then
    /// create it under the scene's Vista Manager. Left column is the template list; the right column previews
    /// the selected one (screenshot on top, info below). Wired to <see cref="BiomeTemplateUtils"/> for both
    /// discovery and creation. The default "blank" starting point is itself a template now, not a special case,
    /// so the list is purely data driven.
    /// </summary>
    public class CreateBiomePage : HubPage
    {
        // One entry in the template list, wrapping a discovered BiomeTemplate with its display text resolved up
        // front from the template's SelfDescription, falling back to the asset name when the title is blank.
        private sealed class Entry
        {
            public readonly BiomeTemplate template;
            public readonly string title;
            public readonly string summary;
            public readonly string detail;
            public readonly Texture2D screenshot;
            public readonly string documentationUrl;

            private Entry(BiomeTemplate template, string title, string summary, string detail, Texture2D screenshot, string documentationUrl)
            {
                this.template = template;
                this.title = title;
                this.summary = summary;
                this.detail = detail;
                this.screenshot = screenshot;
                this.documentationUrl = documentationUrl;
            }

            public static Entry FromTemplate(BiomeTemplate template)
            {
                SelfDescription info = template.info;
                string title = !string.IsNullOrEmpty(info.title) ? info.title : template.name;
                return new Entry(template, title, info.description, info.usageGuide, info.screenshot, info.documentationUrl);
            }
        }

        private readonly List<Entry> m_entries = new List<Entry>();
        private readonly Dictionary<Entry, SelectionCard> m_items = new Dictionary<Entry, SelectionCard>();
        private Entry m_selected;
        private VisualElement m_detail;

        // Found on push. The page is only reachable when a Manager exists; if it disappears while the page is
        // open, OnWindowFocus backs out (mirrors CreateTerrainPage).
        private VistaManager m_manager;

        // Suppresses the success modal when the user ticks "Don't show again"; it degrades to a snackbar.
        // Global (EditorPrefs), like the terrain page's equivalent.
        private const string PREF_HIDE_SUCCESS_MODAL = "Pinwheel.Vista.Wizard.HideBiomeSuccessModal";

        public override string title => "Create Biome";

        protected override HubNavId HighlightedNav => HubNavId.CreateInScene;

        public override void OnPush(WizardWindow host)
        {
            m_manager = FindManager();
            LoadEntries();
            base.OnPush(host);
        }

        // Discover the valid templates once per push, before the UI is built so the title strip and content can
        // both read them. Blank is no longer a special entry, the default starting point is itself a template.
        private void LoadEntries()
        {
            m_entries.Clear();
            foreach (BiomeTemplate template in BiomeTemplateUtils.GetValid())
                m_entries.Add(Entry.FromTemplate(template));
            m_selected = m_entries.Count > 0 ? m_entries[0] : null;
        }

        // Without a Manager to own the biome there is nothing to act on. If the one we bound to on push is gone
        // when the window regains focus (deleted in the Hierarchy, scene unloaded), back out to Create in Scene
        // rather than sit on a dead page.
        public override void OnWindowFocus(WizardWindow host)
        {
            if (m_manager == null && host.pageCount > 1)
                host.PopPage();
        }

        // Primary create action in the title strip, mirroring Create Terrain. Disabled when there are no
        // templates, so the empty gallery cannot be "created" from.
        protected override void BuildTitleStripActions(VisualElement container)
        {
            Button create = VistaUI.Button("Create", OnCreateClicked).Primary();
            create.SetEnabled(m_entries.Count > 0);
            container.Add(create);
        }

        protected override void BuildContent(VisualElement content)
        {
            // Empty gallery: no templates in the project yet. Point at how to add one instead of a dead list.
            if (m_entries.Count == 0)
            {
                content.Add(VistaUI.Label("No biome templates found.").P1());
                content.Add(VistaUI.Label("Create one via Assets > Create > Vista > Biome Template, then it will appear here.").P2().Faded());
                return;
            }

            content.Add(VistaUI.Label("Choose a starting point for your biome, then create it under the scene's Vista Manager.").P1());
            content.Add(VistaUI.Spacer(8));

            VisualElement columns = VistaUI.Row();
            columns.AddToClassList("biome-columns");
            content.Add(columns);

            // Left: the template list.
            VisualElement list = new VisualElement();
            list.AddToClassList("biome-template-list");
            columns.Add(list);

            m_items.Clear();
            foreach (Entry entry in m_entries)
            {
                SelectionCard item = MakeTemplateItem(entry);
                m_items[entry] = item;
                list.Add(item);
            }

            // Right: the preview for the selected item (screenshot on top, info below).
            m_detail = new VisualElement();
            m_detail.AddToClassList("biome-detail");
            columns.Add(m_detail);

            RefreshSelection();
        }

        // A selectable list item: bold name over a faded summary. The card look and the selected highlight come
        // from SelectionCard, the same control the terrain type tiles use.
        private SelectionCard MakeTemplateItem(Entry entry)
        {
            Entry captured = entry;
            SelectionCard item = new SelectionCard(() => Select(captured));
            item.AddToClassList("biome-template-item");

            Label name = VistaUI.Label(entry.title).H3();
            name.AddToClassList("biome-template-item__name");
            item.Add(name);

            Label summary = VistaUI.Label(entry.summary).P1().Faded();
            summary.AddToClassList("biome-template-item__summary");
            item.Add(summary);

            return item;
        }

        private void Select(Entry entry)
        {
            m_selected = entry;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            foreach (KeyValuePair<Entry, SelectionCard> pair in m_items)
                pair.Value.selected = pair.Key == m_selected;

            BuildDetail();
        }

        private void BuildDetail()
        {
            m_detail.Clear();
            if (m_selected == null)
                return;

            // Screenshot on top: the template's real art when it has one, else a faded placeholder (Blank and
            // templates without a screenshot).
            VisualElement shot = new VisualElement();
            shot.AddToClassList("biome-detail__screenshot");
            ConfigureScreenshotBox(shot);
            if (m_selected.screenshot != null)
            {
                Image preview = new Image { image = m_selected.screenshot, scaleMode = ScaleMode.ScaleAndCrop };
                preview.AddToClassList("biome-detail__screenshot-image");
                shot.Add(preview);
            }
            else
            {
                shot.Add(VistaUI.Label("No preview").Faded());
            }
            m_detail.Add(shot);

            // Info directly below the screenshot. Summary and usage guide are optional on a template.
            VisualElement info = new VisualElement();
            info.AddToClassList("biome-detail__info");
            
            if (!string.IsNullOrEmpty(m_selected.detail))
                info.Add(VistaUI.Label(m_selected.detail).P1().Faded());

            // Optional "Learn more" link to a full online guide, shown only when the template ships a valid http(s)
            // URL. The inline usage guide above stays the primary source, this is the go deeper affordance.
            if (IsValidDocumentationUrl(m_selected.documentationUrl))
            {
                string url = m_selected.documentationUrl;
                ClickableElement learnMore = VistaUI.Clickable("Learn more", () => Application.OpenURL(url)).Chip();
                learnMore.AddToClassList("biome-detail__doc-link");
                info.Add(learnMore);
            }
            m_detail.Add(info);
        }

        // Guard the link so an empty or malformed value never renders a dead "Learn more". Only absolute http(s)
        // URLs open a browser, anything else is treated as no link.
        private static bool IsValidDocumentationUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
                return false;
            return System.Uri.TryCreate(url, System.UriKind.Absolute, out System.Uri parsed)
                && (parsed.Scheme == System.Uri.UriSchemeHttp || parsed.Scheme == System.Uri.UriSchemeHttps);
        }

        // The preview should read like a proper media frame, not a stretchy utility box. Keep it on the same
        // corner radius as other wizard cards and maintain a stable 16:9 shape as the page width changes.
        private static void ConfigureScreenshotBox(VisualElement shot)
        {
            shot.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                float width = evt.newRect.width;
                if (width <= 0)
                {
                    return;
                }

                float targetHeight = width * 9f / 16f;
                if (Mathf.Abs(shot.resolvedStyle.height - targetHeight) > 0.5f)
                {
                    shot.style.height = targetHeight;
                }
            });
        }

        // Create the selected template under the Manager. Failure reports inline via an error snackbar (the list
        // stays put to fix and retry); success confirms with a modal that hands off into the graph editor. The
        // factory already selects the new biome and records it in Recent Biomes.
        private void OnCreateClicked()
        {
            if (m_selected == null)
                return;

            BiomeTemplateUtils.Result result = BiomeTemplateUtils.CreateFromTemplate(m_manager, m_selected.template);

            VisualElement overlayHost = host?.rootVisualElement;
            if (overlayHost == null)
                return;

            if (!result.success)
            {
                Snackbar.Show(overlayHost, result.message, SnackbarType.Error);
                return;
            }

            // Queue only the regions affected by the newly created biomes. The scene biome service combines
            // overlapping requests and retains the previous footprint for later regional edits.
            for (int i = 0; i < result.biomes.Count; ++i)
            {
                result.biomes[i].NotifyChanged();
            }

            // The modal-or-snackbar choice is the caller's: when the user has opted out, confirm success with a
            // lightweight snackbar instead of the modal.
            if (Modal.IsDontShowAgainSet(PREF_HIDE_SUCCESS_MODAL))
            {
                Snackbar snackbar = Snackbar.Show(
                    overlayHost,
                    result.message,
                    SnackbarType.Success);
                snackbar.dismissed += RecordBiomeCreated;
                return;
            }

            Modal modal = ShowSuccessModal(overlayHost, result);
            modal.closed += RecordBiomeCreated;
        }

        // Builds and shows the success modal: what happened plus an Open Graph Editor hand off, with a "Don't
        // show again" opt-out. The template's usage guide is not repeated here, it already shows in the page's
        // preview pane.
        private static Modal ShowSuccessModal(VisualElement host, BiomeTemplateUtils.Result result)
        {
            Modal modal = Modal.Show(host, result.biomes.Count == 1 ? "Biome Created" : "Biomes Created");
            if (result.biomes.Count == 1)
            {
                modal.AddLine(string.Format("Biome \"{0}\" was created under the Vista Manager and selected in the scene.", result.spawnedRoot.name));
            }
            else
            {
                modal.AddLine(string.Format("{0} biomes were created under \"{1}\" and selected in the scene.", result.biomes.Count, result.spawnedRoot.name));
            }

            // Open Graph is procedural only; a non procedural biome has no graph to open (the same feature-detect
            // as Recent Biomes' Open Graph action, so no Personal→Pro dependency). The hand off is offered only
            // when it is unambiguous: every procedural biome in the group references the same graph (a single
            // biome trivially, or a group authored around one shared graph). With several distinct graphs there
            // is no single graph to open, so the modal stays a plain confirmation.
            IProceduralBiome graphOwner = null;
            bool hasMultipleDistinctGraphs = false;
            foreach (IBiome spawnedBiome in result.biomes)
            {
                IProceduralBiome procedural = spawnedBiome as IProceduralBiome;
                if (procedural == null || procedural.terrainGraph == null)
                {
                    continue;
                }
                if (graphOwner == null)
                {
                    graphOwner = procedural;
                }
                else if (procedural.terrainGraph != graphOwner.terrainGraph)
                {
                    hasMultipleDistinctGraphs = true;
                    break;
                }
            }

            if (graphOwner != null && !hasMultipleDistinctGraphs)
            {
                modal.AddLine(result.biomes.Count == 1
                    ? "Open the Graph Editor to shape it however you like."
                    : "They share one graph. Open the Graph Editor to shape them all at once.");
                IProceduralBiome procedural = graphOwner;
                modal.AddConfirmationFooter(PREF_HIDE_SUCCESS_MODAL, "Open Graph Editor",
                    () => GraphEditorBase.OpenGraph(procedural.terrainGraph, procedural.CreateGraphInputProvider()));
            }
            else
            {
                // Several distinct graphs: no single graph to open, so no CTA, but still point at the next step
                // instead of going silent.
                if (hasMultipleDistinctGraphs)
                {
                    modal.AddLine("Select a biome and open its graph from the biome inspector to shape it.");
                }
                modal.AddConfirmationFooter(PREF_HIDE_SUCCESS_MODAL);
            }
            return modal;
        }

        private static void RecordBiomeCreated()
        {
            SuccessfulActionCounter.Record(
                SuccessfulActionCounter.ActionKeys.BIOME_CREATED_FROM_TEMPLATE);
        }

        // The scene's primary Manager (first enabled instance), or null if there is none.
        private static VistaManager FindManager()
        {
            foreach (VistaManager manager in VistaManager.allInstances)
                return manager;
            return null;
        }
    }
}
#endif
