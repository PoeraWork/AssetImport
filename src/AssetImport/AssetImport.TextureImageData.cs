using System;
using System.IO;

namespace AssetImport
{
    internal static class TextureImageData
    {
        internal static byte[] Read(string path)
        {
            byte[] data=File.ReadAllBytes(path);
            TgaDecoder.Image decoded;
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".tga": decoded = TgaDecoder.Decode(data); break;
                case ".bmp": decoded = BmpDecoder.Decode(data); break;
                case ".dds": decoded = DdsDecoder.Decode(data); break;
                default: return data;
            }
            // ME receives PNG and stores it in the scene/card. No ME patch required.
            return TgaDecoder.EncodePng(decoded);
        }
    }
}
