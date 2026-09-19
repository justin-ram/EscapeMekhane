#if VISTA
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A compact dashboard launcher for committing an action. It owns its label and optional icon so the
    /// visual structure remains consistent wherever the tile is used. A null icon produces the original
    /// text-only layout.
    /// </summary>
    public class ActionTile : ClickableElement
    {
        public ActionTile(string label, Action onClick)
            : this(label, null, onClick)
        {
        }

        public ActionTile(string label, Texture icon, Action onClick, bool opensPage = false)
            : base(onClick)
        {
            AddToClassList("action-tile");

            if (icon != null)
            {
                Image iconElement = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore,
                };
                iconElement.AddToClassList("action-tile__icon");
                Add(iconElement);
            }

            Label labelElement = new Label(label);
            labelElement.AddToClassList("action-tile__label");
            labelElement.pickingMode = PickingMode.Ignore;
            Add(labelElement);

            if (opensPage)
            {
                AddToClassList("action-tile--opens-page");
                Label arrow = new Label("→");
                arrow.AddToClassList("action-tile__arrow");
                arrow.pickingMode = PickingMode.Ignore;
                Add(arrow);
            }
        }
    }
}
#endif
