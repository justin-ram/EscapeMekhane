#if VISTA
using Pinwheel.Vista;
using Pinwheel.VistaEditor.UIElements;
using UnityEngine.UIElements;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// Introduces Terrain Seal before creating one, then points users to the shorter Hierarchy path for
    /// subsequent creation.
    /// </summary>
    public class CreateSealPage : HubPage
    {
        public override string title => "Create Terrain Seal";

        protected override HubNavId HighlightedNav => HubNavId.CreateInScene;

        protected override void BuildContent(VisualElement content)
        {
            content.Add(VistaUI.Label(
                "A Seal protects a region so you can edit it with any manual terrain tool without losing that work the next time Vista regenerates.").P1());

            content.Add(new SectionHeader("How it works"));
            content.Add(VistaUI.Label(
                "Place a Seal around the region you want to work on, then sculpt, paint textures, add trees, edit details, or use any other terrain tool you prefer.").P1());
            content.Add(VistaUI.Label(
                "When you regenerate, the sealed region stays as you left it while the surrounding world updates. There is nothing to capture or refresh manually. Keep all work you want to preserve fully inside the polygon because the Seal has a hard edge.").P1());

            content.Add(new SectionHeader("Alternative creation path"));
            content.Add(VistaUI.Label(
                "Right click in the Hierarchy and choose 3D Object > Vista > Terrain Seal.").P1());
        }

        protected override void BuildTitleStripActions(VisualElement container)
        {
            container.Add(VistaUI.Button("Create", CreateSeal).Primary());
        }

        private void CreateSeal()
        {
            TerrainSealEditorUtility.Create();
            SuccessfulActionCounter.Record(SuccessfulActionCounter.ActionKeys.TERRAIN_SEAL_CREATED);
            Snackbar.Show(
                host.rootVisualElement,
                "Terrain Seal created.",
                SnackbarType.Success);
        }
    }
}
#endif
