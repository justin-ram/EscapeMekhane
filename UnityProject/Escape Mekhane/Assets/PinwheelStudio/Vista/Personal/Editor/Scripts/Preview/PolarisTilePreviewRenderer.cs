#if VISTA && GRIFFIN
using System;
using Pinwheel.Griffin;
using Pinwheel.Vista;
using Pinwheel.Vista.PolarisTerrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Pinwheel.VistaEditor.Preview
{
    /// <summary>
    /// Draws world-space mask previews over the current Polaris terrain chunk meshes.
    /// </summary>
    [InitializeOnLoad]
    public sealed class PolarisTilePreviewRenderer : ITilePreviewRenderer
    {
        private const string SHADER_NAME = "Hidden/Vista/Preview/PolarisTilePreview";
        private const int PREVIEW_PASS = 0;

        private static readonly int MASK = Shader.PropertyToID("_Mask");
        private static readonly int MASK_BOUNDS = Shader.PropertyToID("_MaskBounds");
        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int ANIMATED = Shader.PropertyToID("_Animated");
        private static readonly PolarisTilePreviewRenderer s_instance;

        private readonly MaterialPropertyBlock m_properties = new MaterialPropertyBlock();
        private Material m_material;

        static PolarisTilePreviewRenderer()
        {
            s_instance = new PolarisTilePreviewRenderer();
            TilePreviewRenderers.Register<PolarisTile>(s_instance);
        }

        private PolarisTilePreviewRenderer()
        {
        }

        /// <inheritdoc/>
        public void Draw(Camera camera, Bounds maskBoundsWS, Texture mask, Color color, bool animated = false)
        {
            if (camera == null)
            {
                Debug.LogError("Cannot draw a Polaris tile preview without a camera.");
                return;
            }
            if (mask == null)
            {
                Debug.LogError("Cannot draw a Polaris tile preview without a mask.");
                return;
            }
            if (maskBoundsWS.size.x <= 0 || maskBoundsWS.size.z <= 0)
                return;

            Material material = GetMaterial();
            if (material == null)
                return;

            m_properties.Clear();
            m_properties.SetTexture(MASK, mask);
            m_properties.SetVector(
                MASK_BOUNDS,
                new Vector4(maskBoundsWS.min.x, maskBoundsWS.min.z, maskBoundsWS.size.x, maskBoundsWS.size.z));
            m_properties.SetColor(COLOR, color);
            m_properties.SetFloat(ANIMATED, animated ? 1f : 0f);

            foreach (ITile tile in TileRegistry.activeTiles)
            {
                if (!(tile is PolarisTile polarisTile) || !Utilities.OverlapXZ(tile.worldBounds, maskBoundsWS))
                    continue;

                GStylizedTerrain terrain = polarisTile.terrain;
                if (terrain == null || terrain.TerrainData == null)
                    continue;

                DrawTerrain(camera, terrain, maskBoundsWS, material);
            }
        }

        private void DrawTerrain(Camera camera, GStylizedTerrain terrain, Bounds maskBoundsWS, Material material)
        {
            Bounds terrainBounds = terrain.Bounds;
            Rect previewRect = Rect.MinMaxRect(
                Utilities.InverseLerpUnclamped(terrainBounds.min.x, terrainBounds.max.x, maskBoundsWS.min.x),
                Utilities.InverseLerpUnclamped(terrainBounds.min.z, terrainBounds.max.z, maskBoundsWS.min.z),
                Utilities.InverseLerpUnclamped(terrainBounds.min.x, terrainBounds.max.x, maskBoundsWS.max.x),
                Utilities.InverseLerpUnclamped(terrainBounds.min.z, terrainBounds.max.z, maskBoundsWS.max.z));

            GTerrainChunk[] chunks = terrain.GetChunks();
            for (int i = 0; i < chunks.Length; ++i)
            {
                GTerrainChunk chunk = chunks[i];
                if (chunk == null || !chunk.GetUvRange().Overlaps(previewRect))
                    continue;

                Mesh mesh = chunk.GetMesh(0);
                if (mesh == null)
                    continue;

                Utilities.DrawMeshCompat(
                    mesh,
                    chunk.transform.localToWorldMatrix,
                    material,
                    chunk.gameObject.layer,
                    camera,
                    PREVIEW_PASS,
                    m_properties,
                    ShadowCastingMode.Off,
                    false,
                    null,
                    LightProbeUsage.Off);
            }
        }

        private Material GetMaterial()
        {
            if (m_material == null)
            {
                Shader shader = Shader.Find(SHADER_NAME);
                if (shader == null)
                {
                    Debug.LogError($"Cannot find shader '{SHADER_NAME}'.");
                    return null;
                }

                m_material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }
            return m_material;
        }
    }
}
#endif
