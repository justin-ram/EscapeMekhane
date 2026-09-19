#if VISTA
#if UNITY_2021_2_OR_NEWER
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Graphics;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System;
using UnityEditor.Toolbars;

namespace Pinwheel.VistaEditor.UIElements
{
    [Overlay(typeof(SceneView), "Vista")]
    public class SceneViewOverlay : ToolbarOverlay
    {
        public delegate void PopulateItemHandler(Collector<string> elementIds);
        public static event PopulateItemHandler populateItemCallback;

        public SceneViewOverlay() : base(PopulateItems())
        {
        }

        public override void OnCreated()
        { 
            base.OnCreated();
            EnsureFirstTimeVisible();
        }

        private static string[] PopulateItems()
        {
            Collector<string> elementIds = new Collector<string>();
            elementIds.Add(OpenWizardToolbarButton.ID);
            elementIds.Add(ForceGenerateToolbarButton.ID);
            populateItemCallback?.Invoke(elementIds);
            return elementIds.ToArray();
        }

        private void EnsureFirstTimeVisible()
        {
            EditorSettings settings = EditorSettings.Get();
            if (settings == null || settings.generalSettings.sceneViewOverlayInitialized)
            {
                return;
            }

            displayed = true;
            settings.generalSettings.sceneViewOverlayInitialized = true;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
#endif
