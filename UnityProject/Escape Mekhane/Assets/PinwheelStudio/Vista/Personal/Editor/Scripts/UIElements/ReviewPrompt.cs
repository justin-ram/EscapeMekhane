#if VISTA
using System;
using Pinwheel.Vista;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// Shared floating review prompt for Vista editor windows.
    /// </summary>
    public sealed class ReviewPrompt : VisualElement
    {
        private const string MESSAGE = "Finding Vista useful? A quick review helps other creators and guides what we improve next.";

        private UILocation m_location;

        internal event Action reviewNowClicked;
        internal event Action laterClicked;

        public ClickableElement reviewNowButton { get; }
        public ClickableElement laterButton { get; }

        internal ReviewPrompt()
        {
            name = "review-prompt";
            AddToClassList("review-prompt");

            StyleSheet styleSheet = Resources.Load<StyleSheet>("Vista/USS/ReviewPrompt");
            if (styleSheet != null)
            {
                styleSheets.Add(styleSheet);
            }

            VisualElement card = new VisualElement() { name = "review-prompt-card" };
            card.AddToClassList("review-prompt__card");
            Add(card);

            Label message = VistaUI.Label(MESSAGE).P1();
            message.AddToClassList("review-prompt__message");
            card.Add(message);

            VisualElement actions = VistaUI.Row();
            actions.AddToClassList("review-prompt__actions");
            card.Add(actions);

            laterButton = VistaUI.Clickable("Later", OnLaterClicked).Faded();
            laterButton.name = "review-later-button";
            actions.Add(laterButton);

            reviewNowButton = VistaUI.Clickable("Leave a Review", OnReviewNowClicked).Chip();
            reviewNowButton.name = "review-now-button";
            actions.Add(reviewNowButton);


        }

        internal void SetTrackingLocation(UILocation location)
        {
            m_location = location;
        }

        private void OnLaterClicked()
        {
            NetUtils.TrackClick("later", m_location);
            laterClicked?.Invoke();
        }

        private void OnReviewNowClicked()
        {
            NetUtils.TrackClick("review-now", m_location);
            reviewNowClicked?.Invoke();
            Application.OpenURL($"{Links.STORE_PAGE}#reviews");
        }

        public void SetShown(bool shown)
        {
            style.display = shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
#endif
