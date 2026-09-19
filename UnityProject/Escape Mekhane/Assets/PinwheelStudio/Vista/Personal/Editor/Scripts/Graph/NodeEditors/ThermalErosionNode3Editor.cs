#if VISTA
using Pinwheel.Vista.Graph;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.Graph
{
    [NodeEditor(typeof(ThermalErosionNode3))]
    public class ThermalErosionNode3Editor : ImageNodeEditorBase
    {
        private static readonly GUIContent GENERAL_HEADER = new GUIContent("General");
        private static readonly GUIContent ITERATION_COUNT = new GUIContent("Iteration", "The number of simulation steps to perform. This also acts as the simulation time, more iterations produce older, more developed erosion");
        private static readonly GUIContent AUTO_ITERATION_PER_FRAME = new GUIContent("Auto", "Automatically scale steps-per-frame with the output resolution so large bakes stay responsive and never overrun the GPU driver watchdog (recommended, required for stable 4K)");
        private static readonly GUIContent ITERATION_PER_FRAME = new GUIContent("Iteration Per Frame", "The number of steps to perform in a single frame. Ignored while Auto is on");
        private static readonly GUIContent USE_MULTI_RESOLUTION = new GUIContent("Use Multi Resolution", "Run the simulation at multiple resolution levels for better performance");

        private static readonly GUIContent SIMULATION_HEADER = new GUIContent("Simulation");
        private static readonly GUIContent EROSION_RATE = new GUIContent("Erosion Rate", "The amount of loose soil produced per iteration, in millimeters. Total erosion potential is roughly this value multiplied by iteration count");
        private static readonly GUIContent TALUS_ANGLE = new GUIContent("Talus Angle", "The slope threshold in degrees. Soil breaks out and slides where the surface is steeper than this angle, flatter areas only receive debris");
        private static readonly GUIContent EROSION_OVER_TIME = new GUIContent("Erosion Over Time", "A curve controlling how much new soil is produced over the simulation lifetime. Ramp it down toward the end so the final iterations only keep transporting already loose soil without eroding more");
        private static readonly GUIContent USE_BORDER_FADE = new GUIContent("Use Border Fade", "Fade out weathering toward the edges of the biome so the erosion blends smoothly into the surrounding terrain instead of ending at a hard edge. Turn off to weather uniformly across the whole biome");

        public override void OnGUI(INode node)
        {
            ThermalErosionNode3 n = node as ThermalErosionNode3;
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
            // bool useAutoIterationPerFrame = n.useAutoIterationPerFrame;
            // EditorGUI.BeginDisabledGroup(useAutoIterationPerFrame);
            // int iterationPerFrame = EditorGUILayout.DelayedIntField(ITERATION_PER_FRAME, n.iterationPerFrame);
            // EditorGUI.EndDisabledGroup();
            // useAutoIterationPerFrame = EditorCommon.ToggleButton(AUTO_ITERATION_PER_FRAME, n.useAutoIterationPerFrame, GUILayout.ExpandWidth(true));
            EditorGUILayout.EndHorizontal();

            bool useMultiResolution = EditorGUILayout.Toggle(USE_MULTI_RESOLUTION, n.useMultiResolution);

            EditorCommon.Header(SIMULATION_HEADER);
            float erosionRate = EditorGUILayout.DelayedFloatField(EROSION_RATE, n.erosionRate);
            float talusAngle = EditorGUILayout.DelayedFloatField(TALUS_ANGLE, n.talusAngle);
            AnimationCurve erosionOverTime = EditorGUILayout.CurveField(EROSION_OVER_TIME, n.erosionOverTime, Color.red, new Rect(0, 0, 1, 1));
            bool useBorderFade = EditorGUILayout.Toggle(USE_BORDER_FADE, n.useBorderFade);

            if (EditorGUI.EndChangeCheck())
            {
                m_graphEditor.RegisterUndo(n);
                n.iterationCount = iterationCount;
                n.iterationPerFrame = iterationPerFrame;
                n.useAutoIterationPerFrame = useAutoIterationPerFrame;
                n.useMultiResolution = useMultiResolution;

                n.erosionRate = erosionRate;
                n.talusAngle = talusAngle;
                n.erosionOverTime = erosionOverTime;
                n.useBorderFade = useBorderFade;
            }
        }
    }
}
#endif
