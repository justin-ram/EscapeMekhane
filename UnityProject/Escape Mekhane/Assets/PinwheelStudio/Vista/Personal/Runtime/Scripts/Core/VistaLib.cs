#if VISTA
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.Graphics;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Provides low-level rendering helpers backed by Vista shaders and compute shaders.
    /// </summary>
    /// <remarks>
    /// These helpers are small runtime building blocks used to generate derived textures such as object-space normals,
    /// solid-color fills, and value-remapped masks without duplicating shader setup code at each call site.
    /// </remarks>
    public static class VistaLib
    {
        private class ExtractNormalMapUtils
        {
            /// <summary>
            /// Shader property ID for the source height map.
            /// </summary>
            public static readonly int HEIGHT_MAP = Shader.PropertyToID("_HeightMap");
            /// <summary>
            /// Shader property ID for the object-space size vector used to scale height gradients.
            /// </summary>
            public static readonly int SIZE = Shader.PropertyToID("_Size");
            /// <summary>
            /// Shader property ID for the source texture resolution.
            /// </summary>
            public static readonly int RESOLUTION = Shader.PropertyToID("_Resolution");
            /// <summary>
            /// Shader property ID for the destination render texture.
            /// </summary>
            public static readonly int TARGET_RT = Shader.PropertyToID("_TargetRT");
            /// <summary>
            /// Resource path of the compute shader used to extract object-space normals from a height map.
            /// </summary>
            public static readonly string SHADER_NAME = "Vista/Shaders/ExtractNormalMap";
        }

        /// <summary>
        /// Generates an object-space normal map from a height map texture.
        /// </summary>
        /// <param name="targetRT">
        /// The destination render texture that receives the generated normal map. It must have the same resolution as
        /// <paramref name="heightMap"/>.
        /// </param>
        /// <param name="heightMap">The source height map texture to sample.</param>
        /// <param name="sizeOS">
        /// The object-space size represented by the height map. This is used to scale height differentials when computing
        /// the normal vectors.
        /// </param>
        /// <remarks>
        /// The method loads the compute shader from Resources each time it runs and dispatches it with 8x8-style thread
        /// groups derived from the target resolution. A debug assertion checks that source and destination resolutions
        /// match.
        /// </remarks>
        public static void ExtractNormalMapOS(RenderTexture targetRT, Texture heightMap, Vector3 sizeOS)
        {
            Debug.Assert(targetRT.width == heightMap.width && targetRT.height == heightMap.height, "Extractring normal map: targetRT & heightMap must have the same resolution");

            ComputeShader shader = Resources.Load<ComputeShader>(ExtractNormalMapUtils.SHADER_NAME);
            shader.SetVector(ExtractNormalMapUtils.SIZE, sizeOS);
            shader.SetVector(ExtractNormalMapUtils.RESOLUTION, new Vector4(targetRT.width, targetRT.height));
            shader.SetTexture(0, ExtractNormalMapUtils.HEIGHT_MAP, heightMap);
            shader.SetTexture(0, ExtractNormalMapUtils.TARGET_RT, targetRT);

            int threadGroupX = (targetRT.width + 7) / 8;
            int threadGroupY = 1;
            int threadGroupZ = (targetRT.height + 7) / 8;
            shader.Dispatch(0, threadGroupX, threadGroupY, threadGroupZ);
        }

        private class FillColorUtils
        {
            /// <summary>
            /// Shader property ID for the fill color.
            /// </summary>
            public static readonly int COLOR = Shader.PropertyToID("_Color");
            /// <summary>
            /// Shader property ID for the destination render texture.
            /// </summary>
            public static readonly int TARGET_RT = Shader.PropertyToID("_TargetRT");
            /// <summary>
            /// Resource path of the compute shader used to write a uniform color into a render texture.
            /// </summary>
            public static readonly string SHADER_NAME = "Vista/Shaders/FillColor";
        }

        /// <summary>
        /// Fills an entire render texture with a uniform color.
        /// </summary>
        /// <param name="targetRT">The destination render texture to overwrite.</param>
        /// <param name="color">The color to write into every pixel of <paramref name="targetRT"/>.</param>
        /// <remarks>
        /// The method uses a compute shader loaded from Resources and dispatches it over the full render texture using
        /// thread groups derived from the destination resolution.
        /// </remarks>
        public static void FillColor(RenderTexture targetRT, Color color)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(FillColorUtils.SHADER_NAME);
            shader.SetVector(FillColorUtils.COLOR, color);
            shader.SetTexture(0, FillColorUtils.TARGET_RT, targetRT);

            int threadGroupX = (targetRT.width + 7) / 8;
            int threadGroupY = 1;
            int threadGroupZ = (targetRT.height + 7) / 8;
            shader.Dispatch(0, threadGroupX, threadGroupY, threadGroupZ);
        }

        private class RemapUtils
        {
            /// <summary>
            /// Resource path of the compute shader that scans a texture for integer-encoded min and max values.
            /// </summary>
            public static readonly string COMPUTE_SHADER_NAME = "Vista/Shaders/Graph/Remap";
            /// <summary>
            /// Shader property ID for the input texture.
            /// </summary>
            public static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
            /// <summary>
            /// Shader property ID for the compute buffer that stores scanned min and max values.
            /// </summary>
            public static readonly int MIN_MAX_BUFFER = Shader.PropertyToID("_MinMaxBuffer");
            /// <summary>
            /// Shader property ID for the integer normalization limit used by the min/max scan.
            /// </summary>
            public static readonly int INT_LIMIT = Shader.PropertyToID("_IntLimit");
            /// <summary>
            /// Kernel index used for the min/max scan compute shader.
            /// </summary>
            public static readonly int KERNEL_INDEX = 0;

            /// <summary>
            /// Shader name of the fullscreen remap material pass.
            /// </summary>
            public static readonly string SHADER_NAME = "Hidden/Vista/Graph/Remap";
            /// <summary>
            /// Shader property ID for the scanned input minimum.
            /// </summary>
            public static readonly int IN_MIN = Shader.PropertyToID("_InMin");
            /// <summary>
            /// Shader property ID for the scanned input maximum.
            /// </summary>
            public static readonly int IN_MAX = Shader.PropertyToID("_InMax");
            /// <summary>
            /// Shader property ID for the desired output minimum.
            /// </summary>
            public static readonly int OUT_MIN = Shader.PropertyToID("_OutMin");
            /// <summary>
            /// Shader property ID for the desired output maximum.
            /// </summary>
            public static readonly int OUT_MAX = Shader.PropertyToID("_OutMax");
            /// <summary>
            /// Material pass index used for the fullscreen remap draw.
            /// </summary>
            public static readonly int PASS = 0;
        }

        /// <summary>
        /// Remaps the normalized values of an input texture into a caller-defined output range.
        /// </summary>
        /// <param name="targetRT">The destination render texture that receives the remapped result.</param>
        /// <param name="inputTexture">The source texture whose values should be scanned and remapped.</param>
        /// <param name="outMin">The minimum value of the destination range.</param>
        /// <param name="outMax">The maximum value of the destination range.</param>
        /// <remarks>
        /// For most textures, the method first runs a compute shader that scans the source texture for integer-encoded min
        /// and max values, then converts those values back into normalized floats by dividing by <see cref="int.MaxValue"/>.
        /// The fullscreen remap shader uses that scanned input range to stretch or compress the data into the requested
        /// output range. When <paramref name="inputTexture"/> is <see cref="Texture2D.blackTexture"/>, the input range is
        /// treated as zero-to-zero and the scan step is skipped.
        /// </remarks>
        public static void Remap(RenderTexture targetRT, Texture inputTexture, float outMin, float outMax)
        {
            int[] minMaxData = new int[2];
            if (inputTexture != Texture2D.blackTexture)
            {
                minMaxData[0] = int.MaxValue;
                minMaxData[1] = int.MinValue;
                ComputeShader cs = Resources.Load<ComputeShader>(RemapUtils.COMPUTE_SHADER_NAME);
                cs.SetTexture(RemapUtils.KERNEL_INDEX, RemapUtils.MAIN_TEX, inputTexture);

                ComputeBuffer minMaxBuffer = new ComputeBuffer(2, sizeof(int));
                minMaxBuffer.SetData(minMaxData);
                cs.SetBuffer(RemapUtils.KERNEL_INDEX, RemapUtils.MIN_MAX_BUFFER, minMaxBuffer);
                cs.SetInt(RemapUtils.INT_LIMIT, int.MaxValue);
                int threadGroupX = (inputTexture.width + 7) / 8;
                int threadGroupY = (inputTexture.height + 7) / 8;
                int threadGroupZ = 1;
                cs.Dispatch(RemapUtils.KERNEL_INDEX, threadGroupX, threadGroupY, threadGroupZ);

                minMaxBuffer.GetData(minMaxData);
                minMaxBuffer.Dispose();
                Resources.UnloadAsset(cs);
            }
            else
            {
                minMaxData[0] = 0;
                minMaxData[1] = 0;
            }

            Material mat = new Material(ShaderUtilities.Find(RemapUtils.SHADER_NAME));
            mat.SetTexture(RemapUtils.MAIN_TEX, inputTexture);
            mat.SetFloat(RemapUtils.IN_MIN, minMaxData[0] * 1.0f / int.MaxValue);
            mat.SetFloat(RemapUtils.IN_MAX, minMaxData[1] * 1.0f / int.MaxValue);
            mat.SetFloat(RemapUtils.OUT_MIN, outMin);
            mat.SetFloat(RemapUtils.OUT_MAX, outMax);
            Drawing.DrawQuad(targetRT, mat, RemapUtils.PASS);
            Object.DestroyImmediate(mat);
        }

        private class SmoothUtils
        {
            public static readonly string SHADER_NAME = "Hidden/Vista/Graph/Smooth";
            public static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
            public static readonly int MASK_MAP = Shader.PropertyToID("_MaskMap");
            public static readonly int PASS = 0;
        }

        /// <summary>
        /// Applies Vista's iterative smooth filter to a render texture in-place using a temporary ping-pong texture.
        /// </summary>
        /// <param name="targetRT">The destination texture that also acts as the initial source.</param>
        /// <param name="tempRT">Temporary texture used for ping-pong rendering. Must match <paramref name="targetRT"/> resolution.</param>
        /// <param name="maskTexture">Optional mask controlling where smoothing applies. Null is treated as white.</param>
        /// <param name="iterationCount">Number of smoothing iterations to execute.</param>
        public static void Smooth(RenderTexture targetRT, RenderTexture tempRT, Texture maskTexture, int iterationCount)
        {
            Material mat = CreateSmoothMaterial(maskTexture);
            ExecuteSmooth(targetRT, tempRT, mat, iterationCount);
            Object.DestroyImmediate(mat);
        }

        /// <summary>
        /// Applies Vista's iterative smooth filter progressively and yields after a configurable number of iterations.
        /// </summary>
        /// <param name="targetRT">The destination texture that also acts as the initial source.</param>
        /// <param name="tempRT">Temporary texture used for ping-pong rendering. Must match <paramref name="targetRT"/> resolution.</param>
        /// <param name="maskTexture">Optional mask controlling where smoothing applies. Null is treated as white.</param>
        /// <param name="iterationCount">Total number of smoothing iterations to execute.</param>
        /// <param name="iterationPerFrame">Number of iterations processed between yields.</param>
        public static IEnumerator SmoothProgressive(RenderTexture targetRT, RenderTexture tempRT, Texture maskTexture, int iterationCount, int iterationPerFrame)
        {
            Material mat = CreateSmoothMaterial(maskTexture);
            for (int i = 0; i < iterationCount; ++i)
            {
                SmoothIteration(targetRT, tempRT, mat, i);
                if (i % iterationPerFrame == 0)
                {
                    yield return null;
                }
            }

            FinalizeSmooth(targetRT, tempRT, iterationCount);
            Object.DestroyImmediate(mat);
        }

        private static Material CreateSmoothMaterial(Texture maskTexture)
        {
            Material mat = new Material(ShaderUtilities.Find(SmoothUtils.SHADER_NAME));
            mat.SetTexture(SmoothUtils.MASK_MAP, maskTexture != null ? maskTexture : Texture2D.whiteTexture);
            return mat;
        }

        private static void ExecuteSmooth(RenderTexture targetRT, RenderTexture tempRT, Material mat, int iterationCount)
        {
            for (int i = 0; i < iterationCount; ++i)
            {
                SmoothIteration(targetRT, tempRT, mat, i);
            }

            FinalizeSmooth(targetRT, tempRT, iterationCount);
        }

        private static void SmoothIteration(RenderTexture targetRT, RenderTexture tempRT, Material mat, int iterationIndex)
        {
            RenderTexture src = iterationIndex % 2 == 0 ? targetRT : tempRT;
            RenderTexture dst = iterationIndex % 2 == 0 ? tempRT : targetRT;
            mat.SetTexture(SmoothUtils.MAIN_TEX, src);
            Drawing.DrawQuad(dst, mat, SmoothUtils.PASS);
        }

        private static void FinalizeSmooth(RenderTexture targetRT, RenderTexture tempRT, int iterationCount)
        {
            if (iterationCount % 2 != 0)
            {
                Drawing.Blit(tempRT, targetRT);
            }
        }

        /// <summary>
        /// Number of threads processed per compute group along a single dispatch axis by the point processing kernels.
        /// </summary>
        private static readonly int THREAD_PER_GROUP = 8;
        /// <summary>
        /// Maximum number of thread groups dispatched in a single one dimensional pass. Larger work loads are split into
        /// multiple passes using a running base index.
        /// </summary>
        private static readonly int MAX_THREAD_GROUP_1D = 64000 / 8;

        private class PointsUtils
        {
            /// <summary>
            /// Resource path of the compute shader that fills a grid of point samples.
            /// </summary>
            public static readonly string COMPUTE_SHADER_NAME = "Vista/Shaders/Graph/Points";
            /// <summary>
            /// Shader property ID for the destination position buffer.
            /// </summary>
            public static readonly int POSITIONS_BUFFER = Shader.PropertyToID("_Positions");
            /// <summary>
            /// Shader property ID for the lower left corner of the first grid point in world space.
            /// </summary>
            public static readonly int LOWER_LEFT_POINT = Shader.PropertyToID("_LowerLeftPoint");
            /// <summary>
            /// Shader property ID for the spacing between grid points in world meters.
            /// </summary>
            public static readonly int SPACING = Shader.PropertyToID("_Spacing");
            /// <summary>
            /// Shader property ID for the world bounds of the biome.
            /// </summary>
            public static readonly int BIOME_BOUNDS = Shader.PropertyToID("_BiomeBounds");
            /// <summary>
            /// Shader property ID for the grid dimension along the x and y axes.
            /// </summary>
            public static readonly int GRID_DIMENSION = Shader.PropertyToID("_GridDimension");
            /// <summary>
            /// Kernel index used by the point grid compute shader.
            /// </summary>
            public static readonly int KERNEL_INDEX = 0;
        }

        /// <summary>
        /// Computes the grid layout for a uniform set of points covering the given world bounds at the given spacing.
        /// </summary>
        /// <param name="worldBounds">The world bounds of the biome as x, y origin and z, w size.</param>
        /// <param name="spacing">The distance between adjacent points in world meters along each axis.</param>
        /// <param name="lowerLeftPoint">Receives the world position of the lower left grid point, snapped to the spacing.</param>
        /// <returns>The number of grid points along the x and y axes, each rounded up to a multiple of eight.</returns>
        /// <remarks>
        /// This is the pure sizing step shared by the Points node and any node that needs to allocate a matching position
        /// buffer before dispatching <see cref="GeneratePoints"/>. The instance count is the product of the two returned axes.
        /// </remarks>
        public static Vector2Int CalculatePointGrid(Vector4 worldBounds, Vector2 spacing, out Vector2 lowerLeftPoint)
        {
            float lowerLeftX = Mathf.Ceil(worldBounds.x / spacing.x) * spacing.x;
            float lowerLeftY = Mathf.Ceil(worldBounds.y / spacing.y) * spacing.y;
            lowerLeftPoint = new Vector2(lowerLeftX, lowerLeftY);
            int gridDimensionX = Utilities.MultipleOf8(Mathf.CeilToInt(worldBounds.z / spacing.x) + 1);
            int gridDimensionY = Utilities.MultipleOf8(Mathf.CeilToInt(worldBounds.w / spacing.y) + 1);
            return new Vector2Int(gridDimensionX, gridDimensionY);
        }

        /// <summary>
        /// Fills a position buffer with a uniform grid of points using the layout produced by <see cref="CalculatePointGrid"/>.
        /// </summary>
        /// <param name="outputBuffer">The destination position buffer, sized for the grid instance count.</param>
        /// <param name="worldBounds">The world bounds of the biome as x, y origin and z, w size.</param>
        /// <param name="spacing">The distance between adjacent points in world meters along each axis.</param>
        /// <param name="lowerLeftPoint">The world position of the lower left grid point.</param>
        /// <param name="gridDimension">The number of grid points along the x and y axes.</param>
        /// <remarks>
        /// The dispatch covers both grid axes in a single pass, so no multi pass loop is needed. The buffer size limit is
        /// reached before the group count along either axis would overflow.
        /// </remarks>
        public static void GeneratePoints(ComputeBuffer outputBuffer, Vector4 worldBounds, Vector2 spacing, Vector2 lowerLeftPoint, Vector2Int gridDimension)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(PointsUtils.COMPUTE_SHADER_NAME);
            shader.SetBuffer(PointsUtils.KERNEL_INDEX, PointsUtils.POSITIONS_BUFFER, outputBuffer);
            shader.SetVector(PointsUtils.LOWER_LEFT_POINT, new Vector4(lowerLeftPoint.x, lowerLeftPoint.y));
            shader.SetVector(PointsUtils.SPACING, spacing);
            shader.SetVector(PointsUtils.BIOME_BOUNDS, worldBounds);
            shader.SetVector(PointsUtils.GRID_DIMENSION, new Vector4(gridDimension.x, gridDimension.y));
            shader.Dispatch(PointsUtils.KERNEL_INDEX, (gridDimension.x + 7) / 8, (gridDimension.y + 7) / 8, 1);
            Resources.UnloadAsset(shader);
        }

        private class SpreadUtils
        {
            /// <summary>
            /// Resource path of the compute shader that spreads each source point into several jittered copies.
            /// </summary>
            public static readonly string COMPUTE_SHADER_NAME = "Vista/Shaders/Graph/Spread";
            /// <summary>
            /// Shader property ID for the source position buffer.
            /// </summary>
            public static readonly int SRC_BUFFER = Shader.PropertyToID("_SrcBuffer");
            /// <summary>
            /// Shader property ID for the destination position buffer.
            /// </summary>
            public static readonly int DEST_BUFFER = Shader.PropertyToID("_DestBuffer");
            /// <summary>
            /// Shader property ID for the optional distance map that scales the spread radius per point.
            /// </summary>
            public static readonly int DISTANCE_MAP = Shader.PropertyToID("_DistanceMap");
            /// <summary>
            /// Shader property ID for the base spread distance.
            /// </summary>
            public static readonly int DISTANCE = Shader.PropertyToID("_Distance");
            /// <summary>
            /// Shader property ID for the number of copies spread from each source point.
            /// </summary>
            public static readonly int COUNT = Shader.PropertyToID("_Count");
            /// <summary>
            /// Shader property ID for the random seed vector.
            /// </summary>
            public static readonly int SEED = Shader.PropertyToID("_Seed");
            /// <summary>
            /// Shader property ID for the running base index used to split large work loads across passes.
            /// </summary>
            public static readonly int BASE_INDEX = Shader.PropertyToID("_BaseIndex");
            /// <summary>
            /// Shader property ID for the number of source instances.
            /// </summary>
            public static readonly int SRC_INSTANCE_COUNT = Shader.PropertyToID("_SrcInstanceCount");
            /// <summary>
            /// Kernel index used by the spread compute shader.
            /// </summary>
            public static readonly int KERNEL_INDEX = 0;
            /// <summary>
            /// Keyword that keeps the original source points in the output in addition to the spread copies.
            /// </summary>
            public static readonly string KW_KEEP_SOURCE_POINTS = "KEEP_SOURCE_POINTS";
            /// <summary>
            /// Keyword that enables per point spread scaling from the distance map.
            /// </summary>
            public static readonly string KW_HAS_DISTANCE_MAP = "HAS_DISTANCE_MAP";
        }

        /// <summary>
        /// Spreads each source point into a number of jittered copies and writes them to a destination buffer.
        /// </summary>
        /// <param name="sourceBuffer">The source position buffer.</param>
        /// <param name="destinationBuffer">The destination position buffer, sized for source count times count plus one.</param>
        /// <param name="distanceMap">Optional map that scales the spread radius per point. Null disables the scaling.</param>
        /// <param name="distance">The base spread distance in normalized units.</param>
        /// <param name="count">The number of copies spread from each source point.</param>
        /// <param name="keepSourcePoints">Whether the original source points are also written to the output.</param>
        /// <param name="seed">The node level seed.</param>
        /// <param name="baseSeed">The graph level seed, combined with the node seed to randomize the result.</param>
        /// <remarks>
        /// The random seed vector consumes four sequential samples from a seeded generator in a fixed order, so the result
        /// is deterministic for a given seed pair. Large work loads are split into multiple passes with a running base index.
        /// </remarks>
        public static void Spread(ComputeBuffer sourceBuffer, ComputeBuffer destinationBuffer, Texture distanceMap, float distance, int count, bool keepSourcePoints, int seed, int baseSeed)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(SpreadUtils.COMPUTE_SHADER_NAME);
            shader.SetBuffer(SpreadUtils.KERNEL_INDEX, SpreadUtils.SRC_BUFFER, sourceBuffer);
            shader.SetBuffer(SpreadUtils.KERNEL_INDEX, SpreadUtils.DEST_BUFFER, destinationBuffer);
            shader.SetFloat(SpreadUtils.DISTANCE, distance);
            if (distanceMap != null)
            {
                shader.SetTexture(SpreadUtils.KERNEL_INDEX, SpreadUtils.DISTANCE_MAP, distanceMap);
                shader.EnableKeyword(SpreadUtils.KW_HAS_DISTANCE_MAP);
            }
            else
            {
                shader.DisableKeyword(SpreadUtils.KW_HAS_DISTANCE_MAP);
            }
            if (keepSourcePoints)
            {
                shader.EnableKeyword(SpreadUtils.KW_KEEP_SOURCE_POINTS);
            }
            else
            {
                shader.DisableKeyword(SpreadUtils.KW_KEEP_SOURCE_POINTS);
            }
            shader.SetInt(SpreadUtils.COUNT, count);

            int sourceInstanceCount = sourceBuffer.count / PositionSample.SIZE;
            shader.SetInt(SpreadUtils.SRC_INSTANCE_COUNT, sourceInstanceCount);

            System.Random random = new System.Random(seed ^ baseSeed);
            Vector4 randomSeed = new Vector4((float)(10 * random.NextDouble()), (float)(-10 * random.NextDouble()), (float)(20 * random.NextDouble()), (float)(-20 * random.NextDouble()));
            shader.SetVector(SpreadUtils.SEED, randomSeed);

            int totalThreadGroupX = (sourceInstanceCount + THREAD_PER_GROUP - 1) / THREAD_PER_GROUP;
            int iteration = (totalThreadGroupX + MAX_THREAD_GROUP_1D - 1) / MAX_THREAD_GROUP_1D;
            for (int i = 0; i < iteration; ++i)
            {
                int threadGroupX = Mathf.Min(MAX_THREAD_GROUP_1D, totalThreadGroupX);
                totalThreadGroupX -= MAX_THREAD_GROUP_1D;
                int baseIndex = i * MAX_THREAD_GROUP_1D * THREAD_PER_GROUP;
                shader.SetInt(SpreadUtils.BASE_INDEX, baseIndex);
                shader.Dispatch(SpreadUtils.KERNEL_INDEX, threadGroupX, 1, 1);
            }
            Resources.UnloadAsset(shader);
        }

        private class ThinOutUtils
        {
            /// <summary>
            /// Resource path of the compute shader that removes points based on a mask.
            /// </summary>
            public static readonly string COMPUTE_SHADER_NAME = "Vista/Shaders/Graph/ThinOut";
            /// <summary>
            /// Shader property ID for the source position buffer.
            /// </summary>
            public static readonly int POSITION_INPUT = Shader.PropertyToID("_PositionInput");
            /// <summary>
            /// Shader property ID for the destination position buffer.
            /// </summary>
            public static readonly int POSITION_OUTPUT = Shader.PropertyToID("_PositionOutput");
            /// <summary>
            /// Shader property ID for the mask that drives the removal probability.
            /// </summary>
            public static readonly int MASK = Shader.PropertyToID("_Mask");
            /// <summary>
            /// Shader property ID for the mask multiplier.
            /// </summary>
            public static readonly int MASK_MULTIPLIER = Shader.PropertyToID("_MaskMultiplier");
            /// <summary>
            /// Shader property ID for the random seed vector.
            /// </summary>
            public static readonly int SEED = Shader.PropertyToID("_Seed");
            /// <summary>
            /// Shader property ID for the running base index used to split large work loads across passes.
            /// </summary>
            public static readonly int BASE_INDEX = Shader.PropertyToID("_BaseIndex");
            /// <summary>
            /// Shader property ID for the number of instances.
            /// </summary>
            public static readonly int INSTANCE_COUNT = Shader.PropertyToID("_InstanceCount");
            /// <summary>
            /// Kernel index used by the thin out compute shader.
            /// </summary>
            public static readonly int KERNEL_INDEX = 0;
            /// <summary>
            /// Keyword that enables mask driven removal.
            /// </summary>
            public static readonly string KW_HAS_MASK = "HAS_MASK";
        }

        /// <summary>
        /// Removes points from a set using a mask, where a blacker mask value gives a higher chance of removal.
        /// </summary>
        /// <param name="inputBuffer">The source position buffer.</param>
        /// <param name="outputBuffer">The destination position buffer, sized to match the input.</param>
        /// <param name="maskTexture">Optional mask that drives the removal probability. Null keeps every point.</param>
        /// <param name="maskMultiplier">Scales the mask value before it is used to decide removal.</param>
        /// <param name="seed">The node level seed.</param>
        /// <param name="baseSeed">The graph level seed, combined with the node seed to randomize the result.</param>
        /// <remarks>
        /// The output buffer keeps the same instance count as the input. Removed points are flagged in place rather than
        /// compacted, so downstream consumers skip them. Large work loads are split into multiple passes with a running base index.
        /// </remarks>
        public static void ThinOut(ComputeBuffer inputBuffer, ComputeBuffer outputBuffer, Texture maskTexture, float maskMultiplier, int seed, int baseSeed)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(ThinOutUtils.COMPUTE_SHADER_NAME);
            shader.SetBuffer(ThinOutUtils.KERNEL_INDEX, ThinOutUtils.POSITION_OUTPUT, outputBuffer);
            shader.SetBuffer(ThinOutUtils.KERNEL_INDEX, ThinOutUtils.POSITION_INPUT, inputBuffer);

            shader.SetFloat(ThinOutUtils.MASK_MULTIPLIER, maskMultiplier);
            if (maskTexture != null)
            {
                shader.SetTexture(ThinOutUtils.KERNEL_INDEX, ThinOutUtils.MASK, maskTexture);
                shader.EnableKeyword(ThinOutUtils.KW_HAS_MASK);
            }
            else
            {
                shader.DisableKeyword(ThinOutUtils.KW_HAS_MASK);
            }

            System.Random random = new System.Random(seed ^ baseSeed);
            shader.SetVector(ThinOutUtils.SEED, new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()));

            int instanceCount = inputBuffer.count / PositionSample.SIZE;
            shader.SetInt(ThinOutUtils.INSTANCE_COUNT, instanceCount);
            int totalThreadGroupX = (instanceCount + THREAD_PER_GROUP - 1) / THREAD_PER_GROUP;
            int iteration = (totalThreadGroupX + MAX_THREAD_GROUP_1D - 1) / MAX_THREAD_GROUP_1D;
            for (int i = 0; i < iteration; ++i)
            {
                int threadGroupX = Mathf.Min(MAX_THREAD_GROUP_1D, totalThreadGroupX);
                totalThreadGroupX -= MAX_THREAD_GROUP_1D;
                int baseIndex = i * MAX_THREAD_GROUP_1D * THREAD_PER_GROUP;
                shader.SetInt(ThinOutUtils.BASE_INDEX, baseIndex);
                shader.Dispatch(ThinOutUtils.KERNEL_INDEX, threadGroupX, 1, 1);
            }
            Resources.UnloadAsset(shader);
        }
    }
}
#endif


