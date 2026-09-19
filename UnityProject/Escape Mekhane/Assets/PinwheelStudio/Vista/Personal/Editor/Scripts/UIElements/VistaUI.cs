#if VISTA
using System;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// Common UI helper library for shared Vista editor UI. Emit an element with <see cref="Label"/> or
    /// <see cref="Clickable"/>, then chain a styling method: <c>VistaUI.Label("Quick Actions").H2()</c>.
    /// The styling methods are the UI Toolkit parallel of Polaris GStyles (title, h1..h3, p1, p2), plus
    /// Faded, Link, RoundedOutline, and Chip. Each one loads Controls.uss itself, so the result looks
    /// right in any window.
    /// </summary>
    public static class VistaUI
    {
        // --- Emitters ---

        /// <summary>A plain label, style it by chaining (for example <c>.P1()</c>).</summary>
        public static Label Label(string text = "")
        {
            Label label = new Label(text);
            ControlsStylesheet.ApplyTo(label);
            return label;
        }

        /// <summary>A flat clickable element (no button chrome), style it by chaining (for example <c>.Link()</c>).</summary>
        public static ClickableElement Clickable(string text = "", Action onClick = null)
        {
            ClickableElement element = onClick != null ? new ClickableElement(onClick) : new ClickableElement();
            element.text = text;
            return element;
        }

        /// <summary>A regular editor button with the standard button chrome, as opposed to the flat <see cref="Clickable"/>.</summary>
        public static Button Button(string text = "", Action onClick = null)
        {
            Button button = onClick != null ? new Button(onClick) : new Button();
            button.text = text;
            return button;
        }

        /// <summary>A small "ⓘ" info glyph, typically placed in a header's actions area; the tooltip carries the explanation shown on hover.</summary>
        public static Label InfoBadge(string tooltip)
        {
            Label badge = Label("ⓘ");
            badge.tooltip = tooltip;
            return badge;
        }

        /// <summary>A fixed, empty gap for spacing between controls without putting margin on the controls themselves. Sizes both axes (inline style), so the same call works as a vertical gap in a <see cref="Column"/> or a horizontal gap in a <see cref="Row"/>. flex-shrink is off so the gap survives a tight container.</summary>
        public static VisualElement Spacer(float size)
        {
            VisualElement spacer = new VisualElement();
            spacer.style.width = size;
            spacer.style.height = size;
            spacer.style.flexShrink = 0;
            return spacer;
        }

        /// <summary>A horizontal block. Add children to fill it, for example a row of buttons. Optional children can be passed in.</summary>
        public static VisualElement Row(params VisualElement[] children) => Block("row", children);

        /// <summary>A vertical block. Add children to fill it. Optional children can be passed in.</summary>
        public static VisualElement Column(params VisualElement[] children) => Block("column", children);

        private static VisualElement Block(string className, VisualElement[] children)
        {
            VisualElement block = new VisualElement();
            block.AddToClassList(className);
            ControlsStylesheet.ApplyTo(block);
            if (children != null)
            {
                foreach (VisualElement child in children)
                {
                    if (child != null)
                        block.Add(child);
                }
            }
            return block;
        }

        // --- Styling, chainable (the GStyles type scale plus utilities) ---

        public static T Title<T>(this T element) where T : VisualElement => Style(element, "title");
        public static T H1<T>(this T element) where T : VisualElement => Style(element, "h1");
        public static T H2<T>(this T element) where T : VisualElement => Style(element, "h2");
        public static T H3<T>(this T element) where T : VisualElement => Style(element, "h3");
        public static T P1<T>(this T element) where T : VisualElement => Style(element, "p1");
        public static T P2<T>(this T element) where T : VisualElement => Style(element, "p2");

        /// <summary>De-emphasize with faded opacity.</summary>
        public static T Faded<T>(this T element) where T : VisualElement => Style(element, "faded");

        /// <summary>Show as a clickable text link (link color, no button chrome).</summary>
        public static T Link<T>(this T element) where T : VisualElement => Style(element, "link");

        /// <summary>Wrap the element in a thin rounded outline (1px border, rounded corners).</summary>
        public static T RoundedOutline<T>(this T element) where T : VisualElement => Style(element, "rounded-outline");

        /// <summary>Show as a pill chip, a border with fully rounded ends. On a <see cref="Clickable"/> it acts as a button (the clickable supplies the hover and press feedback); on a <see cref="Label"/> it reads as a static tag.</summary>
        public static T Chip<T>(this T element) where T : VisualElement => Style(element, "chip");

        /// <summary>Style a <see cref="Button"/> as a page's main action: bold and taller, sized to sit in a title strip. Constrained to <see cref="Button"/>, so it cannot be applied to arbitrary elements. Use on the single primary call to action of a page or section.</summary>
        public static T Primary<T>(this T element) where T : Button => Style(element, "primary");

        private static T Style<T>(T element, string className) where T : VisualElement
        {
            element.AddToClassList(className);
            ControlsStylesheet.ApplyTo(element);
            return element;
        }
    }
}
#endif
