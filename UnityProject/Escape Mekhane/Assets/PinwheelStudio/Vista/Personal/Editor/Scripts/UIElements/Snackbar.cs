#if VISTA
using System;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>The kind of a <see cref="Snackbar"/> message, which picks its accent color.</summary>
    public enum SnackbarType
    {
        Info,
        Success,
        Warning,
        Error,
    }

    /// <summary>
    /// A transient, Android style status bar anchored to the bottom of a host element. Used for quick
    /// pass/fail/status feedback after an action (for example a failed terrain creation), not for content
    /// that must be acted on. It fades in on show, stays for a short duration, then fades out and removes
    /// itself. Color follows the <see cref="SnackbarType"/>.
    ///
    /// Reusable anywhere in the Vista editor: <c>Snackbar.Show(host, "message", SnackbarType.Error)</c>,
    /// where <c>host</c> is a positioned container that fills its window (for example an EditorWindow's
    /// <c>rootVisualElement</c>). The snackbar positions itself absolutely against that host, so it does
    /// not disturb the host's layout. It loads Controls.uss itself, so it looks right without host wiring.
    /// </summary>
    public class Snackbar : VisualElement
    {
        // Long enough to read a short line, short enough to stay out of the way. The fade windows are part
        // of this budget, so the bar is fully opaque for most of it.
        private const long DEFAULT_DURATION_MS = 4000;
        private const long FADE_MS = 180;

        private readonly VisualElement m_bar;
        private readonly Label m_label;
        private IVisualElementScheduledItem m_dismiss;
        private bool m_isDismissing;
        private bool m_hasDismissed;

        public event Action dismissed;

        private Snackbar(string message, SnackbarType type)
        {
            // This element is a transparent, full-width wrapper pinned to the host bottom; it only centers
            // the bar. Absolutely positioned elements ignore align-self, so centering has to come from a
            // normal-flow parent (this) using justify-content, not from the bar positioning itself. Ignore
            // picking on the wrapper so its empty side areas never eat clicks meant for the UI beneath.
            AddToClassList("vista-snackbar");
            ControlsStylesheet.ApplyTo(this);
            pickingMode = PickingMode.Ignore;

            m_bar = new VisualElement();
            m_bar.AddToClassList("vista-snackbar__bar");
            m_bar.AddToClassList(ModifierClass(type));
            Add(m_bar);

            m_label = new Label(message);
            m_label.AddToClassList("vista-snackbar__label");
            m_bar.Add(m_label);

            // Start transparent; the opacity transition in USS animates the fade in once we flip it after
            // attach. Clicking the bar dismisses it early.
            style.opacity = 0f;
            m_bar.RegisterCallback<PointerDownEvent>(_ => Dismiss());
            RegisterCallback<DetachFromPanelEvent>(_ => NotifyDismissed());
        }

        /// <summary>
        /// Show a snackbar in <paramref name="host"/>. Returns the bar in case the caller wants to dismiss
        /// it early. Pass <paramref name="durationMs"/> = 0 to keep it up until dismissed manually.
        /// </summary>
        public static Snackbar Show(VisualElement host, string message, SnackbarType type = SnackbarType.Info, long durationMs = DEFAULT_DURATION_MS)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));

            Snackbar bar = new Snackbar(message, type);
            host.Add(bar);

            // Flip opacity next frame so the transition has a start value to animate from (setting it in
            // the same frame it attaches would jump straight to the end).
            bar.schedule.Execute(() => bar.style.opacity = 1f).StartingIn(0);

            if (durationMs > 0)
                bar.m_dismiss = bar.schedule.Execute(bar.Dismiss).StartingIn(durationMs);

            return bar;
        }

        /// <summary>Fade the snackbar out and remove it. Safe to call more than once.</summary>
        public void Dismiss()
        {
            if (m_isDismissing || m_hasDismissed)
                return;

            m_isDismissing = true;
            m_dismiss?.Pause();
            m_dismiss = null;

            style.opacity = 0f;
            schedule.Execute(RemoveFromHierarchy).StartingIn(FADE_MS);
        }

        private void NotifyDismissed()
        {
            if (m_hasDismissed)
                return;

            m_hasDismissed = true;
            dismissed?.Invoke();
            dismissed = null;
        }

        private static string ModifierClass(SnackbarType type)
        {
            switch (type)
            {
                case SnackbarType.Success: return "vista-snackbar--success";
                case SnackbarType.Warning: return "vista-snackbar--warning";
                case SnackbarType.Error: return "vista-snackbar--error";
                default: return "vista-snackbar--info";
            }
        }
    }
}
#endif
