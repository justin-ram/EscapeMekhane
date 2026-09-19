#if VISTA
using System;
using System.Collections.Generic;
using Pinwheel.Vista;
using Pinwheel.Vista.UnityTerrain;
using UnityEditor;
using UnityEngine;

namespace Pinwheel.VistaEditor.TerrainUndo
{
    /// <summary>
    /// Observes terrain edit sessions and records Unity Terrain changes with Unity's native Undo system.
    /// </summary>
    [InitializeOnLoad]
    public static class UnityTerrainUndoHandler
    {
        private sealed class Session
        {
            public readonly int undoGroup;
            public readonly string intent;

            public Session(int undoGroup, string intent)
            {
                this.undoGroup = undoGroup;
                this.intent = intent;
            }
        }

        private static readonly Dictionary<Guid, Session> s_sessions = new Dictionary<Guid, Session>();

        static UnityTerrainUndoHandler()
        {
            TerrainEditSession.began += OnBegin;
            TerrainEditSession.beforeWrite += OnBeforeWrite;
            TerrainEditSession.ended += OnEnd;
        }

        private static void OnBegin(Guid sessionId, string intent, IReadOnlyList<ITile> tiles)
        {
            try
            {
                if (!ContainsUnityTerrainTile(tiles))
                    return;

                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName(intent);
                s_sessions.Add(sessionId, new Session(undoGroup, intent));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void OnBeforeWrite(Guid sessionId, ITile tile)
        {
            try
            {
                if (!(tile is TerrainTile terrainTile) ||
                    !s_sessions.TryGetValue(sessionId, out Session session))
                    return;

                TerrainData terrainData = terrainTile.terrain != null ? terrainTile.terrain.terrainData : null;
                if (terrainData == null)
                    return;

                Texture2D[] alphamapTextures = terrainData.alphamapTextures;
                UnityEngine.Object[] objects = new UnityEngine.Object[1 + alphamapTextures.Length];
                objects[0] = terrainData;
                for (int i = 0; i < alphamapTextures.Length; ++i)
                {
                    objects[i + 1] = alphamapTextures[i];
                }
                Undo.RegisterCompleteObjectUndo(objects, session.intent);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void OnEnd(Guid sessionId, IReadOnlyList<ITile> tiles)
        {
            if (!s_sessions.TryGetValue(sessionId, out Session session))
                return;

            try
            {
                Undo.CollapseUndoOperations(session.undoGroup);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                s_sessions.Remove(sessionId);
            }
        }

        private static bool ContainsUnityTerrainTile(IReadOnlyList<ITile> tiles)
        {
            for (int i = 0; i < tiles.Count; ++i)
            {
                if (tiles[i] is TerrainTile)
                    return true;
            }
            return false;
        }
    }
}
#endif
