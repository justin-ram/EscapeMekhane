#if VISTA
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Wizard
{
    public sealed class VistaToolContext
    {
        public WizardWindow window { get; }
        public Object activeObject => Selection.activeObject;
        public GameObject activeGameObject => Selection.activeGameObject;

        public VistaToolContext(WizardWindow window)
        {
            this.window = window;
        }
    }
}
#endif
