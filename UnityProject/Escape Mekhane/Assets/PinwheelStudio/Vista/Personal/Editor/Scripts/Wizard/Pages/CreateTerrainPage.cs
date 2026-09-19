#if VISTA
using System;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.VistaEditor.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Drill down from Create in Scene: configure a terrain grid and create it under the given Vista Manager.
    /// A plain drill down page (not an INavLevelPage), so it stacks on Create in Scene with a breadcrumb back
    /// and keeps that nav item highlighted. The backend is chosen from the registered <see cref="ITerrainSystem"/>s
    /// (Unity always, Polaris when installed); the selector only appears when more than one is available.
    /// </summary>
    public class CreateTerrainPage : HubPage
    {
        // Common starting points, mirroring the sizes Vista's sample scenes use. The first seeds the fields.
        private static readonly TerrainGridConfig[] s_builtInConfigs =
        {
            new TerrainGridConfig(1, 1, 1000f, 600f),
            new TerrainGridConfig(2, 2, 1000f, 600f),
            new TerrainGridConfig(4, 4, 1000f, 800f),
            new TerrainGridConfig(2, 2, 500f, 300f),
            new TerrainGridConfig(4, 4, 250f, 150f),
        };

        // Found on push, not passed in. The page is only reachable when a Manager exists; if the one we
        // bound to later disappears, OnWindowFocus pops the page.
        private VistaManager m_manager;

        // The chosen terrain backend, and the tile per backend (for the active highlight). Both come from
        // the registered ITerrainSystems, so Polaris appears only when installed, with no special casing.
        private ITerrainSystem m_system;
        private readonly Dictionary<ITerrainSystem, SelectionCard> m_typeTiles = new Dictionary<ITerrainSystem, SelectionCard>();

        private IntegerField m_columns;
        private IntegerField m_rows;
        private FloatField m_widthLength;
        private FloatField m_height;
        private VisualElement m_chips;

        // When set, the success modal is suppressed and success is confirmed with a snackbar instead. Toggled
        // by the "Don't show again" checkbox in the success modal footer. Global (EditorPrefs), like the other
        // wizard prefs.
        private const string PREF_HIDE_SUCCESS_MODAL = "Pinwheel.Vista.Wizard.HideTerrainSuccessModal";

        public override string title => "Create Terrain";

        protected override HubNavId HighlightedNav => HubNavId.CreateInScene;

        public override void OnPush(WizardWindow host)
        {
            base.OnPush(host);
            m_manager = FindManager();
        }

        // Without a Manager to own the terrain this page has nothing to act on. If the Manager we bound to
        // on push is gone when the window regains focus (deleted in the Hierarchy, scene unloaded), back out
        // to Create in Scene rather than sit on a dead page.
        public override void OnWindowFocus(WizardWindow host)
        {
            if (m_manager == null && host.pageCount > 1)
                host.PopPage();
        }

        protected override void BuildContent(VisualElement content)
        {
            content.Add(VistaUI.Label("Configure the terrain grid, then create it. New tiles are added to the Vista Manager automatically.").P1());

            // A form with a fixed label column (see Wizard.uss, .terrain-form), so every field input lines
            // up and the unlabeled type selector can reserve the exact same indent.
            VisualElement form = new VisualElement();
            form.AddToClassList("terrain-form");
            content.Add(form);

            BuildTypeSelector(form);

            // Grid fields, seeded from the first built-in config.
            TerrainGridConfig seed = s_builtInConfigs[0];
            m_columns = new IntegerField("Columns") { value = seed.columns };
            m_rows = new IntegerField("Rows") { value = seed.rows };
            m_widthLength = new FloatField("Width & Length (m)") { value = seed.widthLength };
            m_height = new FloatField("Height (m)") { value = seed.height };
            form.Add(m_columns);
            form.Add(m_rows);
            form.Add(m_widthLength);
            form.Add(m_height);

            // Config chips: the built-in presets plus the user's saved configs. Clicking one fills the fields
            // instantly; "+ Save current" stores the current values; a saved chip removes via right-click.
            // The row rebuilds live on TerrainGridConfigs.changed while attached.
            m_chips = VistaUI.Row();
            m_chips.AddToClassList("terrain-config-chips");
            m_chips.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                TerrainGridConfigs.changed -= RebuildChips;
                TerrainGridConfigs.changed += RebuildChips;
                RebuildChips();
            });
            m_chips.RegisterCallback<DetachFromPanelEvent>(_ => TerrainGridConfigs.changed -= RebuildChips);
            form.Add(m_chips);
        }

        // The primary action lives in the page title strip (right side), next to the "Create Terrain" title.
        protected override void BuildTitleStripActions(VisualElement container)
        {
            container.Add(VistaUI.Button("Create Terrain", CreateTerrain).Primary());
        }

        private void RebuildChips()
        {
            m_chips.Clear();
            m_chips.Add(LabelSpacer());

            // All chips live in one wrapping row beside the spacer, so every wrapped line shares the indent
            // (the spacer reserves the label column once, the wrap row fills the rest).
            VisualElement wrap = VistaUI.Row();
            wrap.AddToClassList("terrain-config-chips__wrap");
            m_chips.Add(wrap);

            foreach (TerrainGridConfig config in s_builtInConfigs)
                wrap.Add(MakeConfigChip(config, false));

            foreach (TerrainGridConfig config in TerrainGridConfigs.Get())
                wrap.Add(MakeConfigChip(config, true));

            ClickableElement save = VistaUI.Clickable("+ Save current", SaveCurrentConfig);
            save.tooltip = "Save the current grid values as a reusable config";
            wrap.Add(save);
        }

        // A chip that fills the fields with its config on click. Saved (removable) configs also offer a
        // right-click Remove; built-in presets do not.
        private ClickableElement MakeConfigChip(TerrainGridConfig config, bool removable)
        {
            TerrainGridConfig captured = config;
            ClickableElement chip = VistaUI.Clickable(config.DisplayLabel, () => ApplyConfig(captured)).Chip();
            if (removable)
            {
                chip.tooltip = "Right-click to remove";
                chip.AddManipulator(new ContextualMenuManipulator(evt =>
                    evt.menu.AppendAction("Remove", _ => TerrainGridConfigs.Remove(captured))));
            }
            return chip;
        }

        // One selectable tile per registered terrain system, no label. A leading empty spacer carries the
        // field label class so the tiles line up with the field inputs. The selector is shown only when there
        // is a real choice (more than one system); with one, that system is selected silently.
        private void BuildTypeSelector(VisualElement parent)
        {
            List<ITerrainSystem> systems = new List<ITerrainSystem>(VistaManager.GetTerrainSystems());
            if (systems.Count == 0)
                return;

            m_system = systems[0];
            if (systems.Count == 1)
                return;

            VisualElement row = VistaUI.Row();
            row.AddToClassList("terrain-type-row");
            row.Add(LabelSpacer());

            bool first = true;
            foreach (ITerrainSystem system in systems)
            {
                if (!first)
                    row.Add(VistaUI.Spacer(6));
                first = false;

                ITerrainSystem captured = system;
                SelectionCard tile = MakeTypeTile(
                    system.terrainLabel,
                    GetComponentIcon(system.GetTerrainComponentType()),
                    () => SelectSystem(captured));
                m_typeTiles[system] = tile;
                row.Add(tile);
            }

            parent.Add(row);
            RefreshTypeTiles();
        }

        // An empty element that reserves the form's label column width (via the field label class), so the
        // content after it lines up with the field inputs. Used by the type selector and the config chips.
        private static Label LabelSpacer()
        {
            Label spacer = VistaUI.Label();
            spacer.AddToClassList("unity-base-field__label");
            return spacer;
        }

        private SelectionCard MakeTypeTile(string label, Texture icon, Action onClick)
        {
            SelectionCard tile = new SelectionCard(onClick);
            tile.AddToClassList("terrain-type-tile");

            Image iconImage = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
            iconImage.AddToClassList("terrain-type-tile__icon");
            tile.Add(iconImage);

            tile.Add(VistaUI.Label(label));
            return tile;
        }

        // The terrain component's editor icon, or null.
        private static Texture GetComponentIcon(Type type)
        {
            if (type == null)
                return null;

            // Built-in/native component types (Unity Terrain) resolve directly.
            Texture icon = AssetPreview.GetMiniTypeThumbnail(type);

            // Script based types (Polaris GStylizedTerrain) carry their icon on the MonoScript (the
            // MonoImporter icon in the .cs.meta), which the type thumbnail lookup misses. Find the script
            // and read its assigned icon.
            if (icon == null)
            {
                MonoScript script = FindScript(type);
                if (script != null)
                    icon = EditorGUIUtility.GetIconForObject(script);
            }

            return icon;
        }

        // The MonoScript asset whose class is the given type, or null. Used to read a script's assigned icon.
        private static MonoScript FindScript(Type type)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:MonoScript " + type.Name))
            {
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == type)
                    return script;
            }
            return null;
        }

        private void SelectSystem(ITerrainSystem system)
        {
            m_system = system;
            RefreshTypeTiles();
        }

        private void RefreshTypeTiles()
        {
            foreach (KeyValuePair<ITerrainSystem, SelectionCard> pair in m_typeTiles)
                pair.Value.selected = pair.Key == m_system;
        }

        private void ApplyConfig(TerrainGridConfig config)
        {
            m_columns.value = config.columns;
            m_rows.value = config.rows;
            m_widthLength.value = config.widthLength;
            m_height.value = config.height;
        }

        private void SaveCurrentConfig()
        {
            // Add dedupes by value, so re-saving the same grid just moves it to the front.
            TerrainGridConfigs.Add(CurrentConfig());
        }

        // The current (clamped) field values as a config. Used both to save and to create, so empty or
        // negative fields can never make a degenerate grid.
        private TerrainGridConfig CurrentConfig()
        {
            return new TerrainGridConfig(
                Mathf.Max(1, m_columns.value),
                Mathf.Max(1, m_rows.value),
                Mathf.Max(1f, m_widthLength.value),
                Mathf.Max(1f, m_height.value));
        }

        // Success moves on to the full-screen result page; failure stays here and reports inline so the
        // filled-in fields are still right there to fix and retry.
        // Success and failure both surface over this page, so the filled-in fields stay put. Success is a
        // moment with a next step, so it gets a blocking modal; failure is quick status, so it gets a
        // transient error snackbar anchored to the window bottom.
        private void CreateTerrain()
        {
            TerrainGridFactory.Result result = TerrainGridFactory.TryCreate(m_manager, m_system, CurrentConfig());
            if (!result.success)
            {
                ShowFailure(result);
                return;
            }

            VisualElement overlayHost = host?.rootVisualElement;
            if (overlayHost == null)
            {
                RecordTerrainGridCreated();
                return;
            }

            // The modal-or-snackbar choice is the caller's: when the user has opted out, confirm success with a
            // lightweight snackbar instead of the modal.
            if (Modal.IsDontShowAgainSet(PREF_HIDE_SUCCESS_MODAL))
            {
                Snackbar snackbar = Snackbar.Show(
                    overlayHost,
                    result.message,
                    SnackbarType.Success);
                snackbar.dismissed += RecordTerrainGridCreated;
                return;
            }

            Modal modal = ShowSuccessModal(host, result);
            modal.closed += RecordTerrainGridCreated;
        }

        // Builds and shows the success modal: what happened, the next step, a "Create Biome" hand off, and a
        // "Don't show again" opt-out. Static so it can be previewed with a fake Result from WizardWindow dev,
        // not only reached through a real creation.
        internal static Modal ShowSuccessModal(WizardWindow host, TerrainGridFactory.Result result)
        {
            VisualElement overlayHost = host?.rootVisualElement;
            if (overlayHost == null)
                return null;

            Modal modal = Modal.Show(overlayHost, "Terrain Created");
            modal.AddLine(string.Format("{0} terrain {1} created and added to the Vista Manager.",
                result.tileCount, result.tileCount == 1 ? "tile was" : "tiles were"));
            modal.AddLine("Next step:").H3();
            modal.AddLine("Create a biome to shape geometry, texture, and scatter details onto the terrain.");
            modal.AddConfirmationFooter(PREF_HIDE_SUCCESS_MODAL, "Create Biome", () =>
            {
                host?.ResetTo(new CreateInScenePage());
                host?.PushPage(new CreateBiomePage());
            });
            return modal;
        }

        private static void RecordTerrainGridCreated()
        {
            SuccessfulActionCounter.Record(
                SuccessfulActionCounter.ActionKeys.TERRAIN_GRID_CREATED);
        }

        private void ShowFailure(TerrainGridFactory.Result result)
        {
            VisualElement overlayHost = host?.rootVisualElement;
            if (overlayHost == null)
                return;

            Snackbar.Show(overlayHost, result.message, SnackbarType.Error);
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
