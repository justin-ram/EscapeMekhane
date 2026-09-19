#if VISTA
using System;
using Pinwheel.VistaEditor.UIElements;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    public class CreatePainterPage : HubPage
    {
        internal static Action createPainter;

        public override string title => "Create Vista Painter";

        protected override HubNavId HighlightedNav => HubNavId.CreateInScene;

        protected override void BuildContent(VisualElement content)
        {
            content.Add(VistaUI.Label(
                "Vista Painter brings the power of procedural effects into terrain painting.").P1());

            content.Add(new SectionHeader("How it works"));
            content.Add(VistaUI.Label(
                "Choose a purpose built Stroke Action and paint it directly onto terrain. Each action uses Vista's graph system to process the area beneath the stroke, bringing procedural geometry, texturing, trees, and grass into an interactive workflow.").P1());
            content.Add(VistaUI.Label(
                "Create your own actions visually, reuse them with different inputs and settings, or combine several actions into a multi pass effect applied with a single stroke.").P1());

            content.Add(new SectionHeader("Preserve manual edits"));
            content.Add(VistaUI.Label(
                "Use a Terrain Seal to preserve painted regions through later regeneration.").P1());

            content.Add(new SectionHeader("Alternative creation path"));
            content.Add(VistaUI.Label(
                "Right click in the Hierarchy and choose 3D Object > Vista > Vista Painter.").P1());
        }

        protected override void BuildTitleStripActions(VisualElement container)
        {
            if (EditorCommon.IsPersonalEdition())
            {
                container.Add(VistaUI.Label("Available in Vista Indie and Pro").Faded());
            }
            else
            {
                Button createButton = VistaUI.Button("Create", CreatePainter).Primary();
                createButton.SetEnabled(createPainter != null);
                container.Add(createButton);
            }
        }

        private void CreatePainter()
        {
            if (createPainter == null)
                return;

            createPainter.Invoke();
            Snackbar.Show(
                host.rootVisualElement,
                "Vista Painter created.",
                SnackbarType.Success);
        }
    }
}
#endif
