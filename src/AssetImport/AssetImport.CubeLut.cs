using System;
using System.Globalization;
using System.IO;

namespace AssetImport
{
    // Adobe/Resolve .cube order: red changes fastest, then green, then blue.
    // Work in encoded RGB as Photoshop Color Lookup does; alpha is untouched.
    internal sealed class CubeLut
    {
        private int size;
        private float[] values;
        private readonly float[] min = { 0, 0, 0 }, max = { 1, 1, 1 };

        internal static CubeLut Read(TextReader reader)
        {
            var lut = new CubeLut();
            string line;
            int count = 0;
            bool hasMin = false, hasMax = false;
            while ((line = reader.ReadLine()) != null)
            {
                int comment = line.IndexOf('#');
                if (comment >= 0) line = line.Substring(0, comment);
                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || parts[0] == "TITLE") continue;
                if (parts[0] == "LUT_3D_SIZE")
                {
                    if (parts.Length != 2 || lut.values != null || !int.TryParse(parts[1], out lut.size) || lut.size < 2 || lut.size > 128)
                        throw new FormatException("LUT_3D_SIZE must be 2..128 and appear once.");
                    lut.values = new float[lut.size * lut.size * lut.size * 3];
                }
                else if (parts[0] == "DOMAIN_MIN" || parts[0] == "DOMAIN_MAX")
                {
                    bool isMin = parts[0] == "DOMAIN_MIN";
                    if (parts.Length != 4 || count != 0 || (isMin ? hasMin : hasMax)) throw new FormatException("Invalid LUT domain.");
                    if (isMin) hasMin = true; else hasMax = true;
                    for (int i = 0; i < 3; i++) (isMin ? lut.min : lut.max)[i] = Number(parts[i + 1]);
                }
                else
                {
                    if (parts.Length != 3 || lut.values == null || count + 3 > lut.values.Length)
                        throw new FormatException("Expected a 3D .cube LUT with RGB rows.");
                    for (int i = 0; i < 3; i++) lut.values[count++] = Number(parts[i]);
                }
            }
            if (lut.values == null || count != lut.values.Length) throw new FormatException("Incomplete 3D LUT.");
            for (int i = 0; i < 3; i++)
                if (lut.max[i] <= lut.min[i] || float.IsInfinity(lut.max[i] - lut.min[i]))
                    throw new FormatException("LUT domain must have finite positive width.");
            return lut;
        }

        private static float Number(string text)
        {
            float value;
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException("Non-finite or invalid LUT number.");
            return value;
        }

        private float Coordinate(byte value, int axis)
        {
            return Math.Max(0, Math.Min(1, (value / 255f - min[axis]) / (max[axis] - min[axis]))) * (size - 1);
        }

        internal void Apply(byte[] rgba)
        {
            if (rgba == null || rgba.Length % 4 != 0) throw new ArgumentException("Expected RGBA pixels.");
            for (int p = 0; p < rgba.Length; p += 4)
            {
                float r = Coordinate(rgba[p], 0), g = Coordinate(rgba[p + 1], 1), b = Coordinate(rgba[p + 2], 2);
                int x = Math.Min((int)r, size - 2), y = Math.Min((int)g, size - 2), z = Math.Min((int)b, size - 2);
                r -= x; g -= y; b -= z;
                for (int channel = 0; channel < 3; channel++)
                {
                    double result = 0;
                    for (int dz = 0; dz < 2; dz++)
                    for (int dy = 0; dy < 2; dy++)
                    for (int dx = 0; dx < 2; dx++)
                        result += values[(((z + dz) * size + y + dy) * size + x + dx) * 3 + channel]
                            * (dx == 0 ? 1 - r : r) * (dy == 0 ? 1 - g : g) * (dz == 0 ? 1 - b : b);
                    rgba[p + channel] = (byte)Math.Max(0, Math.Min(255, Math.Round(result * 255)));
                }
            }
        }
    }
}
