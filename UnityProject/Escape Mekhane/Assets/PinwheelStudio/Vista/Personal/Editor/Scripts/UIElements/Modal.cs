#if VISTA
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// A centered, blocking dialog over a host element. A full cover scrim fades the underlying UI and
    /// intercepts input, with a centered card holding the content. Used for a moment that deserves space
    /// and a clear next step (for example a successful terrain creation with a "Create Biome" hand off),
    /// where a transient <see cref="Snackbar"/> would be too slight.
    ///
    /// Reusable anywhere in the Vista editor. Show it, fill the card, wire the actions to close:
    /// <code>
    /// Modal modal = Modal.Show(host, "Terrain Created");
    /// modal.AddLine("Created 4 tiles.");
    /// modal.AddActions(VistaUI.Button("Close", modal.Close));
    /// </code>
    /// <c>host</c> is a positioned container that fills its window (for example an EditorWindow's
    /// <c>rootVisualElement</c>). Content added via <see cref="VisualElement.Add"/> lands in the card,
    /// since <see cref="contentContainer"/> is the card. It loads Controls.uss itself.
    /// </summary>
    public class Modal : VisualElement
    {
        private const long FADE_MS = 150;

        private readonly VisualElement m_card;
        private bool m_isClosing;
        private bool m_hasClosed;

        public event Action closed;

        // Add/Remove and the VistaUI factories target the card, not the scrim.
        public override VisualElement contentContainer => m_card;

        private Modal(string title)
        {
            // The scrim: fades and blocks the UI beneath. Default picking swallows clicks so nothing under
            // it reacts; stopping propagation here keeps a click on the backdrop from leaking either.
            AddToClassList("vista-modal");
            ControlsStylesheet.ApplyTo(this);
            style.opacity = 0f;
            RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            RegisterCallback<DetachFromPanelEvent>(_ => NotifyClosed());

            m_card = new VisualElement();
            m_card.AddToClassList("vista-modal__card");
            hierarchy.Add(m_card);

            if (!string.IsNullOrEmpty(title))
                AddTitle(title);
        }

        /// <summary>Show a modal over <paramref name="host"/>, with an optional bold title. Returns it so the caller can fill the card and wire actions.</summary>
        public static Modal Show(VisualElement host, string title = null)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            Modal modal = new Modal(title);
            host.Add(modal);

            // Flip opacity next frame so the USS opacity transition has a start value to animate from.
            modal.schedule.Execute(() => modal.style.opacity = 1f).StartingIn(0);
            return modal;
        }

        /// <summary>Add a bold title row to the card.</summary>
        public Label AddTitle(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("vista-modal__title");
            Add(label);
            return label;
        }

        /// <summary>Add a wrapping text line to the card.</summary>
        public Label AddLine(string text)
        {
            Label label = new Label(text);
            label.AddToClassList("vista-modal__line");
            Add(label);
            return label;
        }

        /// <summary>Add a right aligned row of action controls (buttons) at the bottom of the card.</summary>
        public VisualElement AddActions(params VisualElement[] actions)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("vista-modal__actions");
            if (actions != null)
            {
                foreach (VisualElement action in actions)
                {
                    if (action != null)
                        row.Add(action);
                }
            }
            Add(row);
            return row;
        }

        /// <summary>
        /// Add the standard confirmation footer to the card in one call: an optional "Don't show again" toggle
        /// keyed to an EditorPrefs bool on the left, and a dismiss button (which closes the modal) plus an
        /// optional primary call to action on the right. The call to action closes the modal, then runs its
        /// callback. The modal only builds this footer, whether to show the modal at all (versus a lighter
        /// snackbar when the user has opted out) is the caller's decision, checked through
        /// <see cref="IsDontShowAgainSet"/> with the same pref key.
        /// </summary>
        /// <param name="dontShowAgainPrefKey">EditorPrefs bool the toggle reads and writes, or null/empty to omit the toggle.</param>
        /// <param name="callToActionLabel">Primary button label, or null/empty to omit the call to action (dismiss only).</param>
        /// <param name="callToAction">Primary button action, run after the modal closes.</param>
        /// <param name="dismissLabel">Label for the dismiss button, defaults to "Got It".</param>
        public VisualElement AddConfirmationFooter(string dontShowAgainPrefKey = null, string callToActionLabel = null, Action callToAction = null, string dismissLabel = "Got It")
        {
            string dismiss = string.IsNullOrEmpty(dismissLabel) ? "Got It" : dismissLabel;

            List<VisualElement> controls = new List<VisualElement>();
            controls.Add(VistaUI.Clickable(dismiss, Close));
            if (!string.IsNullOrEmpty(callToActionLabel) && callToAction != null)
            {
                Action action = callToAction;
                controls.Add(VistaUI.Spacer(8));
                controls.Add(VistaUI.Button(callToActionLabel, () =>
                {
                    Close();
                    action();
                }));
            }

            // No opt-out toggle: a plain right aligned actions row.
            if (string.IsNullOrEmpty(dontShowAgainPrefKey))
            {
                return AddActions(controls.ToArray());
            }

            RegisterDontShowAgainKey(dontShowAgainPrefKey);

            // With the opt-out: the toggle on the left, the actions on the right.
            VisualElement actions = VistaUI.Row(controls.ToArray());
            actions.style.alignItems = Align.Center;

            VisualElement footer = VistaUI.Row(BuildDontShowAgain(dontShowAgainPrefKey), actions);
            footer.style.alignItems = Align.Center;
            footer.style.justifyContent = Justify.SpaceBetween;
            footer.style.marginTop = 14;
            Add(footer);
            return footer;
        }

        // Every pref key ever passed to AddConfirmationFooter, ';' separated, so reset tooling can find them all
        // without a hardcoded list that rots when a new modal appears. A pref can only be ticked in a session
        // where its footer was built, so registering at build time covers every pref that can possibly be set.
        private const string DONT_SHOW_AGAIN_KEY_INDEX_PREF = "Pinwheel.Vista.Modal.DontShowAgainKeyIndex";

        private static void RegisterDontShowAgainKey(string prefKey)
        {
            string index = EditorPrefs.GetString(DONT_SHOW_AGAIN_KEY_INDEX_PREF, string.Empty);
            List<string> knownKeys = new List<string>(index.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries));
            if (!knownKeys.Contains(prefKey))
            {
                knownKeys.Add(prefKey);
                EditorPrefs.SetString(DONT_SHOW_AGAIN_KEY_INDEX_PREF, string.Join(";", knownKeys));
            }
        }

        /// <summary>
        /// Whether the user ticked "Don't show again" for <paramref name="prefKey"/>, so the caller should show
        /// its lighter alternative (for example a snackbar) instead of the modal. Callers should use this rather
        /// than reading EditorPrefs directly: it also registers the key, so the reset tooling knows about it even
        /// before the footer is ever built.
        /// </summary>
        public static bool IsDontShowAgainSet(string prefKey)
        {
            if (string.IsNullOrEmpty(prefKey))
            {
                return false;
            }
            RegisterDontShowAgainKey(prefKey);
            return EditorPrefs.GetBool(prefKey, false);
        }

        /// <summary>
        /// Delete every "Don't show again" pref any modal has registered, so all opted out modals show again.
        /// </summary>
        public static void ResetAllDontShowAgainStates()
        {
            string index = EditorPrefs.GetString(DONT_SHOW_AGAIN_KEY_INDEX_PREF, string.Empty);
            foreach (string prefKey in index.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                EditorPrefs.DeleteKey(prefKey);
            }
            EditorPrefs.DeleteKey(DONT_SHOW_AGAIN_KEY_INDEX_PREF);
        }

        // A bare Toggle plus our own Label, so the checkbox sits before the text (the native Toggle renders its
        // label on the left of the box). Clicking the label flips the toggle too. Writes the pref on change.
        private VisualElement BuildDontShowAgain(string prefKey)
        {
            Toggle toggle = new Toggle();
            toggle.style.flexGrow = 0;
            toggle.SetValueWithoutNotify(EditorPrefs.GetBool(prefKey, false));
            toggle.RegisterValueChangedCallback(evt => EditorPrefs.SetBool(prefKey, evt.newValue));

            Label label = new Label("Don't show again");
            label.style.marginLeft = 2;
            label.RegisterCallback<PointerDownEvent>(_ => toggle.value = !toggle.value);

            VisualElement row = VistaUI.Row(toggle, label);
            row.style.alignItems = Align.Center;
            return row;
        }

        /// <summary>Fade the modal out and remove it. Safe to call more than once.</summary>
        public void Close()
        {
            if (m_isClosing || m_hasClosed)
                return;

            m_isClosing = true;
            style.opacity = 0f;
            schedule.Execute(RemoveFromHierarchy).StartingIn(FADE_MS);
        }

        private void NotifyClosed()
        {
            if (m_hasClosed)
                return;

            m_hasClosed = true;
            closed?.Invoke();
            closed = null;
        }
    }
}
#endif
