#if VISTA
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Graphics;
using Pinwheel.Vista.Graph;

namespace Pinwheel.Vista.Graph 
{
    [NodeMetadata(
        title = "Hydraulic Erosion 2",
        path = "Nature/Hydraulic Erosion 2",
        description ="Simulate realistic hydraulic erosion on the terrain",
        keywords ="hydraulic, water, flow, erode, wear, exposure, outcrop")]
    public class HydraulicErosionNode2 : ImageNodeBase
    {
        public readonly MaskSlot inputHeightSlot = new MaskSlot("Height", SlotDirection.Input, 0);
        public readonly MaskSlot hardnessSlot = new MaskSlot("Hardness", SlotDirection.Input, 1);

        public readonly MaskSlot outputHeightSlot = new MaskSlot("Height", SlotDirection.Output, 100);
        //Soil: where the original green surface has been disturbed, either scoured away by drainage (exposure) or
        //buried under settled sediment (deposit). Both expose the same bare soil, so they share one output.
        public readonly MaskSlot outputSoilSlot = new MaskSlot("Soil", SlotDirection.Output, 101);

        public override bool shouldSplitExecution
        {
            get => true;
            set => base.shouldSplitExecution = value;
        }

        [SerializeField]
        private int m_iterationCount;
        public int iterationCount
        {
            get
            {
                return m_iterationCount;
            }
            set
            {
                m_iterationCount = Mathf.Max(1, value);
            }
        }

        [SerializeField]
        private int m_iterationPerFrame;
        public int iterationPerFrame
        {
            get
            {
                return Mathf.Max(1, m_iterationPerFrame);
            }
            set
            {
                m_iterationPerFrame = Mathf.Max(1, value);
            }
        }

        [SerializeField]
        private bool m_useAutoIterationPerFrame;
        public bool useAutoIterationPerFrame
        {
            get
            {
                return m_useAutoIterationPerFrame;
            }
            set
            {
                m_useAutoIterationPerFrame = value;
            }
        }

        [SerializeField]
        private float m_rainRate;
        public float rainRate
        {
            get
            {
                return m_rainRate;
            }
            set
            {
                m_rainRate = Mathf.Max(0, value);
            }
        }

        [SerializeField]
        private AnimationCurve m_rainOverTime;
        public AnimationCurve rainOverTime
        {
            get
            {
                return m_rainOverTime;
            }
            set
            {
                m_rainOverTime = value;
            }
        }

        [SerializeField]
        private float m_sedimentCapacity;
        public float sedimentCapacity
        {
            get
            {
                return m_sedimentCapacity;
            }
            set
            {
                m_sedimentCapacity = Mathf.Max(0, value);
            }
        }

        [SerializeField]
        private float m_erosionRate;
        public float erosionRate
        {
            get
            {
                return m_erosionRate;
            }
            set
            {
                m_erosionRate = Mathf.Clamp(value, 0, 100);
            }
        }

        [SerializeField]
        private float m_depositionRate;
        public float depositionRate
        {
            get
            {
                return m_depositionRate;
            }
            set
            {
                m_depositionRate = Mathf.Clamp(value, 0, 100);
            }
        }

        [SerializeField]
        private float m_evaporationRate;
        public float evaporationRate
        {
            get
            {
                return m_evaporationRate;
            }
            set
            {
                m_evaporationRate = Mathf.Max(0, value);
            }
        }

        //Simulation feature size, in meters per pixel. The sim runs on a canvas of (worldSize / featureSize) pixels,
        //then the erosion is upsampled onto the full-res source. Larger = coarser canvas = bigger drainage features
        //and faster bakes; smaller = finer detail and slower. It is the size of the smallest resolvable erosion feature.
        [SerializeField]
        private int m_featureSize;
        public int featureSize
        {
            get
            {
                return m_featureSize;
            }
            set
            {
                m_featureSize = Mathf.Max(1, value);
            }
        }

        [SerializeField]
        private bool m_useBorderFade;
        public bool useBorderFade
        {
            get
            {
                return m_useBorderFade;
            }
            set
            {
                m_useBorderFade = value;
            }
        }

        //Depth of settled sediment, in cm, at which the Soil mask reads fully bare over a deposit. Lower highlights
        //thinner deposits. Kept low because deposits are concentrated in valleys and do not bleed onto clean terrain.
        [SerializeField]
        private float m_depositRamp;
        public float depositRamp
        {
            get
            {
                return m_depositRamp;
            }
            set
            {
                m_depositRamp = Mathf.Max(0.01f, value);
            }
        }

        //Depth of scoured rock, in cm, at which the Soil mask reads fully bare over drainage. Kept high because
        //flowing water grazes almost every pixel, so a low value would light up the whole terrain, not just channels.
        [SerializeField]
        private float m_drainageRamp;
        public float drainageRamp
        {
            get
            {
                return m_drainageRamp;
            }
            set
            {
                m_drainageRamp = Mathf.Max(0.01f, value);
            }
        }

        private static ComputeShader s_sourceShader;
        private static ComputeShader sourceShader
        {
            get
            {
                if (s_sourceShader == null)
                {
                    s_sourceShader = Resources.Load<ComputeShader>(COMPUTE_SHADER_NAME);
                }
                return s_sourceShader;
            }
        }

        private static readonly string COMPUTE_SHADER_NAME = "Vista/Shaders/Graph/HydraulicErosion2";
        private static readonly string TEMP_WORLD_DATA = "~HydraulicErosion2_WorldData";
        private static readonly string TEMP_SIM_DATA_0 = "~HydraulicErosion2_SimData0";
        private static readonly string TEMP_SIM_DATA_1 = "~HydraulicErosion2_SimData1";
        private static readonly string TEMP_SIM_DATA_2 = "~HydraulicErosion2_SimData2";
        private static readonly string TEMP_INITIAL_HEIGHT = "~HydraulicErosion2_InitialHeight";

        private static readonly int INPUT_HEIGHT_01 = Shader.PropertyToID("_InputHeight01");
        private static readonly int INPUT_HARDNESS_01 = Shader.PropertyToID("_InputHardness01");
        private static readonly int WORLD_DATA_CM = Shader.PropertyToID("_WorldDataCM");
        private static readonly int SIM_DATA_0 = Shader.PropertyToID("_SimData0");
        private static readonly int SIM_DATA_1 = Shader.PropertyToID("_SimData1");
        private static readonly int SIM_DATA_2 = Shader.PropertyToID("_SimData2");
        private static readonly int INITIAL_HEIGHT_DATA = Shader.PropertyToID("_InitialHeightData");
        private static readonly int OUTPUT_RT_01 = Shader.PropertyToID("_OutputRT01");

        private static readonly int WORLD_DATA_RESOLUTION = Shader.PropertyToID("_WorldDataResolution");
        private static readonly int OUTPUT_RESOLUTION = Shader.PropertyToID("_OutputResolution");
        private static readonly int WORLD_SIZE_CM = Shader.PropertyToID("_WorldSizeCM");
        private static readonly int FLOW_CONSTANT = Shader.PropertyToID("_FlowConstant");
        private static readonly int SEDIMENT_TRANSPORT_CONSTANT = Shader.PropertyToID("_SedimentTransportConstant");

        private static readonly int RAIN_RATE_CM = Shader.PropertyToID("_RainRateCM");
        private static readonly int SEDIMENT_CAPACITY_CM = Shader.PropertyToID("_SedimentCapacityCM");
        private static readonly int EROSION_RATE = Shader.PropertyToID("_ErosionRate");
        private static readonly int DEPOSITION_RATE = Shader.PropertyToID("_DepositionRate");
        private static readonly int EVAPORATION_RATE_CM = Shader.PropertyToID("_EvaporationRateCM");
        private static readonly int DEPOSIT_RAMP_CM = Shader.PropertyToID("_DepositRampCM");
        private static readonly int DRAINAGE_RAMP_CM = Shader.PropertyToID("_DrainageRampCM");

        //All artist rates are per-iteration (no DT in the shader). Uploads convert to the shader's expected unit:
        //rain/evaporation are mm -> cm (x0.1); erosion/deposition are [0,100] -> [0,1] fraction (/100).
        private const float MM_TO_CM = 0.1f;
        private const float RATE_PERCENT_TO_UNIT = 0.01f;
        private static readonly int USE_BORDER_FADE = Shader.PropertyToID("_UseBorderFade");

        private static readonly int KERNEL_INIT_WORLD_DATA = 0;
        private static readonly int KERNEL_RAIN = 1;
        private static readonly int KERNEL_WATER_FLOW_PHASE_1 = 2;
        private static readonly int KERNEL_WATER_FLOW_PHASE_2 = 3;
        private static readonly int KERNEL_EVAPORATION = 4;
        private static readonly int KERNEL_EROSION_DEPOSITION = 5;
        private static readonly int KERNEL_SEDIMENT_TRANSPORT_PHASE_1 = 6;
        private static readonly int KERNEL_SEDIMENT_TRANSPORT_PHASE_2 = 7;
        private static readonly int KERNEL_OUTPUT_HEIGHT = 8;
        private static readonly int KERNEL_OUTPUT_SOIL = 9;

        // Transport substeps per iteration (each is a Phase1+Phase2 dispatch pair). Must stay >= 6 for stability.
        // A/B (2026-07-13): 4/3 looked fine at <=1K but OSCILLATES at 2K+, so reverted to 6. Reason: pipe-length flow
        // is flowFactor = _FlowConstant / cellDistance, and cellDistance shrinks with resolution -> per-substep flow
        // grows at high res and needs more substeps to stay under the CFL limit. 6 is the floor that holds to 4K.
        // (Total flow per iteration is substep-count independent — the flow constant divides by the count.)
        private static readonly int FLOW_SUBSTEP_COUNT = 6;
        private static readonly int SEDIMENT_TRANSPORT_SUBSTEP_COUNT = 6;
        // Thermal (talus) erosion was removed from this node: as a relaxation filter it flattened the source
        // heightmap's micro detail. Chain a ThermalErosionNode3 downstream if talus behavior is wanted.

        // Pipe-length reference (cm). The flow/transport kernels drive flux by true world-space slope
        // (dh / cellDistance, cellDistance = worldWidthCM / simRes) instead of raw dh, so the sim responds to real
        // steepness — consistent across sim resolution AND biome size/aspect. This constant is the cell distance at
        // which the slope-based flow reproduces the old raw-dh flow; it recalibrates the flux magnitude. TUNE visually.
        private static readonly float PIPE_LENGTH_REFERENCE_CM = 50f;

        // The simulation never runs below this resolution, no matter how large Feature Size is set.
        private static readonly int MIN_SIM_RESOLUTION = 32;

        // Hard ceiling on the sim canvas so cost stays bounded as the biome grows. Feature Size sets a meters per
        // pixel target, but a large biome would push worldWidth / featureSize arbitrarily high, so the canvas is
        // capped here (and never above the output). Past the cap the effective feature size grows, see the warning
        // raised in Execute. The flow kernels normalize by true cell distance, so a coarser canvas stays valid.
        private static readonly int MAX_SIM_RESOLUTION = 2048;

        // Set during Execute when the sim canvas is capped below what Feature Size asked for; null otherwise. Read by
        // HydraulicErosionNode2Editor to raise a warning badge on the node. Not serialized: recomputed every execution and
        // cleared the moment the cap no longer bites.
        [System.NonSerialized]
        private string m_simResolutionWarning;
        public string simResolutionWarning
        {
            get
            {
                return m_simResolutionWarning;
            }
        }

        // The badge only reaches the artist inside the graph editor. A graph generated in the scene (template
        // drop-in, runtime, headless) has no badge, so the cap is also logged once. Deduped against the last logged
        // message, and static so several biomes sharing the same capped graph — each a separate clone/execution —
        // and repeated regenerations log at most once per distinct situation. Resets on domain reload, the desired cadence.
        [System.NonSerialized]
        private static string s_lastLoggedSimResolutionWarning;

        public HydraulicErosionNode2() : base()
        {
            //Defaults mirror the Macro preset (see HydraulicErosionNode2Editor.ApplyPreset) so a freshly dropped node
            //bakes a sensible broad-drainage result out of the box. Keep the two in sync if either changes.
            m_iterationCount = 500;
            m_iterationPerFrame = 50; //perf only, ignored while Auto is on; not part of the preset
            m_useAutoIterationPerFrame = true;

            m_rainRate = 10f;           //mm of water per iteration
            m_rainOverTime = AnimationCurve.Linear(0.25f, 1f, 0.75f, 0f); //full rain, then settling ramp
            m_sedimentCapacity = 100f;  //cm of sediment carryable per unit of flow
            m_erosionRate = 10f;        //[0,100] fraction of the capacity deficit dissolved per iteration
            m_depositionRate = 0.3f;    //[0,100] fraction of the excess sediment deposited per iteration
            m_evaporationRate = 10f;    //mm of water removed per iteration

            m_featureSize = 8; //meters per sim pixel; 1 = finest/slowest, higher = bigger drainage/faster
            m_useBorderFade = false;

            m_depositRamp = 5f;    //cm of settled sediment that reads as fully bare soil
            m_drainageRamp = 100f; //cm of scoured rock that reads as fully bare soil
        }

        public override void ExecuteImmediate(GraphContext context)
        {
            Utilities.DrainCoroutine(Execute(context));
        }

        private struct RequiredOutputs
        {
            public bool height;
            public bool soil;
        }

        [System.Serializable]
        private struct SettingsSnapshot
        {
            public int iterationCount;
            public float rainRate;
            public AnimationCurve rainOverTime;
            public float sedimentCapacity;
            public float erosionRate;
            public float depositionRate;
            public float evaporationRate;
            public int featureSize;
            public bool useBorderFade;
            public float depositRamp;
            public float drainageRamp;
        }

        [System.Serializable]
        private struct ArgsSnapshot
        {
            public int graphResolution;
            public int inputResolution;
            public int outputResolution;
            public float boundsX;
            public float boundsY;
            public float boundsZ;
            public float boundsW;
            public float terrainHeight;
            public int seed;
        }

        public override IEnumerator Execute(GraphContext context)
        {
            int graphResolution = context.GetArg(Args.RESOLUTION).intValue;
            SlotRef inputHeightRefLink = context.GetInputLink(m_id, inputHeightSlot.id);
            Texture inputHeightTexture = context.GetTexture(inputHeightRefLink);
            int inputResolution;
            if (inputHeightTexture != null)
            {
                inputResolution = inputHeightTexture.width;
            }
            else
            {
                inputHeightTexture = Texture2D.blackTexture;
                inputResolution = graphResolution;
            }

            SlotRef hardnessRefLink = context.GetInputLink(m_id, hardnessSlot.id);
            Texture hardnessTexture = context.GetTexture(hardnessRefLink);
            if (hardnessTexture == null)
            {
                hardnessTexture = Texture2D.blackTexture;
            }

            int outputResolution = this.CalculateResolution(graphResolution, inputResolution);
            Vector4 bounds = context.GetArg(Args.WORLD_BOUNDS).vectorValue;
            float maxHeight = context.GetArg(Args.TERRAIN_HEIGHT).floatValue;
            SlotRef outputHeightRef = new SlotRef(m_id, outputHeightSlot.id);
            SlotRef outputSoilRef = new SlotRef(m_id, outputSoilSlot.id);
            RequiredOutputs requiredOutputs = GetRequiredOutputs(context, outputHeightRef, outputSoilRef);
            string settingsJson = null;
            string argsJson = null;

            if (inputHeightTexture == Texture2D.blackTexture)
            {
                //no source height means no sim ran, so any prior capped-canvas warning no longer applies
                m_simResolutionWarning = null;
                if (requiredOutputs.height)
                {
                    DataPool.RtDescriptor outputHeightDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                    RenderTexture outputHeightTexture = context.CreateRenderTarget(outputHeightDesc, outputHeightRef);
                    GraphicsUtils.ClearWithZeros(outputHeightTexture);
                }
                if (requiredOutputs.soil)
                {
                    DataPool.RtDescriptor outputSoilDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                    RenderTexture outputSoilTexture = context.CreateRenderTarget(outputSoilDesc, outputSoilRef);
                    GraphicsUtils.ClearWithZeros(outputSoilTexture);
                }
                context.ReleaseReference(inputHeightRefLink);
                context.ReleaseReference(hardnessRefLink);
                yield break;
            }

            if (context.hasCache)
            {
                settingsJson = CreateSettingsJson();
                argsJson = CreateArgsJson(context, graphResolution, inputResolution, outputResolution, bounds, maxHeight);
                if (TryLoadFromCache(context, settingsJson, argsJson, inputHeightTexture, hardnessTexture, requiredOutputs, outputResolution, outputHeightRef, outputSoilRef))
                {
                    context.SetCurrentProgress(1f);
                    context.ReleaseReference(inputHeightRefLink);
                    context.ReleaseReference(hardnessRefLink);
                    yield break;
                }
            }

            Vector3 worldSizeCM = new Vector3(bounds.z, maxHeight, bounds.w) * 100f;

            //Sim canvas resolution from Feature Size (meters per pixel): bigger feature size -> smaller canvas ->
            //bigger drainage + faster. Clamped so it never drops below a floor, never exceeds the output, and never
            //exceeds the sim budget (MAX_SIM_RESOLUTION) so cost stays bounded no matter how large the biome grows.
            float worldWidthMeters = bounds.z;
            int requestedSimResolution = Utilities.MultipleOf8(Mathf.CeilToInt(worldWidthMeters / m_featureSize));
            int maxSimResolution = Mathf.Min(MAX_SIM_RESOLUTION, outputResolution);
            int simResolution = Mathf.Clamp(requestedSimResolution, MIN_SIM_RESOLUTION, maxSimResolution);
            UpdateSimResolutionWarning(requestedSimResolution, simResolution, maxSimResolution, worldWidthMeters);

            //Sim textures are the downsampled canvas (+8 px margin for the stencil halo / LDS tiling).
            DataPool.RtDescriptor worldDataDesc = DataPool.RtDescriptor.Create(simResolution + 8, simResolution + 8, RenderTextureFormat.ARGBFloat);
            RenderTexture worldDataTexture = context.CreateTemporaryRT(worldDataDesc, TEMP_WORLD_DATA);
            GraphicsUtils.ClearWithZeros(worldDataTexture);

            DataPool.RtDescriptor simDataDesc = DataPool.RtDescriptor.Create(worldDataTexture.width, worldDataTexture.height, RenderTextureFormat.ARGBFloat);
            RenderTexture simDataTexture0 = context.CreateTemporaryRT(simDataDesc, TEMP_SIM_DATA_0); //outflowVH
            RenderTexture simDataTexture1 = context.CreateTemporaryRT(simDataDesc, TEMP_SIM_DATA_1); //outflowDiag
            RenderTexture simDataTexture2 = context.CreateTemporaryRT(simDataDesc, TEMP_SIM_DATA_2); //flowVelocity.xy, deposit, exposure
            GraphicsUtils.ClearWithZeros(simDataTexture0);
            GraphicsUtils.ClearWithZeros(simDataTexture1);
            GraphicsUtils.ClearWithZeros(simDataTexture2);

            //Source height sampled at sim res, captured by Init; the output kernels diff against it to get the erosion delta.
            DataPool.RtDescriptor initialHeightDesc = DataPool.RtDescriptor.Create(worldDataTexture.width, worldDataTexture.height, RenderTextureFormat.RFloat);
            RenderTexture initialHeightTexture = context.CreateTemporaryRT(initialHeightDesc, TEMP_INITIAL_HEIGHT);

            ComputeShader shader = Object.Instantiate(sourceShader);
            context.RegisterDestroyLater(shader);
            shader.SetFloat(SEDIMENT_CAPACITY_CM, m_sedimentCapacity);
            shader.SetFloat(EROSION_RATE, m_erosionRate * RATE_PERCENT_TO_UNIT);
            shader.SetFloat(DEPOSITION_RATE, m_depositionRate * RATE_PERCENT_TO_UNIT);
            shader.SetFloat(EVAPORATION_RATE_CM, m_evaporationRate * MM_TO_CM);
            shader.SetFloat(DEPOSIT_RAMP_CM, m_depositRamp);
            shader.SetFloat(DRAINAGE_RAMP_CM, m_drainageRamp);
            shader.SetFloat(USE_BORDER_FADE, m_useBorderFade ? 1f : 0f);
            shader.SetVector(WORLD_SIZE_CM, worldSizeCM);
            shader.SetVector(WORLD_DATA_RESOLUTION, new Vector4(simResolution, simResolution));
            shader.SetVector(OUTPUT_RESOLUTION, new Vector4(outputResolution, outputResolution));
            shader.SetFloat(FLOW_CONSTANT, PIPE_LENGTH_REFERENCE_CM / FLOW_SUBSTEP_COUNT); // /count keeps total flow constant across substep counts
            shader.SetFloat(SEDIMENT_TRANSPORT_CONSTANT, PIPE_LENGTH_REFERENCE_CM / SEDIMENT_TRANSPORT_SUBSTEP_COUNT);

            shader.SetTexture(KERNEL_INIT_WORLD_DATA, INPUT_HEIGHT_01, inputHeightTexture);
            shader.SetTexture(KERNEL_INIT_WORLD_DATA, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_INIT_WORLD_DATA, INITIAL_HEIGHT_DATA, initialHeightTexture);

            shader.SetTexture(KERNEL_RAIN, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_RAIN, SIM_DATA_2, simDataTexture2); //reset flow velocity each iteration

            shader.SetTexture(KERNEL_WATER_FLOW_PHASE_1, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_WATER_FLOW_PHASE_1, SIM_DATA_0, simDataTexture0);
            shader.SetTexture(KERNEL_WATER_FLOW_PHASE_1, SIM_DATA_1, simDataTexture1);

            shader.SetTexture(KERNEL_WATER_FLOW_PHASE_2, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_WATER_FLOW_PHASE_2, SIM_DATA_0, simDataTexture0);
            shader.SetTexture(KERNEL_WATER_FLOW_PHASE_2, SIM_DATA_1, simDataTexture1);
            shader.SetTexture(KERNEL_WATER_FLOW_PHASE_2, SIM_DATA_2, simDataTexture2); //accumulate flowVelocity

            shader.SetTexture(KERNEL_EROSION_DEPOSITION, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_EROSION_DEPOSITION, SIM_DATA_2, simDataTexture2); //read flowVelocity, write deposit + exposure
            shader.SetTexture(KERNEL_EROSION_DEPOSITION, INPUT_HARDNESS_01, hardnessTexture); //erosion mask

            shader.SetTexture(KERNEL_SEDIMENT_TRANSPORT_PHASE_1, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_SEDIMENT_TRANSPORT_PHASE_1, SIM_DATA_0, simDataTexture0);
            shader.SetTexture(KERNEL_SEDIMENT_TRANSPORT_PHASE_1, SIM_DATA_1, simDataTexture1);

            shader.SetTexture(KERNEL_SEDIMENT_TRANSPORT_PHASE_2, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_SEDIMENT_TRANSPORT_PHASE_2, SIM_DATA_0, simDataTexture0);
            shader.SetTexture(KERNEL_SEDIMENT_TRANSPORT_PHASE_2, SIM_DATA_1, simDataTexture1);

            shader.SetTexture(KERNEL_EVAPORATION, WORLD_DATA_CM, worldDataTexture);

            //Output kernels run at full res and upsample the sim canvas; height also reads the full-res + initial source.
            shader.SetTexture(KERNEL_OUTPUT_HEIGHT, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_OUTPUT_HEIGHT, INITIAL_HEIGHT_DATA, initialHeightTexture);
            shader.SetTexture(KERNEL_OUTPUT_HEIGHT, INPUT_HEIGHT_01, inputHeightTexture);
            shader.SetTexture(KERNEL_OUTPUT_SOIL, SIM_DATA_2, simDataTexture2);

            //Height output RT (full res), created up front so the live-preview OutputHeight can write it during the sim.
            //Its delta-combine always includes the full-res source, so early frames just show the source terrain, then erode.
            RenderTexture heightOutputRt = null;
            if (requiredOutputs.height)
            {
                heightOutputRt = context.CreateRenderTarget(DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat), outputHeightRef);
                shader.SetTexture(KERNEL_OUTPUT_HEIGHT, OUTPUT_RT_01, heightOutputRt);
            }

            yield return Simulate(context, shader, m_iterationCount, simResolution, outputResolution, requiredOutputs.height);
            if (context.isCancellationRequested)
            {
                yield break;
            }

            if (requiredOutputs.height)
            {
                //write the finished erosion, upsampled onto the full-res source (heightOutputRt is bound above)
                shader.Dispatch(KERNEL_OUTPUT_HEIGHT, (outputResolution + 7) / 8, 1, (outputResolution + 7) / 8);
            }

            if (requiredOutputs.soil)
            {
                DataPool.RtDescriptor outputSoilDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                RenderTexture outputSoilTexture = context.CreateRenderTarget(outputSoilDesc, outputSoilRef);

                shader.SetTexture(KERNEL_OUTPUT_SOIL, SIM_DATA_2, simDataTexture2);
                shader.SetTexture(KERNEL_OUTPUT_SOIL, OUTPUT_RT_01, outputSoilTexture);
                shader.Dispatch(KERNEL_OUTPUT_SOIL, (outputResolution + 7) / 8, 1, (outputResolution + 7) / 8);
            }

            if (context.hasCache)
            {
                StoreToCache(context, settingsJson, argsJson, inputHeightTexture, hardnessTexture, outputHeightRef, outputSoilRef);
            }

            context.ReleaseReference(inputHeightRefLink);
            context.ReleaseReference(hardnessRefLink);
            context.ReleaseTemporary(TEMP_WORLD_DATA);
            context.ReleaseTemporary(TEMP_SIM_DATA_0);
            context.ReleaseTemporary(TEMP_SIM_DATA_1);
            context.ReleaseTemporary(TEMP_SIM_DATA_2);
            context.ReleaseTemporary(TEMP_INITIAL_HEIGHT);
            yield return null;
        }

        //The sim canvas Feature Size asks for (worldWidth / featureSize) was larger than the applied cap, so the sim
        //runs coarser than requested. Store a warning (with the effective feature size actually baked) for the node
        //editor to surface as a badge, and log it once for the scene-generation case that has no badge; clear the
        //badge when the request fits so it disappears on its own.
        private void UpdateSimResolutionWarning(int requestedSimResolution, int simResolution, int maxSimResolution, float worldWidthMeters)
        {
            if (requestedSimResolution <= simResolution)
            {
                m_simResolutionWarning = null;
                return;
            }

            float effectiveFeatureSize = worldWidthMeters / simResolution;
            m_simResolutionWarning = string.Format(
                "Feature Size {0} m needs a {1} px sim canvas, above the {2} px budget. Baking at {3} px " +
                "(effective feature size {4:0.##} m per pixel). Increase Feature Size, or use a smaller biome, " +
                "to bake at your chosen scale.",
                m_featureSize, requestedSimResolution, maxSimResolution, simResolution, effectiveFeatureSize);

            //Log for the no-badge (scene generation) case, deduped so authoring's live-preview re-runs and multiple
            //biomes sharing the same capped graph don't spam the console with the same message.
            if (!string.Equals(m_simResolutionWarning, s_lastLoggedSimResolutionWarning, System.StringComparison.Ordinal))
            {
                s_lastLoggedSimResolutionWarning = m_simResolutionWarning;
                Debug.Log("Hydraulic Erosion 2: " + m_simResolutionWarning);
            }
        }

        private RequiredOutputs GetRequiredOutputs(GraphContext context, SlotRef outputHeightRef, SlotRef outputSoilRef)
        {
            RequiredOutputs outputs = new RequiredOutputs();
            outputs.height = context.GetReferenceCount(outputHeightRef) > 0 || context.IsTargetNode(m_id);
            outputs.soil = context.GetReferenceCount(outputSoilRef) > 0;
            return outputs;
        }

        private string CreateSettingsJson()
        {
            SettingsSnapshot snapshot = new SettingsSnapshot();
            snapshot.iterationCount = m_iterationCount;
            snapshot.rainRate = m_rainRate;
            snapshot.rainOverTime = m_rainOverTime;
            snapshot.sedimentCapacity = m_sedimentCapacity;
            snapshot.erosionRate = m_erosionRate;
            snapshot.depositionRate = m_depositionRate;
            snapshot.evaporationRate = m_evaporationRate;
            snapshot.featureSize = m_featureSize;
            snapshot.useBorderFade = m_useBorderFade;
            snapshot.depositRamp = m_depositRamp;
            snapshot.drainageRamp = m_drainageRamp;
            return JsonUtility.ToJson(snapshot);
        }

        private string CreateArgsJson(GraphContext context, int graphResolution, int inputResolution, int outputResolution, Vector4 bounds, float terrainHeight)
        {
            ArgsSnapshot snapshot = new ArgsSnapshot();
            snapshot.graphResolution = graphResolution;
            snapshot.inputResolution = inputResolution;
            snapshot.outputResolution = outputResolution;
            snapshot.boundsX = bounds.x;
            snapshot.boundsY = bounds.y;
            snapshot.boundsZ = bounds.z;
            snapshot.boundsW = bounds.w;
            snapshot.terrainHeight = terrainHeight;
            snapshot.seed = context.GetArg(Args.SEED).intValue;
            return JsonUtility.ToJson(snapshot);
        }

        private bool TryLoadFromCache(
            GraphContext context,
            string settingsJson,
            string argsJson,
            Texture inputHeightTexture,
            Texture hardnessTexture,
            RequiredOutputs requiredOutputs,
            int outputResolution,
            SlotRef outputHeightRef,
            SlotRef outputSoilRef)
        {
            GraphExecutionCache.Entry entry;

            // No entry means this node has not produced a reusable result for this graph yet, or a cache was not provided to this graph execution
            if (!context.TryGetCacheEntry(m_id, out entry) || entry == null)
            {
                return false;
            }

            // Settings and execution arguments must match exactly before comparing GPU inputs.
            if (!string.Equals(entry.settingsJson, settingsJson, System.StringComparison.Ordinal) ||
                !string.Equals(entry.argsJson, argsJson, System.StringComparison.Ordinal))
            {
                return false;
            }

            // The entry must contain every output needed by this execution.
            if (!HasRequiredCachedOutputs(entry, requiredOutputs, outputResolution))
            {
                return false;
            }

            // Height input changes invalidate the cached erosion result.
            if (!InputMatches(entry, inputHeightSlot.id, inputHeightTexture))
            {
                return false;
            }

            // Hardness input changes invalidate the cached erosion result.
            if (!InputMatches(entry, hardnessSlot.id, hardnessTexture))
            {
                return false;
            }

            CopyRequiredOutputsFromCache(context, entry, requiredOutputs, outputResolution, outputHeightRef, outputSoilRef);
            return true;
        }

        private bool InputMatches(GraphExecutionCache.Entry entry, int slotId, Texture currentTexture)
        {
            RenderTexture cachedTexture;
            // A missing cached input means this entry cannot prove the current input is unchanged.
            if (entry.inputTextures == null || !entry.inputTextures.TryGetValue(slotId, out cachedTexture))
            {
                return false;
            }
            return TextureComparator.AreEqual(currentTexture, cachedTexture);
        }

        private bool HasRequiredCachedOutputs(GraphExecutionCache.Entry entry, RequiredOutputs requiredOutputs, int outputResolution)
        {
            // Height is required by the current execution, so the cached entry must contain it.
            if (requiredOutputs.height && !HasCachedOutput(entry, outputHeightSlot.id, outputResolution))
            {
                return false;
            }
            // Soil is required by the current execution, so the cached entry must contain it.
            if (requiredOutputs.soil && !HasCachedOutput(entry, outputSoilSlot.id, outputResolution))
            {
                return false;
            }
            return true;
        }

        private static bool HasCachedOutput(GraphExecutionCache.Entry entry, int slotId, int outputResolution)
        {
            RenderTexture cachedTexture;
            // A missing cached output cannot be copied into the current DataPool target.
            if (entry.outputTextures == null || !entry.outputTextures.TryGetValue(slotId, out cachedTexture))
            {
                return false;
            }
            // Cached outputs must match the requested descriptor before reuse.
            return cachedTexture != null &&
                cachedTexture.width == outputResolution &&
                cachedTexture.height == outputResolution &&
                cachedTexture.format == RenderTextureFormat.RFloat;
        }

        private void CopyRequiredOutputsFromCache(
            GraphContext context,
            GraphExecutionCache.Entry entry,
            RequiredOutputs requiredOutputs,
            int outputResolution,
            SlotRef outputHeightRef,
            SlotRef outputSoilRef)
        {
            if (requiredOutputs.height)
            {
                CopyOutputFromCache(context, entry.outputTextures[outputHeightSlot.id], outputResolution, outputHeightRef);
            }
            if (requiredOutputs.soil)
            {
                CopyOutputFromCache(context, entry.outputTextures[outputSoilSlot.id], outputResolution, outputSoilRef);
            }
        }

        private static void CopyOutputFromCache(GraphContext context, RenderTexture cachedTexture, int outputResolution, SlotRef outputRef)
        {
            DataPool.RtDescriptor outputDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
            RenderTexture outputTexture = context.CreateRenderTarget(outputDesc, outputRef);
            UnityEngine.Graphics.Blit(cachedTexture, outputTexture);
        }

        private void StoreToCache(
            GraphContext context,
            string settingsJson,
            string argsJson,
            Texture inputHeightTexture,
            Texture inputHardnessTexture,
            SlotRef outputHeightRef,
            SlotRef outputSoilRef)
        {
            GraphExecutionCache.Entry entry = new GraphExecutionCache.Entry();
            entry.settingsJson = settingsJson;
            entry.argsJson = argsJson;

            RenderTexture inputHeightCopy = GraphicsUtils.CloneToRenderTexture(inputHeightTexture);
            entry.inputTextures[inputHeightSlot.id] = inputHeightCopy;

            RenderTexture inputHardnessCopy = GraphicsUtils.CloneToRenderTexture(inputHardnessTexture);
            entry.inputTextures[hardnessSlot.id] = inputHardnessCopy;

            RenderTexture outputHeightTexture = context.GetTexture(outputHeightRef);
            if (outputHeightTexture != null)
            {
                RenderTexture outputHeightCopy = GraphicsUtils.CloneToRenderTexture(outputHeightTexture);
                entry.outputTextures[outputHeightSlot.id] = outputHeightCopy;
            }

            RenderTexture outputSoilTexture = context.GetTexture(outputSoilRef);
            if (outputSoilTexture != null)
            {
                RenderTexture outputSoilCopy = GraphicsUtils.CloneToRenderTexture(outputSoilTexture);
                entry.outputTextures[outputSoilSlot.id] = outputSoilCopy;
            }

            if (!context.SetCacheEntry(m_id, entry))
            {
                entry.Dispose();
            }
        }

        //Runs the whole erosion on the downsampled sim canvas. livePreviewHeight: dispatch OutputHeight (at full res)
        //at each yield so the 3D viewport shows the terrain evolving during the bake (editor refreshes on progress ticks).
        private IEnumerator Simulate(GraphContext context, ComputeShader shader, int numIteration, int simResolution, int outputResolution, bool livePreviewHeight)
        {
            int simGroup = (simResolution + 7) / 8;   //sim kernels dispatch over the sim canvas
            int outGroup = (outputResolution + 7) / 8; //the live-preview OutputHeight dispatches over the full output

            shader.Dispatch(KERNEL_INIT_WORLD_DATA, simGroup, 1, simGroup);

            //Auto mode buckets steps-per-frame by resolution to stay under the driver TDR watchdog; take 50% of the
            //shared bucket for headroom (this node is dispatch-dense). The sim canvas is downsampled, so this is usually generous.
            int effectiveIterationPerFrame = m_useAutoIterationPerFrame
                ? Mathf.Max(1, Mathf.RoundToInt(Utilities.GetAutoIterationPerFrame(simResolution) * 0.5f))
                : iterationPerFrame;

            for (int i = 0; i < numIteration; ++i)
            {
                //rain-over-time: full storm -> taper -> dry settle across the single sim run
                float t = (numIteration > 1) ? i * 1.0f / (numIteration - 1) : 1f;
                shader.SetFloat(RAIN_RATE_CM, m_rainRate * MM_TO_CM * Mathf.Max(0, m_rainOverTime.Evaluate(t)));
                shader.Dispatch(KERNEL_RAIN, simGroup, 1, simGroup);

                for (int iFlow = 0; iFlow < FLOW_SUBSTEP_COUNT; ++iFlow)
                {
                    shader.Dispatch(KERNEL_WATER_FLOW_PHASE_1, simGroup, 1, simGroup);
                    shader.Dispatch(KERNEL_WATER_FLOW_PHASE_2, simGroup, 1, simGroup);
                }

                shader.Dispatch(KERNEL_EROSION_DEPOSITION, simGroup, 1, simGroup);
                for (int iSedimentTransport = 0; iSedimentTransport < SEDIMENT_TRANSPORT_SUBSTEP_COUNT; ++iSedimentTransport)
                {
                    shader.Dispatch(KERNEL_SEDIMENT_TRANSPORT_PHASE_1, simGroup, 1, simGroup);
                    shader.Dispatch(KERNEL_SEDIMENT_TRANSPORT_PHASE_2, simGroup, 1, simGroup);
                }

                shader.Dispatch(KERNEL_EVAPORATION, simGroup, 1, simGroup);

                if (i % effectiveIterationPerFrame == 0 && shouldSplitExecution)
                {
                    if (livePreviewHeight)
                    {
                        //upsample the in-progress erosion onto the full-res source so the viewport shows this frame live
                        shader.Dispatch(KERNEL_OUTPUT_HEIGHT, outGroup, 1, outGroup);
                    }
                    context.SetCurrentProgress((i + 1) * 1.0f / numIteration);
                    yield return null;
                    if (context.isCancellationRequested)
                    {
                        yield break;
                    }
                }
            }
        }
    }
}
#endif
