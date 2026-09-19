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
        title = "Thermal Erosion 3",
        path = "Nature/Thermal Erosion 3",
        description = "Simulate soil gradually breaking out from steep surfaces over time, sliding down and resting in lower places. Erosion Rate is the amount of loose soil produced per iteration, so the total effect scales with iteration count.",
        keywords = "talus, heat, temperature, weathering, wear, bedrock, outcrop, exposure")]
    public class ThermalErosionNode3 : ImageNodeBase
    {
        public readonly MaskSlot inputHeightSlot = new MaskSlot("Height", SlotDirection.Input, 0);
        public readonly MaskSlot hardnessSlot = new MaskSlot("Hardness", SlotDirection.Input, 1);

        public readonly MaskSlot outputHeightSlot = new MaskSlot("Height", SlotDirection.Output, 100);
        public readonly MaskSlot outputSedimentSlot = new MaskSlot("Sediment", SlotDirection.Output, 101);
        public readonly MaskSlot outputExposureSlot = new MaskSlot("Exposure", SlotDirection.Output, 102);

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
        private float m_erosionRate;
        public float erosionRate
        {
            get
            {
                return m_erosionRate;
            }
            set
            {
                m_erosionRate = Mathf.Max(0, value);
            }
        }

        [SerializeField]
        private float m_talusAngle;
        public float talusAngle
        {
            get
            {
                return m_talusAngle;
            }
            set
            {
                m_talusAngle = Mathf.Clamp(value, 0, 89);
            }
        }

        [SerializeField]
        private bool m_useMultiResolution;
        public bool useMultiResolution
        {
            get
            {
                return m_useMultiResolution;
            }
            set
            {
                m_useMultiResolution = value;
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

        [SerializeField]
        private AnimationCurve m_erosionOverTime;
        public AnimationCurve erosionOverTime
        {
            get
            {
                return m_erosionOverTime;
            }
            set
            {
                m_erosionOverTime = value;
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

        private static readonly string COMPUTE_SHADER_NAME = "Vista/Shaders/Graph/ThermalErosion3";
        private static readonly string TEMP_WORLD_DATA = "~ThermalErosion3_WorldData";
        private static readonly string TEMP_SIM_DATA_0 = "~ThermalErosion3_SimData0";
        private static readonly string TEMP_SIM_DATA_1 = "~ThermalErosion3_SimData1";

        private static readonly int INPUT_HEIGHT_01 = Shader.PropertyToID("_InputHeight01");
        private static readonly int INPUT_HARDNESS_01 = Shader.PropertyToID("_InputHardness01");
        private static readonly int WORLD_DATA_CM = Shader.PropertyToID("_WorldDataCM");
        private static readonly int SIM_DATA_0 = Shader.PropertyToID("_SimData0");
        private static readonly int SIM_DATA_1 = Shader.PropertyToID("_SimData1");
        private static readonly int OUTPUT_RT_01 = Shader.PropertyToID("_OutputRT01");

        private static readonly int WORLD_DATA_RESOLUTION = Shader.PropertyToID("_WorldDataResolution");
        private static readonly int UPSAMPLE_RESOLUTION = Shader.PropertyToID("_UpsampleResolution");
        private static readonly int WORLD_SIZE_CM = Shader.PropertyToID("_WorldSizeCM");
        private static readonly int SEDIMENT_TRANSPORT_CONSTANT = Shader.PropertyToID("_SedimentTransportConstant");
        private static readonly int THERMAL_EROSION_DELTA_HEIGHT_THRESHOLD = Shader.PropertyToID("_ThermalErosionDeltaHeightThreshold");

        private static readonly int EROSION_RATE_CM = Shader.PropertyToID("_ErosionRate");
        private static readonly int USE_BORDER_FADE = Shader.PropertyToID("_UseBorderFade");

        private static readonly int KERNEL_INIT_WORLD_DATA = 0;
        private static readonly int KERNEL_OUTPUT_HEIGHT = 1;
        private static readonly int KERNEL_WEATHERING = 2;
        private static readonly int KERNEL_THERMAL_EROSION_PHASE_1 = 3;
        private static readonly int KERNEL_THERMAL_EROSION_PHASE_2 = 4;
        private static readonly int KERNEL_UPSAMPLING = 5;
        private static readonly int KERNEL_OUTPUT_DEPOSIT = 6;
        private static readonly int KERNEL_OUTPUT_EXPOSURE = 7;

        private static readonly int SUBSTEP_COUNT = 1;

        // Multi-resolution iteration budget knob. Scales how many iterations each coarse (lower-res) stage
        // gets; the finest (full-res) stage takes whatever is left. Internal tuning only, no clamping —
        // it is the dev's job to keep it in the valid range [0, 2).
        //   0     : coarse stages get 0 iterations -> effectively no multi-res, all erosion at the finest stage.
        //   1     : legacy baseline. Each coarse stage gets half the iterations of the next-finer stage; the
        //           finest stage's share asymptotes to 50% at high res.
        //   ->2   : coarse stages consume almost the whole budget and the finest stage STARVES (share -> 0 as
        //           output grows). Must stay strictly below 2.
        // Higher = more iterations pushed onto the cheap coarse stages (faster, since the finest stage is the
        // expensive one), at the cost of under-eroding fine detail. Finest-stage share ~= (1 - value/2) at high res.
        // 1.5 chosen from timing + visual A/B testing: ~53% faster than 1.0 at 2K with negligible quality loss.
        private static readonly float COARSE_ITERATION_MULTIPLIER = 1.5f;

        public ThermalErosionNode3() : base()
        {
            m_iterationCount = 1000;
            m_iterationPerFrame = 50;
            m_useAutoIterationPerFrame = true;

            m_erosionRate = 1f;
            m_talusAngle = 15f;

            m_useMultiResolution = true;
            m_useBorderFade = false;

            m_erosionOverTime = AnimationCurve.Linear(0.95f, 1f, 1f, 0f);
        }

        public override void ExecuteImmediate(GraphContext context)
        {
            Utilities.DrainCoroutine(Execute(context));
        }

        class SimTextures
        {
            public RenderTexture worldDataTexture;
            public RenderTexture simDataTexture0;
            public RenderTexture simDataTexture1;
        }

        private struct RequiredOutputs
        {
            public bool height;
            public bool sediment;
            public bool exposure;
        }

        [System.Serializable]
        private struct SettingsSnapshot
        {
            public int iterationCount;
            public float erosionRate;
            public float talusAngle;
            public bool useMultiResolution;
            public bool useBorderFade;
            public AnimationCurve erosionOverTime;
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
            SlotRef outputSedimentRef = new SlotRef(m_id, outputSedimentSlot.id);
            SlotRef outputExposureRef = new SlotRef(m_id, outputExposureSlot.id);
            RequiredOutputs requiredOutputs = GetRequiredOutputs(context, outputHeightRef, outputSedimentRef, outputExposureRef);
            string settingsJson = null;
            string argsJson = null;

            if (inputHeightTexture == Texture2D.blackTexture)
            {
                if (requiredOutputs.height)
                {
                    DataPool.RtDescriptor outputHeightDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                    RenderTexture outputHeightTexture = context.CreateRenderTarget(outputHeightDesc, outputHeightRef);
                    GraphicsUtils.ClearWithZeros(outputHeightTexture);
                }
                if (requiredOutputs.sediment)
                {
                    DataPool.RtDescriptor outputSedimentDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                    RenderTexture outputSedimentTexture = context.CreateRenderTarget(outputSedimentDesc, outputSedimentRef);
                    GraphicsUtils.ClearWithZeros(outputSedimentTexture);
                }
                if (requiredOutputs.exposure)
                {
                    DataPool.RtDescriptor outputExposureDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                    RenderTexture outputExposureTexture = context.CreateRenderTarget(outputExposureDesc, outputExposureRef);
                    GraphicsUtils.ClearWithZeros(outputExposureTexture);
                }
                context.ReleaseReference(inputHeightRefLink);
                context.ReleaseReference(hardnessRefLink);
                yield break;
            }

            if (context.hasCache)
            {
                settingsJson = CreateSettingsJson();
                argsJson = CreateArgsJson(context, graphResolution, inputResolution, outputResolution, bounds, maxHeight);
                if (TryLoadFromCache(context, settingsJson, argsJson, inputHeightTexture, hardnessTexture, requiredOutputs, outputResolution, outputHeightRef, outputSedimentRef, outputExposureRef))
                {
                    context.SetCurrentProgress(1f);
                    context.ReleaseReference(inputHeightRefLink);
                    context.ReleaseReference(hardnessRefLink);
                    yield break;
                }
            }

            DataPool.RtDescriptor worldDataDesc = DataPool.RtDescriptor.Create(outputResolution + 8, outputResolution + 8, RenderTextureFormat.ARGBFloat);
            RenderTexture worldDataTexture = context.CreateTemporaryRT(worldDataDesc, TEMP_WORLD_DATA);
            GraphicsUtils.ClearWithZeros(worldDataTexture);

            Vector3 worldSizeCM = new Vector3(bounds.z, maxHeight, bounds.w) * 100f;

            DataPool.RtDescriptor simDataDesc = DataPool.RtDescriptor.Create(worldDataTexture.width, worldDataTexture.height, RenderTextureFormat.ARGBFloat);
            RenderTexture simDataTexture0 = context.CreateTemporaryRT(simDataDesc, TEMP_SIM_DATA_0); //contains outflowVH
            RenderTexture simDataTexture1 = context.CreateTemporaryRT(simDataDesc, TEMP_SIM_DATA_1); //contains outflowDiag

            GraphicsUtils.ClearWithZeros(simDataTexture0);
            GraphicsUtils.ClearWithZeros(simDataTexture1);

            ComputeShader shader = Object.Instantiate(sourceShader);
            context.RegisterDestroyLater(shader);
            //erosionRate is uploaded per iteration inside Simulate, scaled by the erosionOverTime curve
            shader.SetFloat(USE_BORDER_FADE, m_useBorderFade ? 1f : 0f);

            shader.SetTexture(KERNEL_INIT_WORLD_DATA, INPUT_HEIGHT_01, inputHeightTexture);
            shader.SetTexture(KERNEL_INIT_WORLD_DATA, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_INIT_WORLD_DATA, SIM_DATA_0, simDataTexture0); //upsampled data from last canvas

            shader.SetTexture(KERNEL_WEATHERING, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_WEATHERING, INPUT_HARDNESS_01, hardnessTexture);

            shader.SetTexture(KERNEL_THERMAL_EROSION_PHASE_1, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_THERMAL_EROSION_PHASE_1, SIM_DATA_0, simDataTexture0);
            shader.SetTexture(KERNEL_THERMAL_EROSION_PHASE_1, SIM_DATA_1, simDataTexture1);

            shader.SetTexture(KERNEL_THERMAL_EROSION_PHASE_2, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_THERMAL_EROSION_PHASE_2, SIM_DATA_0, simDataTexture0);
            shader.SetTexture(KERNEL_THERMAL_EROSION_PHASE_2, SIM_DATA_1, simDataTexture1);

            shader.SetFloat(SEDIMENT_TRANSPORT_CONSTANT, 1.0f / SUBSTEP_COUNT);

            shader.SetTexture(KERNEL_UPSAMPLING, WORLD_DATA_CM, worldDataTexture);
            shader.SetTexture(KERNEL_UPSAMPLING, INPUT_HEIGHT_01, inputHeightTexture);
            shader.SetTexture(KERNEL_UPSAMPLING, SIM_DATA_0, simDataTexture0); //storing interpolated data

            SimTextures simTextures = new SimTextures();
            simTextures.worldDataTexture = worldDataTexture;
            simTextures.simDataTexture0 = simDataTexture0;
            simTextures.simDataTexture1 = simDataTexture1;

            if (m_useMultiResolution)
            {
                List<int> simRes = new List<int>();
                List<int> numIteration = new List<int>();
                int res = 256;
                while (res < outputResolution)
                {
                    simRes.Add(res);
                    res *= 2;
                }
                simRes.Add(outputResolution);

                int remainingIteration = m_iterationCount;
                int n = 0;
                for (int i = 0; i < simRes.Count - 1; ++i)
                {
                    n = Mathf.FloorToInt(m_iterationCount * 0.5f * COARSE_ITERATION_MULTIPLIER * (simRes[i] * 1.0f / outputResolution));
                    numIteration.Add(n);
                    remainingIteration -= n;
                }
                numIteration.Add(remainingIteration);

                //iterationOffset tracks progress across all stages so the erosionOverTime curve spans the whole simulation, not each stage
                int iterationOffset = 0;
                for (int i = 0; i < simRes.Count - 1; ++i)
                {
                    yield return Simulate(context, shader, numIteration[i], iterationOffset, simRes[i], simRes[i], worldSizeCM, simTextures);
                    if (context.isCancellationRequested)
                    {
                        yield break;
                    }
                    iterationOffset += numIteration[i];
                    Upsample(shader, simRes[i + 1], simRes[i + 1]);
                }

                yield return Simulate(context, shader, numIteration[numIteration.Count - 1], iterationOffset, simRes[simRes.Count - 1], simRes[simRes.Count - 1], worldSizeCM, simTextures);
                if (context.isCancellationRequested)
                {
                    yield break;
                }
            }
            else
            {
                yield return Simulate(context, shader, m_iterationCount, 0, outputResolution, outputResolution, worldSizeCM, simTextures);
                if (context.isCancellationRequested)
                {
                    yield break;
                }
            }

            if (requiredOutputs.height)
            {
                DataPool.RtDescriptor outputHeightDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                RenderTexture outputHeightTexture = context.CreateRenderTarget(outputHeightDesc, outputHeightRef);

                shader.SetTexture(KERNEL_OUTPUT_HEIGHT, WORLD_DATA_CM, worldDataTexture);
                shader.SetTexture(KERNEL_OUTPUT_HEIGHT, OUTPUT_RT_01, outputHeightTexture);
                shader.Dispatch(KERNEL_OUTPUT_HEIGHT, (outputResolution + 7) / 8, 1, (outputResolution + 7) / 8);
            }

            if (requiredOutputs.sediment)
            {
                DataPool.RtDescriptor outputSedimentDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                RenderTexture outputSedimentTexture = context.CreateRenderTarget(outputSedimentDesc, outputSedimentRef);

                shader.SetTexture(KERNEL_OUTPUT_DEPOSIT, WORLD_DATA_CM, worldDataTexture);
                shader.SetTexture(KERNEL_OUTPUT_DEPOSIT, OUTPUT_RT_01, outputSedimentTexture);
                shader.Dispatch(KERNEL_OUTPUT_DEPOSIT, (outputResolution + 7) / 8, 1, (outputResolution + 7) / 8);
            }

            if (requiredOutputs.exposure)
            {
                DataPool.RtDescriptor outputExposureDesc = DataPool.RtDescriptor.Create(outputResolution, outputResolution, RenderTextureFormat.RFloat);
                RenderTexture outputExposureTexture = context.CreateRenderTarget(outputExposureDesc, outputExposureRef);

                shader.SetTexture(KERNEL_OUTPUT_EXPOSURE, WORLD_DATA_CM, worldDataTexture);
                shader.SetTexture(KERNEL_OUTPUT_EXPOSURE, OUTPUT_RT_01, outputExposureTexture);
                shader.Dispatch(KERNEL_OUTPUT_EXPOSURE, (outputResolution + 7) / 8, 1, (outputResolution + 7) / 8);
            }

            if (context.hasCache)
            {
                StoreToCache(context, settingsJson, argsJson, inputHeightTexture, hardnessTexture, outputHeightRef, outputSedimentRef, outputExposureRef);
            }

            context.ReleaseReference(inputHeightRefLink);
            context.ReleaseReference(hardnessRefLink);
            context.ReleaseTemporary(TEMP_WORLD_DATA);
            context.ReleaseTemporary(TEMP_SIM_DATA_0);
            context.ReleaseTemporary(TEMP_SIM_DATA_1);

            yield return null;
        }

        private RequiredOutputs GetRequiredOutputs(GraphContext context, SlotRef outputHeightRef, SlotRef outputSedimentRef, SlotRef outputExposureRef)
        {
            RequiredOutputs outputs = new RequiredOutputs();
            outputs.height = context.GetReferenceCount(outputHeightRef) > 0 || context.IsTargetNode(m_id);
            outputs.sediment = context.GetReferenceCount(outputSedimentRef) > 0;
            outputs.exposure = context.GetReferenceCount(outputExposureRef) > 0;
            return outputs;
        }

        private string CreateSettingsJson()
        {
            SettingsSnapshot snapshot = new SettingsSnapshot();
            snapshot.iterationCount = m_iterationCount;
            snapshot.erosionRate = m_erosionRate;
            snapshot.talusAngle = m_talusAngle;
            snapshot.useMultiResolution = m_useMultiResolution;
            snapshot.useBorderFade = m_useBorderFade;
            snapshot.erosionOverTime = m_erosionOverTime;
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
            SlotRef outputSedimentRef,
            SlotRef outputExposureRef)
        {
            GraphExecutionCache.Entry entry;
            if (!context.TryGetCacheEntry(m_id, out entry) || entry == null)
            {
                return false;
            }

            if (!string.Equals(entry.settingsJson, settingsJson, System.StringComparison.Ordinal) ||
                !string.Equals(entry.argsJson, argsJson, System.StringComparison.Ordinal))
            {
                return false;
            }

            if (!HasRequiredCachedOutputs(entry, requiredOutputs, outputResolution))
            {
                return false;
            }

            if (!InputMatches(entry, inputHeightSlot.id, inputHeightTexture))
            {
                return false;
            }

            if (!InputMatches(entry, hardnessSlot.id, hardnessTexture))
            {
                return false;
            }

            CopyRequiredOutputsFromCache(context, entry, requiredOutputs, outputResolution, outputHeightRef, outputSedimentRef, outputExposureRef);
            return true;
        }

        private bool InputMatches(GraphExecutionCache.Entry entry, int slotId, Texture currentTexture)
        {
            RenderTexture cachedTexture;
            if (entry.inputTextures == null || !entry.inputTextures.TryGetValue(slotId, out cachedTexture))
            {
                return false;
            }
            return TextureComparator.AreEqual(currentTexture, cachedTexture);
        }

        private bool HasRequiredCachedOutputs(GraphExecutionCache.Entry entry, RequiredOutputs requiredOutputs, int outputResolution)
        {
            if (requiredOutputs.height && !HasCachedOutput(entry, outputHeightSlot.id, outputResolution))
            {
                return false;
            }
            if (requiredOutputs.sediment && !HasCachedOutput(entry, outputSedimentSlot.id, outputResolution))
            {
                return false;
            }
            if (requiredOutputs.exposure && !HasCachedOutput(entry, outputExposureSlot.id, outputResolution))
            {
                return false;
            }
            return true;
        }

        private static bool HasCachedOutput(GraphExecutionCache.Entry entry, int slotId, int outputResolution)
        {
            RenderTexture cachedTexture;
            if (entry.outputTextures == null || !entry.outputTextures.TryGetValue(slotId, out cachedTexture))
            {
                return false;
            }
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
            SlotRef outputSedimentRef,
            SlotRef outputExposureRef)
        {
            if (requiredOutputs.height)
            {
                CopyOutputFromCache(context, entry.outputTextures[outputHeightSlot.id], outputResolution, outputHeightRef);
            }
            if (requiredOutputs.sediment)
            {
                CopyOutputFromCache(context, entry.outputTextures[outputSedimentSlot.id], outputResolution, outputSedimentRef);
            }
            if (requiredOutputs.exposure)
            {
                CopyOutputFromCache(context, entry.outputTextures[outputExposureSlot.id], outputResolution, outputExposureRef);
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
            SlotRef outputSedimentRef,
            SlotRef outputExposureRef)
        {
            GraphExecutionCache.Entry entry = new GraphExecutionCache.Entry();
            entry.settingsJson = settingsJson;
            entry.argsJson = argsJson;

            entry.inputTextures[inputHeightSlot.id] = GraphicsUtils.CloneToRenderTexture(inputHeightTexture);
            entry.inputTextures[hardnessSlot.id] = GraphicsUtils.CloneToRenderTexture(inputHardnessTexture);

            RenderTexture outputHeightTexture = context.GetTexture(outputHeightRef);
            if (outputHeightTexture != null)
            {
                entry.outputTextures[outputHeightSlot.id] = GraphicsUtils.CloneToRenderTexture(outputHeightTexture);
            }

            RenderTexture outputSedimentTexture = context.GetTexture(outputSedimentRef);
            if (outputSedimentTexture != null)
            {
                entry.outputTextures[outputSedimentSlot.id] = GraphicsUtils.CloneToRenderTexture(outputSedimentTexture);
            }

            RenderTexture outputExposureTexture = context.GetTexture(outputExposureRef);
            if (outputExposureTexture != null)
            {
                entry.outputTextures[outputExposureSlot.id] = GraphicsUtils.CloneToRenderTexture(outputExposureTexture);
            }

            if (!context.SetCacheEntry(m_id, entry))
            {
                entry.Dispose();
            }
        }

        private IEnumerator Simulate(GraphContext context, ComputeShader shader, int numIteration, int iterationOffset, int outputWidth, int outputHeight, Vector3 worldSizeCM, SimTextures textures)
        {
            int threadGroupX = (outputWidth + 7) / 8;
            int threadGroupY = 1;
            int threadGroupZ = (outputHeight + 7) / 8;

            shader.SetVector(WORLD_SIZE_CM, worldSizeCM);
            shader.SetVector(WORLD_DATA_RESOLUTION, new Vector4(outputWidth, outputHeight));
            shader.Dispatch(KERNEL_INIT_WORLD_DATA, threadGroupX, threadGroupY, threadGroupZ);

            float cellDistance = worldSizeCM.x / outputWidth;
            float thermalErosionDeltaHeightThreshold = Mathf.Tan(m_talusAngle * Mathf.Deg2Rad) * cellDistance;
            shader.SetFloat(THERMAL_EROSION_DELTA_HEIGHT_THRESHOLD, thermalErosionDeltaHeightThreshold);

            //In Auto mode, iterations-per-frame is bucketed by this stage's resolution so the per-frame GPU batch
            //stays under the driver TDR watchdog (a fixed 50/frame crashes Unity at 4K) while keeping the editor responsive.
            int effectiveIterationPerFrame = m_useAutoIterationPerFrame ? Utilities.GetAutoIterationPerFrame(outputWidth) : iterationPerFrame;

            int currentIteration = 0;
            for (int i = 0; i < numIteration; ++i)
            {
                //normalized simulation time across the whole run, drives the erosionOverTime curve
                float t = (m_iterationCount > 1) ? (iterationOffset + i) * 1.0f / (m_iterationCount - 1) : 1f;
                float erosionRateThisIteration = m_erosionRate * 0.1f * Mathf.Max(0, m_erosionOverTime.Evaluate(t));
                shader.SetFloat(EROSION_RATE_CM, erosionRateThisIteration);
                shader.Dispatch(KERNEL_WEATHERING, threadGroupX, threadGroupY, threadGroupZ);

                for (int iThermalErosion = 0; iThermalErosion < SUBSTEP_COUNT; ++iThermalErosion)
                {
                    shader.Dispatch(KERNEL_THERMAL_EROSION_PHASE_1, threadGroupX, threadGroupY, threadGroupZ);
                    shader.Dispatch(KERNEL_THERMAL_EROSION_PHASE_2, threadGroupX, threadGroupY, threadGroupZ);
                }

                if (i % effectiveIterationPerFrame == 0 && shouldSplitExecution)
                {
                    context.SetCurrentProgress(currentIteration * 1.0f / numIteration);
                    yield return null;
                    if (context.isCancellationRequested)
                    {
                        yield break;
                    }
                }
                currentIteration += 1;
            }
        }

        private void Upsample(ComputeShader shader, int newWidth, int newHeight)
        {
            shader.SetVector(UPSAMPLE_RESOLUTION, new Vector4(newWidth, newHeight, 0, 0));
            int threadGroupX = (newWidth + 7) / 8;
            int threadGroupY = 1;
            int threadGroupZ = (newHeight + 7) / 8;
            shader.Dispatch(KERNEL_UPSAMPLING, threadGroupX, threadGroupY, threadGroupZ);
        }
    }
}
#endif
