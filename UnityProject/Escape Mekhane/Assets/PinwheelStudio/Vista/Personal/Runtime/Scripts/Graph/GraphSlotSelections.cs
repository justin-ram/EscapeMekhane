#if VISTA
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Pinwheel.Vista.Graph
{
    public enum GraphSlotDirection
    {
        Output = 0,
        Input = 1
    }

    [Serializable]
    public sealed class GraphSlotSelections
    {
        private enum SlotKind
        {
            Texture,
            Tree,
            Detail,
            Object
        }

        [Serializable]
        private sealed class Selection
        {
            public GraphSlotDirection direction;
            public SlotKind kind;
            public string name;
            public UnityEngine.Object value;
        }

        [SerializeField]
        private List<Selection> m_selections = new List<Selection>();

        public bool TryGetTexture(GraphSlotDirection direction, string slotName, out TerrainLayer terrainLayer)
        {
            Selection selection = Find(direction, SlotKind.Texture, slotName);
            terrainLayer = selection != null ? selection.value as TerrainLayer : null;
            return selection != null;
        }

        public void SetTexture(GraphSlotDirection direction, string slotName, TerrainLayer terrainLayer)
        {
            SetValue(direction, SlotKind.Texture, slotName, terrainLayer);
        }

        public bool TryGetTree(string slotName, out TreeTemplate treeTemplate)
        {
            Selection selection = Find(GraphSlotDirection.Output, SlotKind.Tree, slotName);
            treeTemplate = selection != null ? selection.value as TreeTemplate : null;
            return selection != null;
        }

        public void SetTree(string slotName, TreeTemplate treeTemplate)
        {
            SetValue(GraphSlotDirection.Output, SlotKind.Tree, slotName, treeTemplate);
        }

        public bool TryGetDetail(string slotName, out DetailTemplate detailTemplate)
        {
            Selection selection = Find(GraphSlotDirection.Output, SlotKind.Detail, slotName);
            detailTemplate = selection != null ? selection.value as DetailTemplate : null;
            return selection != null;
        }

        public void SetDetail(string slotName, DetailTemplate detailTemplate)
        {
            SetValue(GraphSlotDirection.Output, SlotKind.Detail, slotName, detailTemplate);
        }

        public bool TryGetObject(string slotName, out ObjectTemplate objectTemplate)
        {
            Selection selection = Find(GraphSlotDirection.Output, SlotKind.Object, slotName);
            objectTemplate = selection != null ? selection.value as ObjectTemplate : null;
            return selection != null;
        }

        public void SetObject(string slotName, ObjectTemplate objectTemplate)
        {
            SetValue(GraphSlotDirection.Output, SlotKind.Object, slotName, objectTemplate);
        }

        public void ApplyTo(GraphAsset graph)
        {
            if (graph == null)
                return;

            List<TextureSlotOutputNode> textureSlots = graph.GetNodesOfType<TextureSlotOutputNode>();
            for (int i = 0; i < textureSlots.Count; ++i)
            {
                TextureSlotOutputNode node = textureSlots[i];
                if (TryGetTexture(GraphSlotDirection.Output, node.slotName, out TerrainLayer terrainLayer))
                {
                    node.terrainLayer = terrainLayer;
                }
            }

            List<TreeSlotOutputNode> treeSlots = graph.GetNodesOfType<TreeSlotOutputNode>();
            for (int i = 0; i < treeSlots.Count; ++i)
            {
                TreeSlotOutputNode node = treeSlots[i];
                if (TryGetTree(node.slotName, out TreeTemplate treeTemplate))
                {
                    node.treeTemplate = treeTemplate;
                }
            }

            List<DetailDensitySlotOutputNode> detailDensitySlots = graph.GetNodesOfType<DetailDensitySlotOutputNode>();
            for (int i = 0; i < detailDensitySlots.Count; ++i)
            {
                DetailDensitySlotOutputNode node = detailDensitySlots[i];
                if (TryGetDetail(node.slotName, out DetailTemplate detailTemplate))
                {
                    node.detailTemplate = detailTemplate;
                }
            }

            List<DetailInstanceSlotOutputNode> detailInstanceSlots = graph.GetNodesOfType<DetailInstanceSlotOutputNode>();
            for (int i = 0; i < detailInstanceSlots.Count; ++i)
            {
                DetailInstanceSlotOutputNode node = detailInstanceSlots[i];
                if (TryGetDetail(node.slotName, out DetailTemplate detailTemplate))
                {
                    node.detailTemplate = detailTemplate;
                }
            }
        }

        private void SetValue(GraphSlotDirection direction, SlotKind kind, string slotName, UnityEngine.Object value)
        {
            if (m_selections == null)
            {
                m_selections = new List<Selection>();
            }
            Selection selection = Find(direction, kind, slotName);
            if (selection == null)
            {
                selection = new Selection()
                {
                    direction = direction,
                    kind = kind,
                    name = slotName
                };
                m_selections.Add(selection);
            }
            selection.value = value;
        }

        private Selection Find(GraphSlotDirection direction, SlotKind kind, string slotName)
        {
            if (m_selections == null)
                return null;

            for (int i = 0; i < m_selections.Count; ++i)
            {
                Selection selection = m_selections[i];
                if (selection.direction == direction && selection.kind == kind && selection.name == slotName)
                    return selection;
            }
            return null;
        }
    }
}
#endif
