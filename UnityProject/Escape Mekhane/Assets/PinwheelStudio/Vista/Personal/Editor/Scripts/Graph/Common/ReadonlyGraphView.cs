#if VISTA
using Pinwheel.Vista.Graph;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using GraphViewEdge = UnityEditor.Experimental.GraphView.Edge;

namespace Pinwheel.VistaEditor.Graph
{
    /// <summary>
    /// A non-editable visualization of serialized graph data.
    /// </summary>
    /// <remarks>
    /// This view reads directly from the supplied graph. It does not clone, validate, dirty,
    /// or otherwise modify the graph, and it has no dependency on a graph editor window.
    /// </remarks>
    public class ReadonlyGraphView : GraphView
    {
        private sealed class ReadonlyEdgeConnectorListener : IEdgeConnectorListener
        {
            public void OnDrop(GraphView graphView, GraphViewEdge edge)
            {
            }

            public void OnDropOutsidePort(GraphViewEdge edge, Vector2 position)
            {
            }
        }

        private readonly IEdgeConnectorListener m_edgeConnectorListener;

        public GraphAsset graph { get; private set; }

        public ReadonlyGraphView(GraphAsset graph)
        {
            this.graph = graph;
            m_edgeConnectorListener = new ReadonlyEdgeConnectorListener();

            StyleSheet graphViewStyle = Resources.Load<StyleSheet>("Vista/USS/Graph/GraphView");
            if (graphViewStyle != null)
            {
                styleSheets.Add(graphViewStyle);
            }
            AddToClassList("panel");

            SetupZoom(0.2f, 10f, ContentZoomer.DefaultScaleStep, ContentZoomer.DefaultReferenceScale);
            this.AddManipulator(new ContentDragger());
            RegisterCallback<ContextualMenuPopulateEvent>(
                BlockContextualMenu,
                TrickleDown.TrickleDown);

            focusable = true;
            style.flexGrow = 1;
            style.backgroundColor = new StyleColor(new Color32(36, 36, 36, 255));

            LoadGraph();
            AddFrameAllButton();
        }

        public void FrameGraph()
        {
            FrameAll();
        }

        public void Reload(GraphAsset graph)
        {
            this.graph = graph;
            LoadGraph();
        }

        private void AddFrameAllButton()
        {
            UtilityButton frameAllButton = new UtilityButton
            {
                image = Resources.Load<Texture2D>("Vista/Textures/FrameAll"),
                tooltip = "Frame all graph elements"
            };
            frameAllButton.clicked += FrameGraph;
            frameAllButton.style.position = Position.Absolute;
            frameAllButton.style.left = 6;
            frameAllButton.style.top = 6;
            Add(frameAllButton);
            frameAllButton.BringToFront();
        }

        private void LoadGraph()
        {
            List<GraphElement> existingElements = new List<GraphElement>();
            graphElements.ForEach(existingElements.Add);
            DeleteElements(existingElements);
            if (graph == null)
                return;

            Dictionary<string, GroupView> groupViews = AddGroups(graph.GetGroups());
            Dictionary<SlotRef, Port> ports = AddNodes(graph.GetNodes(), groupViews);
            AddEdges(graph.GetEdges(), ports);
            AddStickyNotes(graph.GetStickyNotes(), groupViews);
            AddStickyImages(graph.GetStickyImages(), groupViews);
        }

        private Dictionary<string, GroupView> AddGroups(List<IGroup> groups)
        {
            Dictionary<string, GroupView> groupViews = new Dictionary<string, GroupView>();
            if (groups == null)
                return groupViews;

            foreach (IGroup group in groups)
            {
                if (group == null)
                    continue;

                GroupView view = new GroupView
                {
                    title = group.title,
                    groupId = group.id,
                    capabilities = (Capabilities)0,
                    pickingMode = PickingMode.Ignore
                };
                view.SetPosition(group.position);
                AddElement(view);
                DisableInteraction(view);
                groupViews[group.id] = view;
            }
            return groupViews;
        }

        private Dictionary<SlotRef, Port> AddNodes(List<INode> nodes, Dictionary<string, GroupView> groupViews)
        {
            Dictionary<SlotRef, Port> portMap = new Dictionary<SlotRef, Port>();
            if (nodes == null)
                return portMap;

            foreach (INode node in nodes)
            {
                if (node == null)
                    continue;

                NodeView view = NodeView.Create(node, m_edgeConnectorListener);
                view.SetPosition(new Rect(node.visualState.position, Vector2.zero));
                view.expanded = !node.visualState.collapsed;
                AddElement(view);

                if (!string.IsNullOrEmpty(node.groupId) &&
                    groupViews.TryGetValue(node.groupId, out GroupView groupView))
                {
                    groupView.AddElement(view);
                }

                view.Query<Port>().ForEach(port =>
                {
                    if (port is PortView portView)
                    {
                        portMap[portView.slotRef] = port;
                    }
                });
                DisableInteraction(view);
            }
            return portMap;
        }

        private void AddEdges(List<IEdge> edges, Dictionary<SlotRef, Port> ports)
        {
            if (edges == null)
                return;

            foreach (IEdge edge in edges)
            {
                if (edge == null ||
                    !ports.TryGetValue(edge.outputSlot, out Port output) ||
                    !ports.TryGetValue(edge.inputSlot, out Port input))
                {
                    continue;
                }

                EdgeView view = output.ConnectTo<EdgeView>(input);
                view.edgeId = edge.id;
                view.output = output;
                view.input = input;
                DisableInteraction(view);
                AddElement(view);
            }
        }

        private void AddStickyNotes(List<IStickyNote> notes, Dictionary<string, GroupView> groupViews)
        {
            if (notes == null)
                return;

            foreach (IStickyNote note in notes)
            {
                if (note == null)
                    continue;

                StickyNoteView view = new StickyNoteView
                {
                    title = note.title,
                    contents = note.contents,
                    fontSize = (StickyNoteFontSize)note.fontSize,
                    theme = (StickyNoteTheme)note.theme,
                    noteId = note.id
                };
                view.SetPosition(note.position);
                AddElement(view);

                if (!string.IsNullOrEmpty(note.groupId) &&
                    groupViews.TryGetValue(note.groupId, out GroupView groupView))
                {
                    groupView.AddElement(view);
                }

                DisableInteraction(view);
            }
        }

        private void AddStickyImages(List<IStickyImage> images, Dictionary<string, GroupView> groupViews)
        {
            if (images == null)
                return;

            foreach (IStickyImage image in images)
            {
                if (image == null)
                    continue;

                StickyImageView view = new StickyImageView
                {
                    imageId = image.id
                };
                view.SetPosition(image.position);
                view.SetImage(AssetDatabase.LoadAssetAtPath<Texture2D>(
                    AssetDatabase.GUIDToAssetPath(image.textureGuid)));
                AddElement(view);

                if (!string.IsNullOrEmpty(image.groupId) &&
                    groupViews.TryGetValue(image.groupId, out GroupView groupView))
                {
                    groupView.AddElement(view);
                }

                DisableInteraction(view);
            }
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            BlockContextualMenu(evt);
        }

        private void BlockContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.PreventDefault();
            evt.StopImmediatePropagation();
        }

        private static void DisableInteraction(VisualElement root)
        {
            root.pickingMode = PickingMode.Ignore;
            if (root is GraphElement rootGraphElement)
            {
                rootGraphElement.capabilities = (Capabilities)0;
            }
            root.Query<VisualElement>().ForEach(element =>
            {
                element.pickingMode = PickingMode.Ignore;
                if (element is GraphElement graphElement)
                {
                    graphElement.capabilities = (Capabilities)0;
                }
            });
        }
    }
}
#endif
