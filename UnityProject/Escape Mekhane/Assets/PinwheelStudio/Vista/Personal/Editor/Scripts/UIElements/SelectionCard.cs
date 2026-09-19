#if VISTA
using System;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A selectable card: a rounded, bordered box that lightens on hover and darkens on press (via the shared
    /// .clickable look) and can be marked <see cref="selected"/> by its owner for a brighter, highlighted state.
    /// Selection is per card and owner driven, so several cards on the same screen can be selected at once
    /// (multi selection) or the owner can keep a single one selected. The owner fills the inner content by
    /// adding children. The visual rules live in USS under the .selection-card class. General purpose, not only
    /// the wizard.
    /// </summary>
    public class SelectionCard : ClickableElement
    {
        private bool m_selected;

        public SelectionCard() : this(null)
        {
        }

        public SelectionCard(Action onClick) : base(onClick)
        {
            AddToClassList("selection-card");
        }

        /// <summary>
        /// Whether this card shows the highlighted, selected state. Set by the owner, independent of every other
        /// card, so the owner decides whether selection is single or multi.
        /// </summary>
        public bool selected
        {
            get => m_selected;
            set
            {
                if (m_selected == value)
                    return;
                m_selected = value;
                EnableInClassList("selection-card--selected", value);
            }
        }
    }
}
#endif
