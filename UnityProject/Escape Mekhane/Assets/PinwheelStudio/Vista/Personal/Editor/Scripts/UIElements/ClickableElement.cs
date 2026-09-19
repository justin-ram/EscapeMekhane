#if VISTA
using System;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A Button with the shared "clickable" look: rounded, no default button texture, lightens on hover
    /// and darkens on press. The visual rules live in USS under the .clickable class, in Controls.uss,
    /// which this control loads itself so it works in any window without the host wiring a stylesheet.
    /// General purpose, not tied to the wizard.
    /// </summary>
    public class ClickableElement : Button
    {
        public ClickableElement()
        {
            Init();
        }

        public ClickableElement(Action onClick) : base(onClick)
        {
            Init();
        }

        private void Init()
        {
            AddToClassList("clickable");
            ControlsStylesheet.ApplyTo(this);
        }
    }
}
#endif
