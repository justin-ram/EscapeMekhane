#if VISTA
using Pinwheel.Vista;
using Pinwheel.VistaEditor.Graph;
using Pinwheel.VistaEditor.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// A vertical list of recent biomes. Each row shows the biome's graph and name and its scene path,
    /// with conditional actions: when the biome's scene is loaded, open its graph (in biome context) or
    /// select it; when it is not, load the scene. Names are resolved live so a later rename stays correct.
    /// Display only, call <see cref="Refresh"/> to rebuild. The hosting page owns the section header and
    /// decides whether to show this view at all, based on whether any valid recent biome remains.
    /// </summary>
    public class RecentBiomesView : VisualElement
    {
        public RecentBiomesView()
        {
            AddToClassList("recent-list");
        }

        public void Refresh()
        {
            Clear();
            foreach (RecentBiomes.Entry entry in RecentBiomes.Get())
            {
                Add(BuildRow(entry));
            }
        }

        private VisualElement BuildRow(RecentBiomes.Entry entry)
        {
            bool sceneLoaded = RecentBiomes.IsSceneLoaded(entry);
            IBiome biome = sceneLoaded ? RecentBiomes.Resolve(entry) : null;

            VisualElement row = new VisualElement();
            row.AddToClassList("recent-row");

            VisualElement info = new VisualElement();
            info.AddToClassList("recent-row__info");

            info.Add(BuildTitle(entry, biome));

            Label metaLabel = VistaUI.Label(entry.scenePath).Faded();
            metaLabel.AddToClassList("recent-row__meta");
            metaLabel.tooltip = entry.scenePath;
            info.Add(metaLabel);

            row.Add(info);

            VisualElement actions = new VisualElement();
            actions.AddToClassList("recent-row__actions");

            if (biome != null)
            {
                // Open Graph is procedural only; a non procedural biome has no graph to open.
                if (biome is IProceduralBiome procedural && procedural.terrainGraph != null)
                {
                    actions.Add(VistaUI.Clickable("Open Graph", () => InvokeAndRecord(() => OpenGraph(procedural))).Chip());
                }
                actions.Add(VistaUI.Clickable("Select", () => InvokeAndRecord(() => Select(biome))).Chip());
            }
            else if (!sceneLoaded)
            {
                actions.Add(VistaUI.Clickable("Load Scene", () => InvokeAndRecord(() => OpenScene(entry))).Chip());
            }

            row.Add(actions);
            return row;
        }

        private static void InvokeAndRecord(System.Action interaction)
        {
            interaction?.Invoke();
            SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.RECENT_BIOMES_INTERACTED);
        }

        /// <summary>
        /// Builds the title line: an icon plus name per part, the graph then the biome, separated by a
        /// dot. The graph uses its asset icon, the biome its component icon. Names are resolved live so a
        /// rename is reflected. The graph leads (the distinguishing part, since the biome name is usually
        /// the default) and shows even for an unloaded scene via the stored GUID; the biome shows only when
        /// its scene is loaded. Falls back to the scene file name when nothing live can be resolved.
        /// </summary>
        private static VisualElement BuildTitle(RecentBiomes.Entry entry, IBiome biome)
        {
            IProceduralBiome procedural = biome as IProceduralBiome;
            string graphName = (procedural != null && procedural.terrainGraph != null)
                ? procedural.terrainGraph.name
                : RecentBiomes.ResolveGraphName(entry);
            string biomeName = biome != null ? biome.gameObject.name : string.Empty;

            VisualElement title = new VisualElement();
            title.AddToClassList("recent-row__title");
            title.tooltip = ComposeTitleText(entry, graphName, biomeName);

            bool hasGraph = !string.IsNullOrEmpty(graphName);
            bool hasBiome = !string.IsNullOrEmpty(biomeName);
            if (hasGraph)
                AddTitlePart(title, ResolveGraphIcon(entry, procedural), graphName);
            if (hasGraph && hasBiome)
            {
                Label separator = VistaUI.Label("·").Faded();
                separator.AddToClassList("recent-row__sep");
                title.Add(separator);
            }
            if (hasBiome)
                AddTitlePart(title, ResolveBiomeIcon(biome), biomeName);
            if (!hasGraph && !hasBiome)
                AddTitlePart(title, null, System.IO.Path.GetFileNameWithoutExtension(entry.scenePath));

            return title;
        }

        // One title part: the icon (when there is one) then the bold name.
        private static void AddTitlePart(VisualElement container, Texture icon, string text)
        {
            if (icon != null)
            {
                Image image = new Image { image = icon, scaleMode = ScaleMode.ScaleToFit };
                image.AddToClassList("recent-row__icon");
                container.Add(image);
            }
            Label label = VistaUI.Label(text);
            label.AddToClassList("recent-row__name");
            container.Add(label);
        }

        // The graph's asset icon, from the live biome's graph when loaded, otherwise the stored GUID, so
        // it shows even for an unloaded scene.
        private static Texture ResolveGraphIcon(RecentBiomes.Entry entry, IProceduralBiome procedural)
        {
            string path = (procedural != null && procedural.terrainGraph != null)
                ? AssetDatabase.GetAssetPath(procedural.terrainGraph)
                : (string.IsNullOrEmpty(entry.graphGuid) ? null : AssetDatabase.GUIDToAssetPath(entry.graphGuid));
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.GetCachedIcon(path);
        }

        // The biome's component icon. Only resolvable when its scene is loaded, so it is null otherwise.
        private static Texture ResolveBiomeIcon(IBiome biome)
        {
            UnityEngine.Object obj = biome as UnityEngine.Object;
            return obj != null ? EditorGUIUtility.ObjectContent(obj, obj.GetType()).image : null;
        }

        // The same title as plain text, for the row tooltip.
        private static string ComposeTitleText(RecentBiomes.Entry entry, string graphName, string biomeName)
        {
            bool hasGraph = !string.IsNullOrEmpty(graphName);
            bool hasBiome = !string.IsNullOrEmpty(biomeName);
            if (hasGraph && hasBiome)
                return graphName + "  ·  " + biomeName;
            if (hasGraph)
                return graphName;
            if (hasBiome)
                return biomeName;
            return System.IO.Path.GetFileNameWithoutExtension(entry.scenePath);
        }

        private void OpenGraph(IProceduralBiome biome)
        {
            // The captured biome may have died since the row was built (its scene was unloaded or it was
            // deleted), so re-check before touching it and rebuild the row to match the new state.
            if (!IsAlive(biome))
            {
                Refresh();
                return;
            }
            Select(biome);
            // The biome builds its own input provider, so this works for any procedural biome type
            // (LocalProceduralBiome, RealWorldBiome, ...) without this view referencing the Pro assembly.
            GraphEditorBase.OpenGraph(biome.terrainGraph, biome.CreateGraphInputProvider());
        }

        private void Select(IBiome biome)
        {
            if (!IsAlive(biome))
            {
                Refresh();
                return;
            }
            Selection.activeObject = biome.gameObject;
            EditorGUIUtility.PingObject(biome.gameObject);
        }

        // A biome is a Unity object, so a destroyed one compares equal to null through the fake-null check.
        private static bool IsAlive(IBiome biome)
        {
            return biome as UnityEngine.Object != null;
        }

        private void OpenScene(RecentBiomes.Entry entry)
        {
            if (string.IsNullOrEmpty(entry.scenePath))
                return;
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(entry.scenePath, OpenSceneMode.Single);
                Refresh();
            }
        }
    }
}
#endif
