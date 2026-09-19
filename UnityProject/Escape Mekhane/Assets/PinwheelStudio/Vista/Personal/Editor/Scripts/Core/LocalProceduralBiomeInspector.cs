#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.ExposeProperty;
using Pinwheel.Vista.Graph;
using Pinwheel.VistaEditor.Graph;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Type = System.Type;
using Object = UnityEngine.Object;
using System.Linq;
using Pinwheel.Vista.Geometric;
using UnityEngine.Rendering;

namespace Pinwheel.VistaEditor
{
    [CustomEditor(typeof(LocalProceduralBiome))]
    public class LocalProceduralBiomeInspector : Editor
    {
        public delegate void InjectGUIHandler(LocalProceduralBiomeInspector inspector, LocalProceduralBiome biome);
        public static event InjectGUIHandler onEnableCallback;
        public static event InjectGUIHandler onDisableCallback;
        public static event InjectGUIHandler injectBiomeMaskGUICallback;
        public static event InjectGUIHandler drawExposedPropertiesCallback;

        public delegate void InjectSceneGUIHandler(LocalProceduralBiomeInspector inspector, LocalProceduralBiome biome, SceneView sceneView);
        public static event InjectSceneGUIHandler injectSceneGUICallback;

        public class ExitSceneGUIException : System.Exception { }

        private LocalProceduralBiome m_instance;
        private Editor m_terrainGraphInspector;
        private PolygonalAreaSceneGuiUtils m_polygonalSceneGui;

        internal class Prefs
        {
            public static bool isEditingAnchor;
            public static bool isEditingHexGrid;

            public static bool isInAnchorEditingMode => isEditingAnchor || isEditingHexGrid;

            public static readonly string DEFERRED_UPDATE = "pinwheel.vista.localproceduralbiome.deferredupdate";
            public static bool useDeferredUpdate;

            public static void Load()
            {
                useDeferredUpdate = EditorPrefs.GetBool(DEFERRED_UPDATE, false);
            }

            public static void Save()
            {
                isEditingAnchor = false;
                isEditingHexGrid = false;
                EditorPrefs.SetBool(DEFERRED_UPDATE, useDeferredUpdate);
            }
        }

        private void OnEnable()
        {
            m_instance = target as LocalProceduralBiome;
            m_polygonalSceneGui = new PolygonalAreaSceneGuiUtils(m_instance);
            m_polygonalSceneGui.changed += OnAnchorsChanged;
            SceneView.duringSceneGui += DuringSceneGUI;
            Prefs.Load();

            onEnableCallback?.Invoke(this, m_instance);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGUI;
            m_polygonalSceneGui.changed -= OnAnchorsChanged;
            m_polygonalSceneGui.Dispose();
            Prefs.Save();

            if (m_terrainGraphInspector != null)
            {
                Object.DestroyImmediate(m_terrainGraphInspector);
            }

            onDisableCallback?.Invoke(this, m_instance);
        }

        public override void OnInspectorGUI()
        {
            if (!IsTargetAlive())
            {
                return;
            }

            DrawOrphanedBiomeWarningGUI();
            DrawGeneralGUI();
            DrawGenerationConfigsGUI();
            DrawTextureInputsGUI();
            DrawPositionInputsGUI();
            DrawAnchorsGUI();
            DrawBiomeMaskGUI();
            DrawCachingGUI();
            DrawBlendOptionsGUI();
            DrawActionGUI();
        }

        private void DrawOrphanedBiomeWarningGUI()
        {
            VistaManager vm = m_instance.GetVistaManagerInstance();
            if (vm == null)
            {
                EditorCommon.DrawWarning("This biome must be a child of a Vista Manager instance, otherwise it won't take effect.");
            }
        }

        private class GeneralGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.general";
            public static readonly GUIContent HEADER = new GUIContent("General");
            public static readonly GUIContent ORDER = new GUIContent("Order", "The order of this biome among others, used for biomes sorting");
            public static readonly GUIContent TERRAIN_GRAPH = new GUIContent("Terrain Graph", "The Terrain Graph Asset used for generating this biome");
            public static readonly GUIContent EDIT_GRAPH = new GUIContent("Edit/Sync");

            public static readonly string NULL_GRAPH_WARNING = "You need to assign a Terrain Graph asset.";

            public static readonly string EXPOSED_PROPERTY_ID = "pinwheel.vista.localproceduralbiome.graph.exposedproperties";
            public static readonly GUIContent EXPOSED_PROPERTIES = new GUIContent("Exposed Properties");

            public static readonly GUIContent EXPOSED_PROPERTIES_LOCKED = new GUIContent("Exposed Properties");
            public static readonly string EXPOSED_PROPERTIES_INFO_SINGLE = "This graph exposes 1 property that can be tuned per biome, right here in the inspector, without opening the Graph Editor. Editing exposed properties is available in Pro.";
            public static readonly string EXPOSED_PROPERTIES_INFO_MULTIPLE = "This graph exposes {0} properties that can be tuned per biome, right here in the inspector, without opening the Graph Editor. Editing exposed properties is available in Pro.";

            public static readonly GUIContent EXPOSED_PROPERTIES_LEARN_MORE = new GUIContent("Learn more →");

            // Points at the exposed properties section of the Pro features post, explaining what they are and how
            // a biome uses them. The anchor targets that section, so keep it in sync if the post is restructured.
            public static readonly string EXPOSED_PROPERTIES_INFO_LINK = "https://www.pinwheelstud.io/post/vista-pro-features#viewer-hzglk4367";
        }

        private void DrawGeneralGUI()
        {
            if (EditorCommon.BeginFoldout(GeneralGUI.ID, GeneralGUI.HEADER, null, true))
            {
                GUI.enabled = !Prefs.isEditingAnchor;
                EditorGUI.BeginChangeCheck();
                int order = EditorGUILayout.IntField(GeneralGUI.ORDER, m_instance.order);
                EditorGUILayout.BeginHorizontal();
                TerrainGraph terrainGraph = EditorGUILayout.ObjectField(GeneralGUI.TERRAIN_GRAPH, m_instance.terrainGraph, typeof(TerrainGraph), false) as TerrainGraph;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                    m_instance.order = order;
                    m_instance.terrainGraph = terrainGraph;
                    if (!Prefs.useDeferredUpdate)
                    {
                        MarkChangedAndGenerate();
                    }
                    RecordInspectorInteraction();
                }

                GUI.enabled &= m_instance.terrainGraph != null;
                if (GUILayout.Button(GeneralGUI.EDIT_GRAPH, GUILayout.Width(75)))
                {
                    if (m_instance.terrainGraph != null)
                    {
                        TerrainGenerationConfigs configs = CreateDebugConfig();
                        m_instance.terrainGraph.debugConfigs = configs;

                        GraphEditorBase graphEditor = GraphEditorBase.OpenGraph(m_instance.terrainGraph, new LPBInputProvider(m_instance));
                        TerrainGraph cloneGraph = graphEditor.clonedGraph as TerrainGraph;
                        cloneGraph.debugConfigs = configs;
                        RecordInspectorInteraction();
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;
                if (terrainGraph == null)
                {
                    EditorCommon.DrawWarning(GeneralGUI.NULL_GRAPH_WARNING, true);
                }
                else if (terrainGraph.HasExposedProperties && drawExposedPropertiesCallback != null)
                {
                    Rect box = EditorGUILayout.BeginVertical();
                    RectOffset boxOffset = new RectOffset(18, 1, 0, 0);
                    GUI.Box(boxOffset.Add(box), string.Empty, EditorStyles.helpBox);
                    EditorGUILayout.Space();
                    bool expanded = SessionState.GetBool(GeneralGUI.EXPOSED_PROPERTY_ID, true);
                    expanded = EditorGUILayout.Foldout(expanded, GeneralGUI.EXPOSED_PROPERTIES, true, EditorCommon.Styles.foldoutBold);
                    SessionState.SetBool(GeneralGUI.EXPOSED_PROPERTY_ID, expanded);
                    if (expanded)
                    {
                        EditorGUI.indentLevel += 1;
                        EditorGUI.BeginChangeCheck();
                        drawExposedPropertiesCallback.Invoke(this, m_instance);
                        if (EditorGUI.EndChangeCheck())
                        {
                            RecordInspectorInteraction();
                        }
                        EditorGUI.indentLevel -= 1;
                    }
                    EditorGUILayout.Space();
                    EditorGUILayout.EndVertical();
                }
                else if (terrainGraph.HasExposedProperties)
                {
                    DrawExposedPropertiesInfo(terrainGraph);
                }
            }
            EditorCommon.EndFoldout();
        }

        // The graph carries exposed properties but no handler is registered to render their editors, so the expose
        // property package is not present in this project. List what the graph exposes and which editions can edit
        // it, rather than hiding the section and leaving the author wondering why the template mentions tunable
        // values it cannot find.
        private void DrawExposedPropertiesInfo(TerrainGraph terrainGraph)
        {
            List<PropertyDescriptor> descriptors = terrainGraph.m_exposedProperties;

            Rect box = EditorGUILayout.BeginVertical();
            RectOffset boxOffset = new RectOffset(18, 0, 0, 0);
            GUI.Box(boxOffset.Add(box), string.Empty, EditorStyles.helpBox);
            EditorGUILayout.Space();

            bool expanded = SessionState.GetBool(GeneralGUI.EXPOSED_PROPERTY_ID, false);
            expanded = EditorGUILayout.Foldout(expanded, GeneralGUI.EXPOSED_PROPERTIES_LOCKED, true, EditorCommon.Styles.foldoutBold);
            SessionState.SetBool(GeneralGUI.EXPOSED_PROPERTY_ID, expanded);
            if (expanded)
            {
                //EditorGUI.indentLevel += 1;

                string message = descriptors.Count == 1
                    ? GeneralGUI.EXPOSED_PROPERTIES_INFO_SINGLE
                    : string.Format(GeneralGUI.EXPOSED_PROPERTIES_INFO_MULTIPLE, descriptors.Count);
                EditorGUILayout.LabelField(message, EditorCommon.Styles.infoLabel);
                EditorGUILayout.Space();

                DrawExposedPropertyPreview(descriptors);
                EditorGUILayout.Space();

                if (EditorGUILayout.LinkButton(GeneralGUI.EXPOSED_PROPERTIES_LEARN_MORE))
                {
                    NetUtils.TrackClick("exposed-property-learn-more", UILocation.Inspector_LPB);
                    Application.OpenURL(GeneralGUI.EXPOSED_PROPERTIES_INFO_LINK);
                    RecordInspectorInteraction();
                }

                //EditorGUI.indentLevel -= 1;
            }
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        // A read only listing of what the graph exposes, grouped the way the expose property package groups them.
        // Labels and value types only. The real typed editors stay in that package so there is one implementation
        // of them, and this preview cannot drift from the values it would be showing.
        private static void DrawExposedPropertyPreview(List<PropertyDescriptor> descriptors)
        {
            bool previousEnabled = GUI.enabled;
            GUI.enabled = false;

            IEnumerable<IGrouping<string, PropertyDescriptor>> groups = descriptors.GroupBy(descriptor => descriptor.groupName);
            foreach (IGrouping<string, PropertyDescriptor> group in groups)
            {
                if (!string.IsNullOrEmpty(group.Key))
                {
                    EditorGUILayout.LabelField(group.Key, EditorCommon.Styles.h3);
                    EditorGUI.indentLevel += 1;
                }

                foreach (PropertyDescriptor descriptor in group)
                {
                    string label = !string.IsNullOrEmpty(descriptor.label) ? descriptor.label : descriptor.id.propertyName;
                    EditorGUILayout.LabelField(
                        new GUIContent(label, descriptor.description),
                        new GUIContent(descriptor.propertyType.ToString()));
                }

                if (!string.IsNullOrEmpty(group.Key))
                {
                    EditorGUI.indentLevel -= 1;
                }
            }

            GUI.enabled = previousEnabled;
        }

        TerrainGenerationConfigs CreateDebugConfig()
        {
            float maxHeight = 600;
            VistaManager vm = m_instance.GetVistaManagerInstance();
            if (vm != null)
            {
                maxHeight = vm.terrainMaxHeight;
            }

            Bounds worldBounds = m_instance.worldBounds;
            TerrainGenerationConfigs configs = TerrainGenerationConfigs.Create();
            configs.resolution = m_instance.baseResolution;
            configs.seed = m_instance.seed;
            configs.terrainHeight = maxHeight;
            configs.worldBounds = new Rect(m_instance.space == Space.World ? worldBounds.min.x : 0, m_instance.space == Space.World ? worldBounds.min.z : 0, worldBounds.size.x, worldBounds.size.z);
            m_instance.terrainGraph.debugConfigs = configs;
            return configs;
        }

        private class GenerationConfigsGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.generationconfigs";
            public static readonly GUIContent HEADER = new GUIContent("Generation Configs");
            public static readonly GUIContent SPACE = new GUIContent("Space", "The coordinate for the generation. World space will affect some nodes (noise, etc) depends on the biome position, while Local space will not.");
            public static readonly GUIContent DATA_MASK = new GUIContent("Data Mask", $"Filter out the biome output where unnecessary data will be ignored. For example, if you uncheck {BiomeDataMask.HeightMap} flag, the graph won't output height data even when you have added a {ObjectNames.NicifyVariableName(typeof(HeightOutputNode).Name)}");
            public static readonly GUIContent BASE_RESOLUTION = new GUIContent("Canvas Resolution", "Base resolution for generated textures to inherit from. Final result will depends on the graph.");
            public static readonly GUIContent PPM = new GUIContent("Pixel Per Meter", "The number of pixels to cover 1 meter in world space, calculated based on the Base Resolution and the biome anchors. Higher value means higher quality but uses more VRAM.");
            public static readonly GUIContent SEED = new GUIContent("Seed", "An integer to randomize the result");
            public static readonly GUIContent COLLECT_SCENE_HEIGHT = new GUIContent("Collect Scene Height", $"Should it collect height data from the scene and feed to the graph as input? The input name is {GraphConstants.SCENE_HEIGHT_INPUT_NAME}");

            public static readonly string DATA_MASK_WARNING = "Nothing? Are you sure?";
            public static readonly string SCENE_HEIGHT_WARNING = $"There is no Input Node with the variable name of \"{GraphConstants.SCENE_HEIGHT_INPUT_NAME}\", consider to turn this checkbox off to improve its performance.";

            public static readonly GUIContent EDITION_MESSAGE = new GUIContent("Higher biome canvas resolution in Indie and Pro gives you finer detail per meter across the scene.");
            public static readonly GUIContent COMPARE_EDITIONS = new GUIContent("Compare editions →");
        }

        private void DrawGenerationConfigsGUI()
        {
            if (EditorCommon.BeginFoldout(GenerationConfigsGUI.ID, GenerationConfigsGUI.HEADER, null, true))
            {
                GUI.enabled = !Prefs.isEditingAnchor;
                EditorGUI.BeginChangeCheck();
                Space space = (Space)EditorGUILayout.EnumPopup(GenerationConfigsGUI.SPACE, m_instance.space);
                BiomeDataMask dataMask = (BiomeDataMask)EditorGUILayout.EnumFlagsField(GenerationConfigsGUI.DATA_MASK, m_instance.dataMask);
                if (dataMask == 0)
                {
                    EditorCommon.DrawWarning(GenerationConfigsGUI.DATA_MASK_WARNING, true);
                }
                int seed = EditorCommon.SeedField(GenerationConfigsGUI.SEED, m_instance.seed, m_instance);

                int baseResolution = m_instance.baseResolution;
                GUI.enabled = false;
                Bounds bounds = m_instance.worldBounds;
                float ppm = baseResolution * 1.0f / Mathf.Max(bounds.size.x, bounds.size.z);
                EditorGUILayout.FloatField(GenerationConfigsGUI.PPM, ppm);
                GUI.enabled = !Prefs.isEditingAnchor;
                baseResolution = EditorGUILayout.DelayedIntField(GenerationConfigsGUI.BASE_RESOLUTION, m_instance.baseResolution);
                baseResolution = EditorCommon.TextureResolutionSelector(EditorGUIUtility.TrTextContent(" "), baseResolution);

                if (EditorCommon.IsPersonalEdition())
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PrefixLabel(" ");
                    Rect r = EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.LabelField(GenerationConfigsGUI.EDITION_MESSAGE, EditorCommon.Styles.infoLabel);
                    if (EditorGUILayout.LinkButton(GenerationConfigsGUI.COMPARE_EDITIONS))
                    {
                        NetUtils.TrackClick("compare-edition", UILocation.Inspector_LPB);
                        Application.OpenURL("https://www.pinwheelstud.io/vista#pricing");
                        RecordInspectorInteraction();
                    }
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.EndHorizontal();
                }

                GUI.enabled = !Prefs.isEditingAnchor;

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                    m_instance.space = space;
                    m_instance.dataMask = dataMask;
                    m_instance.baseResolution = baseResolution;
                    m_instance.seed = seed;
                    if (!Prefs.useDeferredUpdate)
                    {
                        MarkChangedAndGenerate();
                    }
                    RecordInspectorInteraction();
                }
                GUI.enabled = true;
            }
            EditorCommon.EndFoldout();
        }

        private class TextureInputsGUI
        {
            public static readonly GUIContent TEXTURE_INPUTS = new GUIContent("Texture Inputs", "Providing textures that can be accessed with Input Node. The input name you typed in here should match with the one in the graph. Make sure to use unique name for each entry.");
            public static readonly string TEXTURE_INPUTS_PROP_NAME = "m_textureInputs";
        }

        private void DrawTextureInputsGUI()
        {
            SerializedProperty textureInputsProp = serializedObject.FindProperty(TextureInputsGUI.TEXTURE_INPUTS_PROP_NAME);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(textureInputsProp, TextureInputsGUI.TEXTURE_INPUTS, true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                serializedObject.ApplyModifiedProperties();
                RecordInspectorInteraction();
            }
            EditorGUILayout.Space();
        }

        private class PositionInputsGUI
        {
            public static readonly GUIContent POSITION_INPUTS = new GUIContent("Position Inputs", "Providing positions that can be accessed with Input Node. The input name you typed in here should match with the one in the graph. Make sure to use unique name for each entry.");
            public static readonly string POSITION_INPUTS_PROP_NAME = "m_positionInputs";
        }

        private void DrawPositionInputsGUI()
        {
            SerializedProperty positionInputsProp = serializedObject.FindProperty(PositionInputsGUI.POSITION_INPUTS_PROP_NAME);
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(positionInputsProp, PositionInputsGUI.POSITION_INPUTS, true);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                serializedObject.ApplyModifiedProperties();
                RecordInspectorInteraction();
            }
            EditorGUILayout.Space();
        }

        private class AnchorsGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.anchors";
            public static readonly GUIContent HEADER = new GUIContent("Anchors");
            public static readonly GUIContent FALLOFF_DIRECTION = new GUIContent("Falloff Direction", "If set to Outer, the total size of the biome expands, otherwise the total size persist.");
            public static readonly GUIContent FALLOFF_DISTANCE = new GUIContent("Falloff Distance", "Determine the size of the biome falloff area");
            public static readonly GUIContent WORLD_BOUNDS = new GUIContent("Bounds", "Boundaries of the biome in world space");
            public static readonly GUIContent SHOW_OVERLAPPED_TILES = new GUIContent("Show Overlapped Tiles", "Highlight the overlapped tiles in the scene view");

            public static readonly GUIContent EDIT_ANCHORS_HELP = new GUIContent(
                "- Use arrow gizmos to move an anchor.\n" +
                "- Shift Click to add a new anchor between the 2 nearest ones.\n" +
                "- Ctrl Click on an anchor to remove it.");
            public static readonly GUIContent EDIT_HEXGRID_HELP = new GUIContent(
                "- Click on the white circle to add an adjacent hexagon.\n" +
                "- Click on the red circle to remove the current hexagon.\n" +
                "For best result, the hexgrid should NOT be hollow.");
            public static readonly GUIContent EDIT_HEXGRID = new GUIContent("Edit Hex Grid");
            public static readonly GUIContent END_EDIT_HEXGRID = new GUIContent("End Editing Hex Grid");
            public static readonly GUIContent HEX_RADIUS = new GUIContent("Hexagon Radius", "Size of a hexagon in the hex-grid");
            public static readonly GUIContent HEX_RADIUS_WARNING = new GUIContent("Maybe hexagon radius is too small?");
            public static readonly GUIContent HEX_ORIENTATION = new GUIContent("Hexagon Orientation", "Orientation of hexagons in the grid");
            public static readonly GUIContent CLEAR_HEXGRID = new GUIContent("Clear Hex Grid");

        }

        private void DrawAnchorsGUI()
        {
            if (EditorCommon.BeginFoldout(AnchorsGUI.ID, AnchorsGUI.HEADER, null, true))
            {
                GUI.enabled = !Prefs.isEditingAnchor;
                EditorGUI.BeginChangeCheck();
                FalloffDirection falloffDirection = (FalloffDirection)EditorGUILayout.EnumPopup(AnchorsGUI.FALLOFF_DIRECTION, m_instance.falloffDirection);
                float falloffDistance = EditorGUILayout.DelayedFloatField(AnchorsGUI.FALLOFF_DISTANCE, m_instance.falloffDistance);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                    m_instance.falloffDirection = falloffDirection;
                    m_instance.falloffDistance = falloffDistance;
                    if (!Prefs.useDeferredUpdate)
                    {
                        MarkChangedAndGenerate();
                    }
                    RecordInspectorInteraction();
                }
                GUI.enabled = true;

                GUI.enabled = false;
                EditorGUILayout.BoundsField(AnchorsGUI.WORLD_BOUNDS, m_instance.worldBounds);
                GUI.enabled = true;
                if (Prefs.isEditingAnchor)
                {
                    EditorGUILayout.LabelField(AnchorsGUI.EDIT_ANCHORS_HELP, EditorCommon.Styles.infoLabel);
                }

                using (new EditorGUI.DisabledScope(Prefs.isEditingHexGrid))
                {
                    PolygonalAreaInspectorUtils.EditAnchorButtons(
                        area: m_instance,
                        isEditing: Prefs.isEditingAnchor,
                        editingChanged: SetAnchorEditing,
                        areaChanged: OnAnchorPresetChanged,
                        prepareCenterizePivot: PrepareCenterizePivot);
                }

                if (Prefs.isEditingHexGrid)
                {
                    EditorGUILayout.LabelField(AnchorsGUI.EDIT_HEXGRID_HELP, EditorCommon.Styles.infoLabel);
                }

                EditorGUILayout.BeginHorizontal();
                EditorCommon.IndentSpace();
                if (Prefs.isEditingHexGrid)
                {
                    Rect r = EditorGUILayout.GetControlRect();
                    //if (GUILayout.Button(AnchorsGUI.END_EDIT_HEXGRID, EditorStyles.miniButtonLeft))
                    if (EditorCommon.ConfirmButton(r, AnchorsGUI.END_EDIT_HEXGRID))
                    {
                        Prefs.isEditingAnchor = false;
                        Prefs.isEditingHexGrid = false;
                        RecordInspectorInteraction();
                    }
                    r = EditorGUILayout.GetControlRect();
                    if (EditorCommon.RejectButton(r, AnchorsGUI.CLEAR_HEXGRID))
                    {
                        ClearHexgridWithUndo();
                        RecordInspectorInteraction();
                    }
                }
                else if (!Prefs.isInAnchorEditingMode)
                {
                    if (GUILayout.Button(AnchorsGUI.EDIT_HEXGRID, EditorStyles.miniButtonLeft))
                    {
                        Prefs.isEditingAnchor = false;
                        Prefs.isEditingHexGrid = true;
                        SceneView.RepaintAll();
                        RecordInspectorInteraction();
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (Prefs.isEditingHexGrid)
                {
                    Rect box = EditorGUI.IndentedRect(EditorGUILayout.BeginVertical());

                    LPBAdditionalData lpbAddData = m_instance.GetComponent<LPBAdditionalData>();
                    if (lpbAddData == null)
                    {
                        lpbAddData = m_instance.gameObject.AddComponent<LPBAdditionalData>();
                    }

                    EditorGUI.BeginChangeCheck();
                    float hexRadius = EditorGUILayout.DelayedFloatField(AnchorsGUI.HEX_RADIUS, lpbAddData.hexagonRadius);
                    if (hexRadius < m_instance.falloffDistance && m_instance.falloffDirection == FalloffDirection.Inner)
                    {
                        EditorCommon.DrawWarning(AnchorsGUI.HEX_RADIUS_WARNING.text, true);
                    }

                    Hexagon2D.Orientation hexOrientation = (Hexagon2D.Orientation)EditorGUILayout.EnumPopup(AnchorsGUI.HEX_ORIENTATION, lpbAddData.hexagonOrientation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(lpbAddData, $"Modify {m_instance.name}");
                        lpbAddData.hexagonRadius = hexRadius;
                        lpbAddData.hexagonOrientation = hexOrientation;
                        UpdateHexGridAnchors(lpbAddData);
                        RecordInspectorInteraction();
                    }
                    EditorGUILayout.EndVertical();
                }

                using (new EditorGUI.DisabledScope(Prefs.isInAnchorEditingMode))
                {
                    PolygonalAreaInspectorUtils.SnapToButtons(
                        area: m_instance,
                        areaChanged: OnAnchorPresetChanged,
                        snapToHexGrid: SnapToHexGrid);
                }
            }
            EditorCommon.EndFoldout();
        }

        private void ClearHexgridWithUndo()
        {
            LPBAdditionalData lpbAddData = m_instance.GetComponent<LPBAdditionalData>();
            if (lpbAddData == null)
            {
                lpbAddData = m_instance.gameObject.AddComponent<LPBAdditionalData>();
            }
            Undo.RecordObject(lpbAddData, "Clear hexgrid");
            lpbAddData.ClearHexagons();
            UpdateHexGridAnchors(lpbAddData);
        }

        private void SnapToHexGrid()
        {
            LPBAdditionalData lpbAddData = m_instance.GetComponent<LPBAdditionalData>();
            if (lpbAddData == null)
            {
                lpbAddData = m_instance.gameObject.AddComponent<LPBAdditionalData>();
            }

            Vector2 p2 = new Vector2(m_instance.transform.localPosition.x, m_instance.transform.localPosition.z);
            Vector2 hexPoint = LPBAdditionalData.FindNearestPointOnHexGrid(p2, lpbAddData.hexagonRadius, lpbAddData.hexagonOrientation);

            Undo.RecordObject(m_instance.transform, "Snap biome to hex grid");
            m_instance.transform.localPosition = new Vector3(hexPoint.x, 0, hexPoint.y);

            if (!Prefs.useDeferredUpdate)
            {
                MarkChangedAndGenerate();
            }
            RecordInspectorInteraction();
        }

        private void SetAnchorEditing(bool isEditing)
        {
            Prefs.isEditingAnchor = isEditing;
            Prefs.isEditingHexGrid = false;
            if (!isEditing && !Prefs.useDeferredUpdate)
            {
                MarkChangedAndGenerate();
            }
            RecordInspectorInteraction();
        }

        private void OnAnchorPresetChanged()
        {
            if (!Prefs.useDeferredUpdate)
            {
                MarkChangedAndGenerate();
            }
            RecordInspectorInteraction();
        }

        private bool PrepareCenterizePivot()
        {
            LPBAdditionalData lpbAddData = m_instance.GetComponent<LPBAdditionalData>();
            if (lpbAddData == null)
            {
                lpbAddData = m_instance.gameObject.AddComponent<LPBAdditionalData>();
            }
            bool willProceed = true;
            if (lpbAddData.GetHexagonTraceCount() > 0)
            {
                if (EditorUtility.DisplayDialog("Confirm", "This action will reset the hexgrid, proceed?", "OK", "Cancel"))
                {
                    willProceed = true;
                }
                else
                {
                    willProceed = false;
                }
            }

            if (willProceed)
            {
                lpbAddData.ClearHexagons();
            }
            return willProceed;
        }

        private class BiomeMaskGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.biomemask";
            public static readonly GUIContent HEADER = new GUIContent("Biome Mask");
            public static readonly GUIContent RESOLUTION = new GUIContent("Resolution", "Size of the biome mask texture. This texture will be used to blend multiple biomes together.");
            public static readonly GUIContent POST_PROCESS_GRAPH = new GUIContent("Post Process", "Post process the biome mask for better blending");
            public static readonly GUIContent EDIT_GRAPH = new GUIContent("Edit/Sync");

            public static readonly string BIOME_MASK_INPUT_WARNING = $"There is no Input Node with the variable name of {GraphConstants.BIOME_MASK_INPUT_NAME}, please add one.";
            public static readonly string BIOME_MASK_OUTPUT_WARNING = $"There is no Output Node with the variable name of {GraphConstants.BIOME_MASK_OUTPUT_NAME}, please add one.";
        }

        private void DrawBiomeMaskGUI()
        {
            if (EditorCommon.BeginFoldout(BiomeMaskGUI.ID, BiomeMaskGUI.HEADER, null, false))
            {
                GUI.enabled = !Prefs.isEditingAnchor;
                EditorGUI.BeginChangeCheck();
                int resolution = EditorGUILayout.DelayedIntField(BiomeMaskGUI.RESOLUTION, m_instance.biomeMaskResolution);
                resolution = EditorCommon.TextureResolutionSelector(EditorGUIUtility.TrTextContent(" "), m_instance.biomeMaskResolution);
                EditorGUILayout.BeginHorizontal();
                BiomeMaskGraph graph = EditorGUILayout.ObjectField(BiomeMaskGUI.POST_PROCESS_GRAPH, m_instance.biomeMaskGraph, typeof(BiomeMaskGraph), false) as BiomeMaskGraph;
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                    m_instance.biomeMaskResolution = resolution;
                    m_instance.biomeMaskGraph = graph;
                    if (!Prefs.useDeferredUpdate)
                    {
                        MarkChangedAndGenerate();
                    }
                    RecordInspectorInteraction();
                }

                GUI.enabled &= m_instance.biomeMaskGraph != null;
                if (GUILayout.Button(BiomeMaskGUI.EDIT_GRAPH, GUILayout.Width(75)))
                {
                    if (m_instance.biomeMaskGraph != null)
                    {
                        TerrainGenerationConfigs configs = CreateDebugConfig();
                        m_instance.biomeMaskGraph.debugConfigs = configs;

                        GraphEditorBase graphEditor = GraphEditorBase.OpenGraph(m_instance.biomeMaskGraph, new LPBInputProvider(m_instance));
                        BiomeMaskGraph cloneGraph = graphEditor.clonedGraph as BiomeMaskGraph;
                        cloneGraph.debugConfigs = configs;
                        RecordInspectorInteraction();
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;
                CheckAndShowBiomeMaskIOWarning();

                injectBiomeMaskGUICallback?.Invoke(this, m_instance);
            }
            EditorCommon.EndFoldout();
        }

        private void CheckAndShowBiomeMaskIOWarning()
        {
            BiomeMaskGraph graph = m_instance.biomeMaskGraph;
            if (graph == null)
                return;
            List<InputNode> inputNodes = graph.GetNodesOfType<InputNode>();
            bool willShowInputWarning = true;
            for (int i = 0; i < inputNodes.Count; ++i)
            {
                if (string.Equals(inputNodes[i].inputName, GraphConstants.BIOME_MASK_INPUT_NAME))
                {
                    willShowInputWarning = false;
                }
            }

            if (willShowInputWarning)
            {
                EditorCommon.DrawWarning(BiomeMaskGUI.BIOME_MASK_INPUT_WARNING, true);
            }

            List<OutputNode> outputNodes = graph.GetNodesOfType<OutputNode>();
            bool willShowOutputWarning = true;
            for (int i = 0; i < outputNodes.Count; ++i)
            {
                if (string.Equals(outputNodes[i].outputName, GraphConstants.BIOME_MASK_OUTPUT_NAME))
                {
                    willShowOutputWarning = false;
                }
            }

            if (willShowOutputWarning)
            {
                EditorCommon.DrawWarning(BiomeMaskGUI.BIOME_MASK_OUTPUT_WARNING, true);
            }
        }

        private class CachingGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.caching";
            public static readonly GUIContent HEADER = new GUIContent("Caching");
            public static readonly GUIContent CLEAN_UP_MODE = new GUIContent("Clean Up Mode", $"Decide when to release the cache data. Choose {LocalProceduralBiome.CleanUpMode.EachIteration} when your graph is still in draft to ensure data is up to date. Choose {LocalProceduralBiome.CleanUpMode.Manually} if you're working with another biome and don't want to re-generate this one every time. Note that the cache is forced to clean up on some event such as scene reloading, object deactivating, etc.");

            public static readonly GUIContent CACHE = new GUIContent("Cache");
            public static Vector2 cacheScrollPos;
        }

        private void DrawCachingGUI()
        {
            if (EditorCommon.BeginFoldout(CachingGUI.ID, CachingGUI.HEADER, null, false))
            {
                GUI.enabled = !Prefs.isEditingAnchor;
                EditorGUI.BeginChangeCheck();
                LocalProceduralBiome.CleanUpMode cleanUpMode = (LocalProceduralBiome.CleanUpMode)EditorGUILayout.EnumPopup(CachingGUI.CLEAN_UP_MODE, m_instance.cleanUpMode);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_instance, $"Modify {m_instance.name}");
                    m_instance.cleanUpMode = cleanUpMode;
                    m_instance.CleanUp();
                    RecordInspectorInteraction();
                }

                BiomeData cachedData = null;
                PropertyInfo propInfo = m_instance.GetType().GetProperty("cachedData", BindingFlags.NonPublic | BindingFlags.Instance);
                if (propInfo != null)
                {
                    cachedData = propInfo.GetValue(m_instance) as BiomeData;
                }

                EditorCommon.Header(CachingGUI.CACHE);
                if (cachedData != null)
                {
                    Rect scrollRect = EditorGUILayout.BeginVertical();
                    EditorCommon.Box(scrollRect);
                    CachingGUI.cacheScrollPos = EditorGUILayout.BeginScrollView(CachingGUI.cacheScrollPos, GUILayout.MaxHeight(188));
                    EditorGUILayout.LabelField("Textures");
                    EditorGUILayout.BeginHorizontal();
                    EditorCommon.IndentSpace();
                    if (cachedData.heightMap != null)
                    {
                        DrawCacheTexture(cachedData.heightMap, "Height Map");
                    }
                    if (cachedData.holeMap != null)
                    {
                        DrawCacheTexture(cachedData.holeMap, "Hole Map");
                    }
                    if (cachedData.meshDensityMap != null)
                    {
                        DrawCacheTexture(cachedData.meshDensityMap, "Mesh Density Map");
                    }
                    if (cachedData.albedoMap != null)
                    {
                        DrawCacheTexture(cachedData.albedoMap, "Albedo Map");
                    }
                    if (cachedData.metallicMap != null)
                    {
                        DrawCacheTexture(cachedData.metallicMap, "Metallic Map");
                    }

                    List<TerrainLayer> layers = new List<TerrainLayer>();
                    List<RenderTexture> layerWeights = new List<RenderTexture>();
                    cachedData.GetLayerWeights(layers, layerWeights);
                    for (int i = 0; i < layers.Count; ++i)
                    {
                        DrawCacheTexture(layerWeights[i], $"Layer Weight: {layers[i].name}");
                    }

                    List<DetailTemplate> detailTemplates = new List<DetailTemplate>();
                    List<RenderTexture> detailDensity = new List<RenderTexture>();
                    cachedData.GetDensityMaps(detailTemplates, detailDensity);
                    for (int i = 0; i < detailTemplates.Count; ++i)
                    {
                        DrawCacheTexture(detailDensity[i], $"Density Map: {detailTemplates[i].name}");
                    }

                    List<string> genericTextureLabel = new List<string>();
                    List<RenderTexture> genericTexture = new List<RenderTexture>();
                    cachedData.GetGenericTextures(genericTextureLabel, genericTexture);
                    for (int i = 0; i < genericTextureLabel.Count; ++i)
                    {
                        DrawCacheTexture(genericTexture[i], $"{genericTextureLabel}");
                    }

                    if (cachedData.biomeMaskMap != null)
                    {
                        DrawCacheTexture(cachedData.biomeMaskMap, "Biome Mask");
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.LabelField("Trees");
                    List<TreeTemplate> treeTemplates = new List<TreeTemplate>();
                    List<ComputeBuffer> treeBuffers = new List<ComputeBuffer>();
                    cachedData.GetTrees(treeTemplates, treeBuffers);
                    if (treeTemplates.Count == 0)
                    {
                        EditorGUILayout.LabelField("None", EditorCommon.Styles.grayMiniLabel);
                    }
                    else
                    {
                        for (int i = 0; i < treeTemplates.Count; ++i)
                        {
                            int instanceCount = treeBuffers[i].count / InstanceSample.SIZE;
                            EditorGUILayout.LabelField($"{treeTemplates[i].name}: {instanceCount} instance{(instanceCount >= 2 ? "s" : "")}", EditorCommon.Styles.grayMiniLabel);
                        }
                    }

                    EditorGUILayout.LabelField("Detail Instances");
                    List<DetailTemplate> detailTemplates_Instance = new List<DetailTemplate>();
                    List<ComputeBuffer> detailBuffer = new List<ComputeBuffer>();
                    cachedData.GetDetailInstances(detailTemplates_Instance, detailBuffer);
                    if (detailTemplates_Instance.Count == 0)
                    {
                        EditorGUILayout.LabelField("None", EditorCommon.Styles.grayMiniLabel);
                    }
                    else
                    {
                        for (int i = 0; i < detailTemplates_Instance.Count; ++i)
                        {
                            int instanceCount = detailBuffer[i].count / InstanceSample.SIZE;
                            EditorGUILayout.LabelField($"{detailTemplates_Instance[i].name}: {instanceCount} instance{(instanceCount >= 2 ? "s" : "")}", EditorCommon.Styles.grayMiniLabel);
                        }
                    }

                    EditorGUILayout.LabelField("Objects");
                    List<ObjectTemplate> objectTemplates = new List<ObjectTemplate>();
                    List<ComputeBuffer> objectBuffers = new List<ComputeBuffer>();
                    cachedData.GetObjects(objectTemplates, objectBuffers);
                    if (objectTemplates.Count == 0)
                    {
                        EditorGUILayout.LabelField("None", EditorCommon.Styles.grayMiniLabel);
                    }
                    else
                    {
                        for (int i = 0; i < objectTemplates.Count; ++i)
                        {
                            int instanceCount = objectBuffers[i].count / InstanceSample.SIZE;
                            EditorGUILayout.LabelField($"{objectTemplates[i].name}: {instanceCount} instance{(instanceCount >= 2 ? "s" : "")}", EditorCommon.Styles.grayMiniLabel);
                        }
                    }

                    EditorGUILayout.LabelField("Generic Buffers");
                    List<string> genericBufferLabels = new List<string>();
                    List<ComputeBuffer> genericBuffers = new List<ComputeBuffer>();
                    cachedData.GetGenericBuffers(genericBufferLabels, genericBuffers);
                    if (genericBufferLabels.Count == 0)
                    {
                        EditorGUILayout.LabelField("None", EditorCommon.Styles.grayMiniLabel);
                    }
                    else
                    {
                        for (int i = 0; i < genericBufferLabels.Count; ++i)
                        {
                            int instanceCount = genericBuffers[i].count / PositionSample.SIZE;
                            EditorGUILayout.LabelField($"{genericBufferLabels[i]}: {instanceCount} instance{(instanceCount >= 2 ? "s" : "")}", EditorCommon.Styles.grayMiniLabel);
                        }
                    }

                    EditorGUILayout.EndScrollView();
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.LabelField("Not available");
                }
                GUI.enabled = true;
            }
            EditorCommon.EndFoldout();
        }

        private void DrawCacheTexture(RenderTexture t, string label)
        {
            Rect r = EditorGUILayout.GetControlRect(GUILayout.Width(128), GUILayout.Height(128 + EditorGUIUtility.singleLineHeight));
            Rect textureRect = new Rect(r.min.x, r.min.y, r.width, r.width);
            EditorGUI.DrawPreviewTexture(textureRect, t);
            using (IndentScope s = new IndentScope(0))
            {
                Rect labelRect = new Rect(r.min.x, r.max.y - EditorGUIUtility.singleLineHeight, r.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(labelRect, new GUIContent(label, label), EditorCommon.Styles.grayMiniLabel);
            }
        }

        private class BlendOptionsGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.blendoptions";
            public static readonly GUIContent HEADER = new GUIContent("Blending");

            public static readonly GUIContent HEIGHT_MAP_BLEND_MODE = new GUIContent(
                "Height Map",
                "Blend mode for the height map.\n" +
                "Let A be the current blend result, B be the height map of this biome:\n" +
                "- Linear: The height map will gradually change from A to B.\n" +
                "- Additive: B will be added to A.\n" +
                "- Subtractive: B will be subtracted from A.\n" +
                "- Min/Max: Take the min/max value of A and B.");

            public static readonly GUIContent MESH_DENSITY_BLEND_MODE = new GUIContent(
                "Mesh Density",
                "Blend mode for the mesh density map (Polaris terrain polygon density).\n" +
                "Let A be the current blend result, B be the mesh density map of this biome.\n" +
                "Replace: gradually change from A to B based on the biome mask.\n" +
                "Add: add B on top of A, stacking density from multiple biomes.\n" +
                "Subtract: reduce density by subtracting B from A.\n" +
                "Max: keep whichever density is higher.\n" +
                "Min: keep whichever density is lower.");

            public static readonly GUIContent HOLE_MAP_BLEND_MODE = new GUIContent(
                "Hole Map",
                "Blend mode for the hole map (1.0 = hole, 0.0 = solid surface).\n" +
                "- Replace: Each biome's hole pattern overwrites the previous result in its coverage area.\n" +
                "- Max: Takes the maximum hole intensity at each pixel, so holes set by any biome are preserved. " +
                "Use this when the same graph is scattered as multiple instances that each punch a single hole.");

            public static readonly GUIContent TEXTURE_BLEND_MODE = new GUIContent(
                "Textures",
                "Blend mode for terrain texture weights.\n" +
                "Replace: texture weights gradually transition from one biome to the next based on the biome mask.\n" +
                "Height Win: textures appear only where this biome wins height blending. This is meaningful with KeepHigher or KeepLower.");

            public static readonly GUIContent POPULATION_BLEND_MODE = new GUIContent(
                "Population",
                "Blend mode for all population outputs: detail density maps, detail instance buffers, tree buffers, object buffers, and generic instance buffers.\n" +
                "Replace: ecosystems transition from one biome to the next. Each biome claims its mask region and fades out any populations it does not own, so one ecosystem dominates per pixel.\n" +
                "Coexist: ecosystems layer on top of each other. A biome only contributes its own populations and leaves all others untouched, so multiple ecosystems can share a pixel.\n" +
                "Height Win: population appears only where this biome wins height blending. This is meaningful with KeepHigher or KeepLower.");

            public static readonly GUIContent USE_TRANSFORM_FOR_HEIGHT_BLEND = new GUIContent(
                EditorGUIUtility.IconContent("Transform Icon").image,
                "Use this biome's Transform Y position and Y scale when blending height. Move the biome up to raise its height range, or scale Y to expand or compress it.");

            public static readonly GUIContent DEFAULT = new GUIContent("Default");
        }

        private void DrawBlendOptionsGUI()
        {
            if (EditorCommon.BeginFoldout(BlendOptionsGUI.ID, BlendOptionsGUI.HEADER, null, false))
            {
                GUI.enabled = !Prefs.isEditingAnchor;
                BiomeBlendOptions options = m_instance.blendOptions;
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.BeginHorizontal();
                options.heightMapBlendMode = (BiomeBlendOptions.HeightBlendMode)EditorGUILayout.EnumPopup(BlendOptionsGUI.HEIGHT_MAP_BLEND_MODE, options.heightMapBlendMode);
                options.useTransformForHeightBlend = EditorCommon.ToggleButton(BlendOptionsGUI.USE_TRANSFORM_FOR_HEIGHT_BLEND, options.useTransformForHeightBlend, GUILayout.Width(40));
                EditorGUILayout.EndHorizontal();
                options.holeMapBlendMode = (BiomeBlendOptions.HoleBlendMode)EditorGUILayout.EnumPopup(BlendOptionsGUI.HOLE_MAP_BLEND_MODE, options.holeMapBlendMode);
                options.meshDensityBlendMode = (BiomeBlendOptions.MeshDensityBlendMode)EditorGUILayout.EnumPopup(BlendOptionsGUI.MESH_DENSITY_BLEND_MODE, options.meshDensityBlendMode);
                options.textureBlendMode = (BiomeBlendOptions.TextureBlendMode)EditorGUILayout.EnumPopup(BlendOptionsGUI.TEXTURE_BLEND_MODE, options.textureBlendMode);
                options.populationBlendMode = (BiomeBlendOptions.PopulationBlendMode)EditorGUILayout.EnumPopup(BlendOptionsGUI.POPULATION_BLEND_MODE, options.populationBlendMode);

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(m_instance, $"Modify ${m_instance.name}");
                    m_instance.blendOptions = options;
                    if (!Prefs.useDeferredUpdate)
                    {
                        MarkChangedAndGenerate();
                    }
                    RecordInspectorInteraction();
                }
                if (EditorCommon.Button(BlendOptionsGUI.DEFAULT))
                {
                    Undo.RecordObject(m_instance, $"Modify ${m_instance.name}");
                    m_instance.blendOptions = BiomeBlendOptions.Default();
                    if (!Prefs.useDeferredUpdate)
                    {
                        MarkChangedAndGenerate();
                    }
                    RecordInspectorInteraction();
                }
                GUI.enabled = true;
            }
            EditorCommon.EndFoldout();
        }

        private class ActionGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.action";
            public static readonly GUIContent HEADER = new GUIContent("Actions");
            public static readonly GUIContent DEFERRED_UPDATE = new GUIContent("Deferred Update", "Turn this on if you want to make several changes to the biome before re-generating. You have to click Force Update.");
            public static readonly GUIContent FORCE_UPDATE = new GUIContent("Force Update");
        }

        private void DrawActionGUI()
        {
            if (EditorCommon.BeginFoldout(ActionGUI.ID, ActionGUI.HEADER, null, true))
            {
                GUI.enabled = !Prefs.isEditingAnchor;
                bool useDeferredUpdate = EditorGUILayout.Toggle(ActionGUI.DEFERRED_UPDATE, Prefs.useDeferredUpdate);
                if (useDeferredUpdate != Prefs.useDeferredUpdate)
                {
                    Prefs.useDeferredUpdate = useDeferredUpdate;
                    RecordInspectorInteraction();
                }
                if (EditorCommon.Button(ActionGUI.FORCE_UPDATE))
                {
                    MarkChangedAndGenerate();
                    if (m_instance.terrainGraph != null && m_instance.GetVistaManagerInstance() != null)
                    {
                        SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.GENERATE_ALL);
                    }
                    RecordInspectorInteraction();
                }
                GUI.enabled = true;
            }
            EditorCommon.EndFoldout();
        }

        private static void RecordInspectorInteraction()
        {
            SuccessfulActionCounter.Record(
                SuccessfulActionCounter.ActionKeys.BIOME_INSPECTOR_INTERACTION);
        }

        private class DebugGUI
        {
            public static readonly string ID = "pinwheel.vista.localproceduralbiome.debug";
            public static readonly GUIContent HEADER = new GUIContent("Debug");
        }

        private class SceneGUI
        {
            public static readonly string SHOW_OVERLAPPED_TILES_KEY = "pinwheel.vista.localproceduralbiome.scenegui.showoverlappedtiles";

            public static readonly float ANCHOR_SIZE = 0.1f;
            public static readonly Color ANCHOR_COLOR = Color.white;
            public static readonly Color SEGMENT_COLOR = Color.red;
            public static readonly Color FALLOFF_COLOR = new Color(SEGMENT_COLOR.r, SEGMENT_COLOR.g, SEGMENT_COLOR.b, 0.5f);
            public static readonly float SEGMENT_WIDTH = 5;
        }

        private void DuringSceneGUI(SceneView sv)
        {
            if (!IsTargetAlive())
            {
                Tools.hidden = false;
                SceneView.duringSceneGui -= DuringSceneGUI;
                return;
            }

            if (!m_instance.gameObject.activeInHierarchy)
                return;

            bool willDrawDefaultGUI = true;
            try
            {
                injectSceneGUICallback?.Invoke(this, m_instance, sv);
            }
            catch (ExitSceneGUIException)
            {
                willDrawDefaultGUI = false;
            }
            if (willDrawDefaultGUI)
            {
                VistaManager manager = m_instance.GetVistaManagerInstance();
                m_polygonalSceneGui.DrawBounds(manager != null ? manager.terrainMaxHeight : 0);
                //DrawOverlappedTilesBounds();
                DrawAnchors(Prefs.isEditingAnchor, 1);
                if (Prefs.isEditingHexGrid)
                {
                    HandleEditingHexGrid();
                }
            }
        }

        private void OnAnchorsChanged()
        {
            if (!Prefs.useDeferredUpdate)
            {
                MarkChangedAndGenerate();
            }
        }

        private bool IsTargetAlive()
        {
            return m_instance != null;
        }

        private void DrawOverlappedTilesBounds()
        {
            VistaManager manager = m_instance.GetComponentInParent<VistaManager>();
            if (manager == null)
                return;
            List<ITile> tiles = manager.GetTiles();
            using (new HandleScope(new Color(0, 1, 1, 0.5f), UnityEngine.Rendering.CompareFunction.LessEqual))
            {
                foreach (ITile t in tiles)
                {
                    Bounds bounds = t.worldBounds;
                    if (m_instance.IsOverlap(bounds))
                    {
                        Handles.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }
        }

        public void DrawAnchors(bool editMode, float alphaMul = 1)
        {
            m_polygonalSceneGui.DrawSceneGUI(editMode, alphaMul);
        }

        private void HandleEditingHexGrid()
        {
            LPBAdditionalData lpbAddData = m_instance.GetComponent<LPBAdditionalData>();
            if (lpbAddData == null)
            {
                lpbAddData = m_instance.gameObject.AddComponent<LPBAdditionalData>();
            }

            List<Hexagon2D> hexagons = lpbAddData.GetHexagons();
            bool hexTilesChanged = false;
            float addHexHandleSize = lpbAddData.hexagonRadius * 0.5f;
            Hexagon2D lastHex = hexagons[hexagons.Count - 1];
            Line2D[] segments = new Line2D[6];
            lastHex.GetSegments(segments);

            Vector3 hexCenter = new Vector3(lastHex.center.x, 0, lastHex.center.y);
            for (int i = 0; i < 6; ++i)
            {
                Line2D s = segments[i];
                Vector3 segmentCenter = new Vector3(s.startPoint.x, 0, s.startPoint.y) * 0.5f + new Vector3(s.endPoint.x, 0, s.endPoint.y) * 0.5f;
                Vector3 adjacentHexCenterWS = m_instance.transform.TransformPoint(2 * segmentCenter - hexCenter);

                Handles.color = Color.white;
                Handles.DrawLine(adjacentHexCenterWS + Vector3.left * addHexHandleSize * 0.5f, adjacentHexCenterWS + Vector3.right * addHexHandleSize * 0.5f, 1);
                Handles.DrawLine(adjacentHexCenterWS + Vector3.back * addHexHandleSize * 0.5f, adjacentHexCenterWS + Vector3.forward * addHexHandleSize * 0.5f, 1);

                if (Handles.Button(adjacentHexCenterWS, Quaternion.Euler(-90, 0, 0), addHexHandleSize, addHexHandleSize, Handles.CircleHandleCap))
                {
                    Undo.RecordObject(lpbAddData, "Add hexagon to LPBAdditionalData");
                    EditorUtility.SetDirty(lpbAddData);
                    lpbAddData.AddHexTrace(i);
                    GUI.changed = true;
                    hexTilesChanged = true;
                }
            }

            if (hexagons.Count > 1)
            {
                Vector3 hexCenterWS = m_instance.transform.TransformPoint(hexCenter);
                Handles.color = Color.red;
                Handles.DrawLine(hexCenterWS + (Vector3.left + Vector3.forward) * addHexHandleSize * 0.5f, hexCenterWS + (Vector3.right + Vector3.back) * addHexHandleSize * 0.5f, 1);
                Handles.DrawLine(hexCenterWS + (Vector3.left + Vector3.back) * addHexHandleSize * 0.5f, hexCenterWS + (Vector3.right + Vector3.forward) * addHexHandleSize * 0.5f, 1);
                if (Handles.Button(hexCenterWS, Quaternion.Euler(-90, 0, 0), addHexHandleSize, addHexHandleSize, Handles.CircleHandleCap))
                {
                    Undo.RecordObject(lpbAddData, "Remove last hexagon from LPBAdditionalData");
                    EditorUtility.SetDirty(lpbAddData);
                    lpbAddData.RemoveLastHexagon();
                    GUI.changed = true;
                    hexTilesChanged = true;
                }
            }

            if (hexTilesChanged)
            {
                UpdateHexGridAnchors(lpbAddData);
            }

            //for (int i = 0; i < hexagons.Count; ++i)
            //{
            //    Vector3 pos = new Vector3(hexagons[i].center.x, 0, hexagons[i].center.y);
            //    Handles.BeginGUI();
            //    Handles.Label(pos, pos.ToString());
            //    Handles.EndGUI();
            //    Handles.DrawSolidDisc(pos, Vector3.up, 5);
            //}

            //for (int x = -10; x < 20; ++x)
            //{
            //    for (int z = -10; z < 20; ++z)
            //    {
            //        Vector2 pos2 = new Vector2(x * 50f, z * 50f);
            //        Vector2 gridPos = LPBAdditionalData.FindNearestPointOnHexGrid(pos2, 100, Hexagon2D.Orientation.Top);
            //        Handles.color = Color.white;
            //        Handles.DrawWireDisc(new Vector3(pos2.x, 0, pos2.y), Vector2.up, 10);
            //        Handles.DrawLine(new Vector3(pos2.x, 0, pos2.y), new Vector3(gridPos.x, 0, gridPos.y));
            //        Handles.color = Color.cyan;
            //        Handles.DrawWireDisc(new Vector3(gridPos.x, 0, gridPos.y), Vector2.up, 20);
            //        Handles.DrawLine(new Vector3(pos2.x, 0, pos2.y), new Vector3(gridPos.x, 0, gridPos.y));
            //    }
            //}
        }

        private void UpdateHexGridAnchors(LPBAdditionalData lpbAddData)
        {
            List<Vector2> contour2D = lpbAddData.GenerateHexContour();
            List<Vector3> anchors = new List<Vector3>();
            for (int i = 0; i < contour2D.Count; ++i)
            {
                Vector2 c = contour2D[i];
                anchors.Add(new Vector3(c.x, 0, c.y));
            }

            Undo.RecordObject(m_instance, "Modify biome anchors");
            m_instance.anchors = anchors.ToArray();
            EditorUtility.SetDirty(m_instance);
            if (!Prefs.useDeferredUpdate)
            {
                MarkChangedAndGenerate();
            }
            RecordInspectorInteraction();
        }

        public void MarkChangedAndGenerate()
        {
            EditorUtility.SetDirty(m_instance);
            if (m_instance.terrainGraph != null)
            {
                m_instance.CleanUp();
                m_instance.NotifyChanged();
            }
        }

        public void ExitSceneGUI()
        {
            throw new ExitSceneGUIException();
        }
    }
}
#endif
