using AssetImport;

internal static class ImportTextureChecks
{
    internal static void Run(Action<bool,string> check)
    {
        string path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");
        byte[] source={137,80,78,71,13,10,26,10};
        File.WriteAllBytes(path,source);
        try
        {
            var lut=CubeLut.Read(new StringReader("LUT_3D_SIZE 2\n"+string.Join("\n",from b in Enumerable.Range(0,2) from g in Enumerable.Range(0,2) from r in Enumerable.Range(0,2) select $"{1-r} {1-g} {1-b}")));
            var diffuse=new TexturePath {Path=path,Type=Assimp.TextureType.Diffuse};
            var normal=new TexturePath {Path=path,Type=Assimp.TextureType.Normals};
            var loader=new ImportTextureData(lut);
            KKAPI.Utilities.TextureUtils.Calls=0;
            var converted=loader.Read(diffuse);
            var expected=TgaDecoder.EncodePng(new TgaDecoder.Image { Width=1, Height=2, Pixels=new byte[]{245,235,225,40,155,105,55,0} });
            check(converted.SequenceEqual(expected),"production pipeline converts RGB to PNG with row order/alpha intact");
            check(KKAPI.Utilities.TextureUtils.Last.destroyed,"temporary decoded Unity texture released");
            check(ReferenceEquals(converted,loader.Read(diffuse)) && KKAPI.Utilities.TextureUtils.Calls==1,"shared material texture converted once per import");
            check(loader.Read(normal).SequenceEqual(source) && KKAPI.Utilities.TextureUtils.Calls==1,"same file used as a normal map bypasses LUT and converted cache");
            check(new ImportTextureData(null).Read(diffuse).SequenceEqual(source),"conversion OFF bypasses Unity decoding and preserves input bytes");
            check(File.ReadAllBytes(path).SequenceEqual(source),"source texture remains unchanged");
        }
        finally { File.Delete(path); }
    }
}

namespace AssetImport
{
    internal sealed class TexturePath { internal string Path; internal Assimp.TextureType Type; }
}
namespace UnityEngine
{
    public enum TextureFormat { RGBA32 }
    public struct Color32 { public byte r,g,b,a; }
    public sealed class Texture2D:Object
    {
        public int width=1,height=2;
        public Color32[] GetPixels32()=>new[]{new Color32 {r=10,g=20,b=30,a=40},new Color32 {r=100,g=150,b=200,a=0}};
    }
}
namespace KKAPI.Utilities
{
    internal static class TextureUtils
    {
        internal static int Calls;
        internal static UnityEngine.Texture2D Last;
        public static UnityEngine.Texture2D LoadTexture(byte[] bytes,UnityEngine.TextureFormat format,bool option)
        {
            if(format!=UnityEngine.TextureFormat.RGBA32 || option) throw new Exception("Unexpected texture decode options");
            Calls++;return Last=new UnityEngine.Texture2D();
        }
    }
}
