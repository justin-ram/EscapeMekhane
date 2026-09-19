#if VISTA
using UnityEngine;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.UIElements
{
    /// <summary>
    /// Loads and applies the shared Controls.uss stylesheet, so general controls (ClickableElement,
    /// OutlinedBox, and the like) carry their own look wherever they are used, without the host window
    /// having to wire a stylesheet. The sheet is loaded once and cached.
    /// </summary>
    internal static class ControlsStylesheet
    {
        private const string USS_PATH = "Vista/USS/Controls";
        private static StyleSheet s_styleSheet;

        public static void ApplyTo(VisualElement element)
        {
            if (element == null)
                return;
            if (s_styleSheet == null)
            {
                s_styleSheet = Resources.Load<StyleSheet>(USS_PATH);
            }
            if (s_styleSheet != null && !element.styleSheets.Contains(s_styleSheet))
            {
                element.styleSheets.Add(s_styleSheet);
            }
        }
    }
}
#endif
