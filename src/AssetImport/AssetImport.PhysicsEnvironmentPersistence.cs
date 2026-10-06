namespace AssetImport
{
    internal static class PhysicsEnvironmentPersistence
    {
        internal static PhysicsEnvironmentSettings Read(Asset asset)
        {
            return new PhysicsEnvironmentSettings { Floor = asset.FloorCollision, FloorOffset = asset.FloorOffset,
                DownwardForce = asset.DownwardForce, Settle = asset.SettlePhysics,
                NaturalDrape = asset.NaturalDrape ?? (asset.DownwardForce > 0f &&
                    !float.IsNaN(asset.DownwardForce) && !float.IsInfinity(asset.DownwardForce)) }.Copy();
        }

        internal static void Write(Asset asset, PhysicsEnvironmentSettings settings)
        {
            var clean = settings.Copy();
            asset.FloorCollision = clean.Floor;
            asset.FloorOffset = clean.FloorOffset;
            asset.DownwardForce = clean.DownwardForce;
            asset.SettlePhysics = clean.Settle;
            asset.NaturalDrape = clean.NaturalDrape;
        }
    }
}
