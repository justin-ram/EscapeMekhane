#if VISTA
using System;
using System.Collections.Generic;

namespace Pinwheel.Vista
{
    /// <summary>
    /// Tracks enabled terrain tiles across all registered Vista terrain backends.
    /// </summary>
    public static class TileRegistry
    {
        private static readonly HashSet<ITile> s_activeTiles = new HashSet<ITile>();

        /// <summary>
        /// Gets all terrain tiles currently registered as active.
        /// </summary>
        public static IEnumerable<ITile> activeTiles => s_activeTiles;

        /// <summary>
        /// Registers an enabled terrain tile.
        /// </summary>
        /// <param name="tile">Tile entering its active lifecycle.</param>
        /// <returns>True when the tile was newly registered.</returns>
        public static bool Register(ITile tile)
        {
            if (tile == null)
                throw new ArgumentNullException(nameof(tile));

            return s_activeTiles.Add(tile);
        }

        /// <summary>
        /// Removes a terrain tile from the active collection.
        /// </summary>
        /// <param name="tile">Tile leaving its active lifecycle.</param>
        /// <returns>True when the tile was registered and removed.</returns>
        public static bool Unregister(ITile tile)
        {
            if (tile == null)
                return false;

            return s_activeTiles.Remove(tile);
        }
    }
}
#endif
