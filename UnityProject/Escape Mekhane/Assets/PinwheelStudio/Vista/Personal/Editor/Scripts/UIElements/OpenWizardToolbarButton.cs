#if VISTA
#if UNITY_2021_2_OR_NEWER
using Pinwheel.VistaEditor.Wizard;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Pinwheel.VistaEditor.UIElements
{
    [EditorToolbarElement(OpenWizardToolbarButton.ID, typeof(SceneView))]
    public class OpenWizardToolbarButton : EditorToolbarButton
    {
        public const string ID = "vista-open-wizard-button";

        public OpenWizardToolbarButton()
        {
            text = "";
            icon = Resources.Load<Texture2D>("Vista/Textures/VistaIcon");
            tooltip = "Open Vista Wizard";
            clicked += WizardWindow.Open;
        }
    }
}
#endif
#endif
