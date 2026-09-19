#if VISTA
using System;

namespace Pinwheel.VistaEditor.Wizard
{
    /// <summary>
    /// A terrain grid specification: a grid of columns x rows square tiles, each widthLength metres wide and
    /// height metres tall. One type for three roles, the built-in presets, the user saved configs (persisted
    /// by <see cref="TerrainGridConfigs"/>), and the input to terrain creation. A serializable class with
    /// mutable fields so JsonUtility can round-trip it. The backend (Polaris or Unity) is deliberately not
    /// part of the config, it is chosen separately, so a saved config works with either. There is no stored
    /// name, the display label is derived from the values (<see cref="DisplayLabel"/>), and identity is by
    /// value (<see cref="Equals(object)"/>), so configs dedupe and remove on their dimensions.
    /// </summary>
    [Serializable]
    public class TerrainGridConfig
    {
        public int columns;
        public int rows;
        public float widthLength;
        public float height;

        public TerrainGridConfig() { }

        public TerrainGridConfig(int columns, int rows, float widthLength, float height)
        {
            this.columns = columns;
            this.rows = rows;
            this.widthLength = widthLength;
            this.height = height;
        }

        /// <summary>The label shown on its chip, derived from the values, e.g. "2x2 · WL500 H500".</summary>
        public string DisplayLabel => $"{columns}x{rows} · WL{widthLength:0} H{height:0}";

        public override bool Equals(object obj)
        {
            return obj is TerrainGridConfig other
                && columns == other.columns
                && rows == other.rows
                && widthLength == other.widthLength
                && height == other.height;
        }

        public override int GetHashCode()
        {
            return (columns, rows, widthLength, height).GetHashCode();
        }
    }
}
#endif
