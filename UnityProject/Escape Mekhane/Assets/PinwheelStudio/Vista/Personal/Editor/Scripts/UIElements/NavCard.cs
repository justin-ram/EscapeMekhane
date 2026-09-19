#if VISTA
using System;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A full width clickable card for navigation: it opens another page or destination. For committing an
    /// action (do something and stay), use <see cref="ClickableElement"/> instead. The card shows a bold
    /// heading, an optional description, and a right arrow that appears on hover to signal "goes somewhere".
    /// The visual rules live in USS under the .nav-card class. General purpose, not only the wizard.
    /// </summary>
    public class NavCard : ClickableElement
    {
        public NavCard(string heading, string description, Action onClick) : base(onClick)
        {
            AddToClassList("nav-card");

            VisualElement body = new VisualElement();
            body.AddToClassList("nav-card__body");
            Add(body);

            Label headingLabel = new Label(heading);
            headingLabel.AddToClassList("nav-card__heading");
            body.Add(headingLabel);

            if (!string.IsNullOrEmpty(description))
            {
                Label descriptionLabel = new Label(description);
                descriptionLabel.AddToClassList("nav-card__desc");
                body.Add(descriptionLabel);
            }

            // Revealed on hover via USS; signals the card navigates rather than acts in place.
            Label arrow = new Label("→");
            arrow.AddToClassList("nav-card__arrow");
            Add(arrow);
        }
    }
}
#endif
