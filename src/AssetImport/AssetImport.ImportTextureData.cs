using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AssetImport
{
    internal sealed class ImportTextureData
    {
        private readonly CubeLut lut;
        private readonly Dictionary<string, byte[]> cache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        internal ImportTextureData(CubeLut lut) { this.lut = lut; }

        internal byte[] Read(TexturePath path)
        {
            bool convert = lut != null && path.Type == Assimp.TextureType.Diffuse;
            string key = Path.GetFullPath(path.Path) + (convert ? "|kk" : "|original");
            byte[] bytes;
            if (!cache.TryGetValue(key, out bytes))
                cache.Add(key, bytes = Read(path, convert ? lut : null));
            return bytes;
        }

        private static byte[] Read(TexturePath path, CubeLut lut)
        {
            byte[] bytes = TextureImageData.Read(path.Path);
            if (lut == null || path.Type != Assimp.TextureType.Diffuse) return bytes;
            Texture2D texture = KKAPI.Utilities.TextureUtils.LoadTexture(bytes, TextureFormat.RGBA32, false);
            if (!texture) throw new FormatException("Cannot decode image for KK color conversion.");
            try
            {
                Color32[] colors = texture.GetPixels32();
                byte[] pixels = new byte[checked(colors.Length * 4)];
                for (int i = 0; i < colors.Length; i++)
                {
                    pixels[i * 4] = colors[i].r; pixels[i * 4 + 1] = colors[i].g;
                    pixels[i * 4 + 2] = colors[i].b; pixels[i * 4 + 3] = colors[i].a;
                }
                lut.Apply(pixels);
                return TgaDecoder.EncodePng(new TgaDecoder.Image { Width = texture.width, Height = texture.height, Pixels = pixels });
            }
            finally { UnityEngine.Object.Destroy(texture); }
        }
    }
}
