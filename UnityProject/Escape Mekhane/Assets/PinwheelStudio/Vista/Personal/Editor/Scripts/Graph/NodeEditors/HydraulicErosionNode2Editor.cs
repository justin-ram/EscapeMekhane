#if VISTA
using Pinwheel.Vista;
using Pinwheel.Vista.Graph;
using Pinwheel.Vista.Graphics;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(HydraulicErosionNode2))]
    public class HydraulicErosionNode2Editor : ImageNodeEditorBase, INeedUpdateNodeVisual
    {
        private static readonly GUIContent GENERAL_HEADER = new GUIContent("General");
        private static readonly GUIContent ITERATION_COUNT = new GUIContent("Iteration", "The number of simulation steps to perform");
        private static readonly GUIContent AUTO_ITERATION_PER_FRAME = new GUIContent("Auto", "Automatically scale steps-per-frame with the output resolution so large bakes stay responsive and never overrun the GPU driver watchdog (recommended, required for stable 4K)");
        private static readonly GUIContent ITERATION_PER_FRAME = new GUIContent("Iteration Per Frame", "The number of steps to perform in a single frame. Ignored while Auto is on");
        private static readonly GUIContent FEATURE_SIZE = new GUIContent("Feature Size", "Size of the smallest erosion feature, in meters. The simulation runs at one pixel per this many meters, then the erosion is upsampled onto the full-resolution source (source micro detail is preserved). Larger = broader drainage and much faster bakes; smaller = finer detail and slower. Type any value, or pick a preset below");
        private static readonly GUIContent FEATURE_SIZE_PRESETS = new GUIContent(" ", "Quick pick a common feature size");
        private static readonly int[] FEATURE_SIZE_VALUES = new int[] { 1, 2, 4, 8, 16, 32 };
        private static readonly string[] FEATURE_SIZE_LABELS = new string[] { "1", "2", "4", "8", "16", "32" };
        private static readonly GUIContent USE_BORDER_FADE = new GUIContent("Use Border Fade", "Fade out erosion toward the edges of the biome so it blends smoothly into the surrounding terrain instead of ending at a hard edge. Turn off to erode uniformly across the whole biome");

        private static readonly GUIContent HYDRAULIC_HEADER = new GUIContent("Hydraulic Erosion");
        private static readonly GUIContent RAIN_RATE = new GUIContent("Rain Rate", "Depth of water poured into the system each iteration, in millimeters");
        private static readonly GUIContent RAIN_OVER_TIME = new GUIContent("Rain Over Time", "A curve that controls the rain intensity over the simulation lifetime");
        private static readonly GUIContent SEDIMENT_CAPACITY = new GUIContent("Sediment Capacity", "The amount of sediment that water can carry");
        private static readonly GUIContent EROSION_RATE = new GUIContent("Erosion Rate", "How aggressively saturated water dissolves soil each iteration, 0-100 (percent of the capacity deficit removed per step)");
        private static readonly GUIContent DEPOSITION_RATE = new GUIContent("Deposition Rate", "How quickly over-saturated water drops soil back to the terrain each iteration, 0-100 (percent of the excess deposited per step)");
        private static readonly GUIContent EVAPORATION_RATE = new GUIContent("Evaporation Rate", "Depth of water removed from the system each iteration, in millimeters");

        private static readonly GUIContent SOIL_OUTPUT_HEADER = new GUIContent("Soil Output");
        private static readonly GUIContent DEPOSIT_RAMP = new GUIContent("Deposit Ramp", "Depth of settled sediment, in centimeters, at which the Soil output reads as fully bare soil. Lower values highlight thinner deposits in valleys and hollows");
        private static readonly GUIContent DRAINAGE_RAMP = new GUIContent("Drainage Ramp", "Depth of scoured rock, in centimeters, at which the Soil output reads as fully bare soil. Higher values keep the mask on the main drainage channels instead of the whole terrain");

        private static readonly GUIContent APPLY_PRESET = new GUIContent("Apply Preset...", "Overwrite the settings below with a curated starting point. Tweak anything you like afterward");

        //A preset is purely an editor convenience: on the user's behalf it writes hardcoded numbers into the node's
        //settings, exactly like clicking through the fields would. No node state records which preset was applied.
        private enum Preset { Macro, Micro }

        public override void OnGUI(INode node)
        {
            HydraulicErosionNode2 n = node as HydraulicErosionNode2;

            EditorGUILayout.Space();
            DrawPresetButton(n);

            EditorGUI.BeginChangeCheck();

            EditorCommon.Header(GENERAL_HEADER);
            int iterationCount = EditorGUILayout.DelayedIntField(ITERATION_COUNT, n.iterationCount);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel(ITERATION_PER_FRAME);
            bool useAutoIterationPerFrame = n.useAutoIterationPerFrame;
            int iterationPerFrame = n.iterationPerFrame;
            if (useAutoIterationPerFrame)
            {
                useAutoIterationPerFrame = EditorCommon.ToggleButton(AUTO_ITERATION_PER_FRAME, useAutoIterationPerFrame);
            }
            else
            {
                iterationPerFrame = EditorGUILayout.DelayedIntField(iterationPerFrame);
                useAutoIterationPerFrame = EditorCommon.ToggleButton(AUTO_ITERATION_PER_FRAME, useAutoIterationPerFrame, GUILayout.Width(50));
            }
            EditorGUILayout.EndHorizontal();

            int featureSize = EditorGUILayout.DelayedIntField(FEATURE_SIZE, n.featureSize);
            EditorGUI.indentLevel += 1;
            featureSize = EditorCommon.ValueSelector(FEATURE_SIZE_PRESETS, featureSize, FEATURE_SIZE_VALUES, FEATURE_SIZE_LABELS, int.MaxValue);
            EditorGUI.indentLevel -= 1;
            bool useBorderFade = EditorGUILayout.Toggle(USE_BORDER_FADE, n.useBorderFade);

            EditorCommon.Header(HYDRAULIC_HEADER);
            float rainRate = EditorGUILayout.DelayedFloatField(RAIN_RATE, n.rainRate);
            AnimationCurve rainOverTime = EditorGUILayout.CurveField(RAIN_OVER_TIME, n.rainOverTime, Color.cyan, new Rect(0, 0, 1, 1));
            float sedimentCapacity = EditorGUILayout.DelayedFloatField(SEDIMENT_CAPACITY, n.sedimentCapacity);
            float erosionRate = EditorGUILayout.DelayedFloatField(EROSION_RATE, n.erosionRate);
            float depositionRate = EditorGUILayout.DelayedFloatField(DEPOSITION_RATE, n.depositionRate);
            float evaporationRate = EditorGUILayout.DelayedFloatField(EVAPORATION_RATE, n.evaporationRate);

            EditorCommon.Header(SOIL_OUTPUT_HEADER);
            float depositRamp = EditorGUILayout.DelayedFloatField(DEPOSIT_RAMP, n.depositRamp);
            float drainageRamp = EditorGUILayout.DelayedFloatField(DRAINAGE_RAMP, n.drainageRamp);

            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(n);
                n.iterationCount = iterationCount;
                n.iterationPerFrame = iterationPerFrame;
                n.useAutoIterationPerFrame = useAutoIterationPerFrame;
                n.featureSize = featureSize;
                n.useBorderFade = useBorderFade;

                n.rainRate = rainRate;
                n.rainOverTime = rainOverTime;
                n.sedimentCapacity = sedimentCapacity;
                n.erosionRate = erosionRate;
                n.depositionRate = depositionRate;
                n.evaporationRate = evaporationRate;

                n.depositRamp = depositRamp;
                n.drainageRamp = drainageRamp;
            }
        }

        //Surface the sim-canvas-budget warning (set during the last bake) as a node badge. The graph editor clears
        //warnings before calling this, so simply re-adding when the field is set gives us a badge that persists while
        //the cap bites and disappears once the artist raises Feature Size or shrinks the biome.
        public void UpdateVisual(INode node, NodeView nv)
        {
            HydraulicErosionNode2 n = node as HydraulicErosionNode2;
            if (!string.IsNullOrEmpty(n.simResolutionWarning))
            {
                nv.AddWarning(n.simResolutionWarning);
            }
        }

        private void DrawPresetButton(HydraulicErosionNode2 n)
        {
            if (GUILayout.Button(APPLY_PRESET))
            {
                GenericMenu menu = new GenericMenu();
                menu.AddItem(new GUIContent("Macro"), false, () => ApplyPreset(n, Preset.Macro));
                menu.AddItem(new GUIContent("Micro"), false, () => ApplyPreset(n, Preset.Micro));
                menu.ShowAsContext();
            }
        }

        //Runs from the deferred menu callback, outside the change-check that auto-regenerates on field edits, so it
        //registers undo and triggers the regenerate itself — the same SetDirty -> ExecuteGraph -> UpdateNodesVisual
        //the field widgets rely on. Values are written through the node's public setters, which clamp.
        private void ApplyPreset(HydraulicErosionNode2 n, Preset preset)
        {
            m_graphEditor.RegisterUndo(n);

            switch (preset)
            {
                case Preset.Macro:
                    n.iterationCount = 500;
                    n.useAutoIterationPerFrame = true;
                    n.featureSize = 8;
                    n.useBorderFade = false;
                    n.rainRate = 10f;
                    n.rainOverTime = AnimationCurve.Linear(0.25f, 1f, 0.75f, 0f);
                    n.sedimentCapacity = 100f;
                    n.erosionRate = 10f;
                    n.depositionRate = 0.3f;
                    n.evaporationRate = 10f;
                    n.depositRamp = 5f;
                    n.drainageRamp = 100f;
                    break;
                case Preset.Micro:
                    n.iterationCount = 200;
                    n.useAutoIterationPerFrame = true;
                    n.featureSize = 2;
                    n.useBorderFade = false;
                    n.rainRate = 10f;
                    n.rainOverTime = AnimationCurve.Linear(0.5f, 1f, 0.85f, 0f);
                    n.sedimentCapacity = 100f;
                    n.erosionRate = 5f;
                    n.depositionRate = 0.5f;
                    n.evaporationRate = 10f;
                    n.depositRamp = 10f;
                    n.drainageRamp = 300f;
                    break;
            }

            EditorUtility.SetDirty(m_graphEditor.clonedGraph);
            m_graphEditor.RequestGraphExecution();
            m_graphEditor.UpdateNodesVisual();
        }
    }
}
#endif
