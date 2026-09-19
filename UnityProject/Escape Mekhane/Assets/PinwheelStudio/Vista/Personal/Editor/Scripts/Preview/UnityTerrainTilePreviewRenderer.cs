#if VISTA
using System;
using Pinwheel.Vista;
using Pinwheel.Vista.UnityTerrain;
using UnityEditor;
using UnityEditor.TerrainTools;
using UnityEngine;
using UnityEngine.TerrainTools;

namespace Pinwheel.VistaEditor.Preview
{
    /// <summary>
    /// Draws mask previews over Vista Unity Terrain tiles using Unity's terrain paint-preview path.
    /// </summary>
    [InitializeOnLoad]
    public sealed class UnityTerrainTilePreviewRenderer : ITilePreviewRenderer
    {
        private const string SHADER_NAME = "Hidden/Vista/Preview/UnityTerrainTilePreview";
        private const int PREVIEW_PASS = 0;

        private static readonly int COLOR = Shader.PropertyToID("_Color");
        private static readonly int ANIMATED = Shader.PropertyToID("_Animated");
        private static readonly UnityTerrainTilePreviewRenderer s_instance;

        private Material m_material;

        static UnityTerrainTilePreviewRenderer()
        {
            s_instance = new UnityTerrainTilePreviewRenderer();
            TilePreviewRenderers.Register<TerrainTile>(s_instance);
        }

        private UnityTerrainTilePreviewRenderer()
        {
        }

        /// <inheritdoc/>
        public void Draw(Camera camera, Bounds maskBoundsWS, Texture mask, Color color, bool animated = false)
        {
            if (camera == null)
            {
                Debug.LogError("Cannot draw a Unity Terrain tile preview without a camera.");
                return;
            }
            if (mask == null)
            {
                Debug.LogError("Cannot draw a Unity Terrain tile preview without a mask.");
                return;
            }
            if (maskBoundsWS.size.x <= 0 || maskBoundsWS.size.z <= 0)
                return;

            try
            {
                Terrain originTerrain = FindFirstOverlappingTerrain(maskBoundsWS);
                if (originTerrain != null)
                    Draw(originTerrain, mask, maskBoundsWS, color, animated);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void Draw(Terrain originTerrain, Texture mask, Bounds maskBoundsWS, Color color, bool animated)
        {
            Rect boundsInTerrainSpace = GetBoundsInTerrainSpace(originTerrain, maskBoundsWS);
            BrushTransform brushTransform = new BrushTransform(
                boundsInTerrainSpace.min,
                new Vector2(boundsInTerrainSpace.width, 0),
                new Vector2(0, boundsInTerrainSpace.height));

            PaintContext paintContext = TerrainPaintUtility.BeginPaintHeightmap(
                originTerrain,
                brushTransform.GetBrushXYBounds());
            if (paintContext == null)
                return;

            try
            {
                Material material = GetMaterial();
                if (material == null)
                    return;

                material.SetColor(COLOR, color);
                material.SetFloat(ANIMATED, animated ? 1 : 0);
                TerrainPaintUtilityEditor.DrawBrushPreview(
                    paintContext,
                    TerrainBrushPreviewMode.SourceRenderTexture,
                    mask,
                    brushTransform,
                    material,
                    PREVIEW_PASS);
            }
            finally
            {
                TerrainPaintUtility.ReleaseContextResources(paintContext);
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

        private static Terrain FindFirstOverlappingTerrain(Bounds maskBoundsWS)
        {
            foreach (ITile tile in TileRegistry.activeTiles)
            {
                if (!(tile is TerrainTile terrainTile))
                    continue;

                Terrain terrain = terrainTile.terrain;
                if (terrain != null && terrain.terrainData != null && Utilities.OverlapXZ(tile.worldBounds, maskBoundsWS))
                {
                    return terrain;
                }
            }
            return null;
        }

        private static Rect GetBoundsInTerrainSpace(Terrain terrain, Bounds maskBoundsWS)
        {
            Vector3 terrainPositionWS = terrain.transform.position;
            return new Rect(
                maskBoundsWS.min.x - terrainPositionWS.x,
                maskBoundsWS.min.z - terrainPositionWS.z,
                maskBoundsWS.size.x,
                maskBoundsWS.size.z);
        }
    }
}
#endif
