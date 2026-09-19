#if VISTA
using Pinwheel.VistaEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// A short list of remote news items on the Home dashboard. Each item is a clickable card with a title,
    /// an optional tag chip, and a summary; clicking opens its URL. Display only, call <see cref="Refresh"/>
    /// to rebuild from <see cref="NewsService"/>. The hosting page owns the section header and decides
    /// whether to show this view at all, based on whether there are any items.
    /// </summary>
    public class NewsView : VisualElement
    {
        public NewsView()
        {
            AddToClassList("news-list");
        }

        public void Refresh()
        {
            Clear();
            foreach (NewsService.Item item in NewsService.Get())
            {
                Add(BuildItem(item));
            }
        }

        private VisualElement BuildItem(NewsService.Item item)
        {
            ClickableElement card = new ClickableElement(() => Open(item));
            card.AddToClassList("news-item");
            if (!string.IsNullOrEmpty(item.url))
                card.tooltip = item.url;

            VisualElement header = VistaUI.Row();
            header.AddToClassList("news-item__header");

            Label title = VistaUI.Label(item.title);
            title.AddToClassList("news-item__title");
            header.Add(title);

            // if (!string.IsNullOrEmpty(item.tag))
            // {
            //     Label tag = VistaUI.Label(item.tag).Chip();
            //     tag.AddToClassList("news-item__tag");
            //     header.Add(tag);
            // }

            card.Add(header);

            if (!string.IsNullOrEmpty(item.summary))
            {
                Label summary = VistaUI.Label(item.summary).P2();
                summary.AddToClassList("news-item__summary");
                card.Add(summary);
            }

            return card;
        }

        private static void Open(NewsService.Item item)
        {
            if (!string.IsNullOrEmpty(item.url))
                Application.OpenURL(item.url);
        }
    }
}
#endif
