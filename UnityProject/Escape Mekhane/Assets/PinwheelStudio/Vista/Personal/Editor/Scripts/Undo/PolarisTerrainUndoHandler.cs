#if VISTA && GRIFFIN
using System;
using System.Collections.Generic;
using Pinwheel.Griffin;
using Pinwheel.Griffin.BackupTool;
using Pinwheel.Vista;
using Pinwheel.Vista.PolarisTerrain;
using UnityEditor;
using UnityEngine;
#if __MICROSPLAT_POLARIS__
using JBooth.MicroSplat;
#endif

namespace Pinwheel.VistaEditor.TerrainUndo
{
    /// <summary>
    /// Observes terrain edit sessions and bridges Polaris backups to Unity Undo and Redo.
    /// </summary>
    [InitializeOnLoad]
    public static class PolarisTerrainUndoHandler
    {
        private sealed class Session
        {
            public readonly string intent;
            public readonly List<GStylizedTerrain> candidateTerrains;
            public bool initialBackupAttempted;
#if __MICROSPLAT_POLARIS__
            public readonly Dictionary<TextureArrayConfig, int> microSplatConfigEntryCounts;
#endif

            public Session(string intent, List<GStylizedTerrain> candidateTerrains)
            {
                this.intent = intent;
                this.candidateTerrains = candidateTerrains;
#if __MICROSPLAT_POLARIS__
                microSplatConfigEntryCounts = new Dictionary<TextureArrayConfig, int>();
#endif
            }
        }

        private static readonly Dictionary<Guid, Session> s_sessions = new Dictionary<Guid, Session>();
        private static readonly HashSet<string> s_vistaBackupNames = new HashSet<string>();
#if __MICROSPLAT_POLARIS__
        private static readonly HashSet<TextureArrayConfig> s_vistaMicroSplatConfigs =
            new HashSet<TextureArrayConfig>();
#endif

        static PolarisTerrainUndoHandler()
        {
            TerrainEditSession.began += OnBegin;
            TerrainEditSession.beforeWrite += OnBeforeWrite;
            TerrainEditSession.ended += OnEnd;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private static void OnBegin(Guid sessionId, string intent, IReadOnlyList<ITile> tiles)
        {
            try
            {
                List<GStylizedTerrain> terrains = GetTerrains(tiles);
                if (terrains.Count > 0)
                    s_sessions.Add(sessionId, new Session(intent, terrains));
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
                if (!(tile is PolarisTile polarisTile) ||
                    !s_sessions.TryGetValue(sessionId, out Session session))
                    return;

                if (!session.initialBackupAttempted)
                {
                    session.initialBackupAttempted = true;
                    string backupName = GBackupInternal.TryCreateAndMergeInitialBackup(
                        session.intent,
                        session.candidateTerrains,
                        GCommon.AllResourceFlags,
                        false);
                    RememberVistaBackup(backupName);
                }

#if __MICROSPLAT_POLARIS__
                RecordMicroSplatConfig(session, polarisTile);
#endif
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
                List<GStylizedTerrain> changedTerrains = GetTerrains(tiles);
                if (changedTerrains.Count == 0)
                    return;

                string backupName = GBackupInternal.TryCreateAndMergeBackup(
                    session.intent,
                    changedTerrains,
                    GCommon.AllResourceFlags,
                    false);
                RememberVistaBackup(backupName);
#if __MICROSPLAT_POLARIS__
                CompileChangedMicroSplatConfigs(session);
#endif
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

        private static void OnUndoRedo()
        {
            try
            {
                string backupName = GUndoCompatibleBuffer.Instance.CurrentBackupName;
                if (IsVistaBackup(backupName))
                {
                    GBackup.Restore(backupName);
#if __MICROSPLAT_POLARIS__
                    CompileVistaMicroSplatConfigs();
#endif
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void RememberVistaBackup(string backupName)
        {
            if (!string.IsNullOrEmpty(backupName))
                s_vistaBackupNames.Add(backupName);
        }

        private static bool IsVistaBackup(string backupName)
        {
            return !string.IsNullOrEmpty(backupName) && s_vistaBackupNames.Contains(backupName);
        }

#if __MICROSPLAT_POLARIS__
        private static void RecordMicroSplatConfig(Session session, PolarisTile tile)
        {
            GStylizedTerrain terrain = tile.terrain;
            if (terrain == null ||
                terrain.TerrainData == null ||
                terrain.TerrainData.Shading.ShadingSystem != GShadingSystem.MicroSplat)
                return;

            TextureArrayConfig config = terrain.TerrainData.Shading.MicroSplatTextureArrayConfig;
            if (config == null || session.microSplatConfigEntryCounts.ContainsKey(config))
                return;

            Undo.RegisterCompleteObjectUndo(config, session.intent);
            session.microSplatConfigEntryCounts.Add(config, config.sourceTextures.Count);
        }

        private static void CompileChangedMicroSplatConfigs(Session session)
        {
            foreach (KeyValuePair<TextureArrayConfig, int> item in session.microSplatConfigEntryCounts)
            {
                TextureArrayConfig config = item.Key;
                if (config == null || config.sourceTextures.Count == item.Value)
                    continue;

                TextureArrayConfigEditor.CompileConfig(config);
                EditorUtility.SetDirty(config);
                s_vistaMicroSplatConfigs.Add(config);
            }
        }

        private static void CompileVistaMicroSplatConfigs()
        {
            s_vistaMicroSplatConfigs.RemoveWhere(IsNullMicroSplatConfig);
            foreach (TextureArrayConfig config in s_vistaMicroSplatConfigs)
            {
                TextureArrayConfigEditor.CompileConfig(config);
            }
        }

        private static bool IsNullMicroSplatConfig(TextureArrayConfig config)
        {
            return config == null;
        }
#endif

        private static List<GStylizedTerrain> GetTerrains(IReadOnlyList<ITile> tiles)
        {
            List<GStylizedTerrain> terrains = new List<GStylizedTerrain>();
            for (int i = 0; i < tiles.Count; ++i)
            {
                if (tiles[i] is PolarisTile tile &&
                    tile.terrain != null &&
                    tile.terrain.TerrainData != null)
                {
                    terrains.Add(tile.terrain);
                }
            }
            return terrains;
        }
    }
}
#endif
