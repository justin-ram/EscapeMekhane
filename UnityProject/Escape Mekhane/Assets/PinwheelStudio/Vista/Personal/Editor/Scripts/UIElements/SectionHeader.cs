#if VISTA
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A section header strip: a title on the left and an actions area on the right, mirroring the wizard
    /// page title strip but for in content sections. The host fills <see cref="Actions"/> with section
    /// level controls (a Manage link, a button, ...).
    /// </summary>
    public class SectionHeader : VisualElement
    {
        /// <summary>Right side container. Add section level actions here.</summary>
        public VisualElement Actions { get; }

        public SectionHeader(string title)
        {
            AddToClassList("section-header");
            ControlsStylesheet.ApplyTo(this);

            Label label = VistaUI.Label(title).H2();
            label.AddToClassList("section-header__title");
            Add(label);

            Actions = new VisualElement();
            Actions.AddToClassList("section-header__actions");
            Add(Actions);
        }
    }
}
#endif
