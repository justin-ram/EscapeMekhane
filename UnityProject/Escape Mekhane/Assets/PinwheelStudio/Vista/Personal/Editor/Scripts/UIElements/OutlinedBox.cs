#if VISTA
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A simple outlined container that groups child elements inside a bordered box. Used for "what
    /// happened" summaries after an action, status panels, and similar. Mostly text today (an optional
    /// title plus one or more lines), but it is a plain container, so any element can be added. The
    /// visual rules live in USS under the .outlined-box class, loaded by the control itself.
    /// </summary>
    public class OutlinedBox : VisualElement
    {
        public OutlinedBox()
        {
            AddToClassList("outlined-box");
            ControlsStylesheet.ApplyTo(this);
        }

        public OutlinedBox(string title) : this()
        {
            if (!string.IsNullOrEmpty(title))
            {
                AddTitle(title);
            }
        }

        /// <summary>Add a bold title row.</summary>
        public Label AddTitle(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("outlined-box__title");
            Add(label);
            return label;
        }

        /// <summary>Add a wrapping text line.</summary>
        public Label AddLine(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("outlined-box__line");
            Add(label);
            return label;
        }
    }
}
#endif
