using System;
using MessagePack;

namespace AssetImport
{
    internal enum PhysicsChainMode { Inherit, KeepShape, FreeHanging, Animation }

    [Serializable, MessagePackObject]
    public sealed class PhysicsChainSettings
    {
        [Key("Root")] public string Root;
        [Key("Mode")] public int Mode;
        [Key("ForceScale")] public float ForceScale = 1f;
        [Key("ShapeRetention")] public float ShapeRetention = 1f;

        internal PhysicsChainSettings Copy()
        {
            return new PhysicsChainSettings
            {
                Root = Root ?? "",
                Mode = Mode >= 0 && Mode <= 3 ? Mode : 0,
                ForceScale = Clean(ForceScale, 0f, 4f, 1f),
                ShapeRetention = Clean(ShapeRetention, 0f, 1f, 1f)
            };
        }
        private static float Clean(float value, float min, float max, float fallback)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(min, Math.Min(max, value)); }
    }
}
