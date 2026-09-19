#if VISTA
using System;
using System.Collections.Generic;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Publishes the lifecycle of terrain-edit operations to interested subsystems.
    /// </summary>
    public static class TerrainEditSession
    {
        /// <summary>
        /// Represents the start of an edit session.
        /// </summary>
        /// <param name="sessionId">Unique identifier for this edit operation.</param>
        /// <param name="intent">Stable category describing what the client intends to do.</param>
        /// <param name="tiles">Complete set of tiles the operation may change.</param>
        public delegate void BeginHandler(Guid sessionId, string intent, IReadOnlyList<ITile> tiles);

        /// <summary>
        /// Represents a notification immediately before or after a session first writes to a tile.
        /// </summary>
        public delegate void TileWriteHandler(Guid sessionId, ITile tile);

        /// <summary>
        /// Represents the end of an edit session.
        /// </summary>
        /// <param name="sessionId">Identifier returned by <see cref="Begin"/>.</param>
        /// <param name="tiles">Tiles that were actually changed by the operation.</param>
        public delegate void EndHandler(Guid sessionId, IReadOnlyList<ITile> tiles);

        /// <summary>Occurs when a client declares a new terrain-edit operation.</summary>
        public static event BeginHandler began;
        /// <summary>Occurs immediately before a session first writes to a tile.</summary>
        public static event TileWriteHandler beforeWrite;
        /// <summary>Occurs after a session successfully writes to a tile.</summary>
        public static event TileWriteHandler afterWrite;
        /// <summary>Occurs when a session finalizes its authoritative changed-tile set.</summary>
        public static event EndHandler ended;

        private sealed class State
        {
            public readonly HashSet<ITile> candidateTiles;
            public readonly HashSet<ITile> preparedTiles;
            public readonly HashSet<ITile> changedTiles;

            public State(IEnumerable<ITile> tiles)
            {
                candidateTiles = new HashSet<ITile>(tiles);
                preparedTiles = new HashSet<ITile>();
                changedTiles = new HashSet<ITile>();
            }
        }

        private static readonly Dictionary<Guid, State> s_sessions = new Dictionary<Guid, State>();

        /// <summary>
        /// Begins a terrain-edit session without changing or capturing tile data.
        /// </summary>
        /// <param name="intent">Stable category describing what the client intends to do.</param>
        /// <param name="tiles">Complete set of non-null tiles the operation may change.</param>
        /// <returns>A unique identifier to pass to the remaining session methods.</returns>
        public static Guid Begin(string intent, IReadOnlyList<ITile> tiles)
        {
            if (string.IsNullOrWhiteSpace(intent))
                throw new ArgumentException("An edit intent is required.", nameof(intent));
            ValidateTiles(tiles, nameof(tiles));

            Guid sessionId = Guid.NewGuid();
            State state = new State(tiles);
            s_sessions.Add(sessionId, state);
            try
            {
                began?.Invoke(sessionId, intent, CopyTiles(tiles));
            }
            catch
            {
                s_sessions.Remove(sessionId);
                throw;
            }
            return sessionId;
        }

        /// <summary>
        /// Announces that the session is about to write to a tile for the first time.
        /// Repeated calls for the same tile are ignored.
        /// </summary>
        public static void BeforeWrite(Guid sessionId, ITile tile)
        {
            State state = GetState(sessionId);
            ValidateCandidate(state, tile);
            if (state.preparedTiles.Add(tile))
            {
                beforeWrite?.Invoke(sessionId, tile);
            }
        }

        /// <summary>
        /// Announces that the session successfully changed a prepared tile.
        /// Repeated calls for the same tile are ignored.
        /// </summary>
        public static void AfterWrite(Guid sessionId, ITile tile)
        {
            State state = GetState(sessionId);
            ValidateCandidate(state, tile);
            if (!state.preparedTiles.Contains(tile))
                throw new InvalidOperationException("BeforeWrite must be called before AfterWrite for a tile.");
            if (state.changedTiles.Add(tile))
            {
                afterWrite?.Invoke(sessionId, tile);
            }
        }

        /// <summary>
        /// Ends the session with the authoritative set of tiles that actually changed.
        /// </summary>
        public static void End(Guid sessionId, IReadOnlyList<ITile> tiles)
        {
            State state = GetState(sessionId);
            ValidateTiles(tiles, nameof(tiles));
            HashSet<ITile> actualTiles = new HashSet<ITile>(tiles);
            if (!actualTiles.IsSubsetOf(state.candidateTiles))
                throw new ArgumentException("Every changed tile must belong to the session's candidate set.", nameof(tiles));
            if (!actualTiles.SetEquals(state.changedTiles))
                throw new ArgumentException("The changed tile set must match the tiles reported through AfterWrite.", nameof(tiles));

            s_sessions.Remove(sessionId);
            ended?.Invoke(sessionId, CopyTiles(tiles));
        }

        private static State GetState(Guid sessionId)
        {
            if (!s_sessions.TryGetValue(sessionId, out State state))
                throw new ArgumentException("The terrain edit session does not exist or has already ended.", nameof(sessionId));
            return state;
        }

        private static void ValidateCandidate(State state, ITile tile)
        {
            if (tile == null)
                throw new ArgumentNullException(nameof(tile));
            if (!state.candidateTiles.Contains(tile))
                throw new ArgumentException("The tile does not belong to this session.", nameof(tile));
        }

        private static void ValidateTiles(IReadOnlyList<ITile> tiles, string parameterName)
        {
            if (tiles == null)
                throw new ArgumentNullException(parameterName);
            for (int i = 0; i < tiles.Count; ++i)
            {
                if (tiles[i] == null)
                    throw new ArgumentException("A terrain edit session cannot contain null tiles.", parameterName);
            }
        }

        private static ITile[] CopyTiles(IReadOnlyList<ITile> tiles)
        {
            ITile[] copy = new ITile[tiles.Count];
            for (int i = 0; i < tiles.Count; ++i)
            {
                copy[i] = tiles[i];
            }
            return copy;
        }
    }
}
#endif
