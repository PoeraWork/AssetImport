using System;

namespace AssetImport
{
    [Serializable]
    internal sealed class PhysicsEnvironmentSettings
    {
        public bool Floor;
        public float FloorOffset;
        public float DownwardForce;
        public bool Settle;
        public bool NaturalDrape;

        internal static PhysicsEnvironmentSettings NewImport()
        {
            return new PhysicsEnvironmentSettings { Floor = true, DownwardForce = .001f, Settle = true, NaturalDrape = true };
        }

        internal PhysicsEnvironmentSettings Copy()
        {
            return new PhysicsEnvironmentSettings { Floor = Floor, FloorOffset = Clamp(FloorOffset, -100f, 100f),
                DownwardForce = Clamp(DownwardForce, 0f, .02f), Settle = Settle, NaturalDrape = NaturalDrape };
        }

        private static float Clamp(float value, float min, float max)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : Math.Max(min, Math.Min(max, value));
        }
    }
}
