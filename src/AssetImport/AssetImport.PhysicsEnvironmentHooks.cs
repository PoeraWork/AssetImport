using HarmonyLib;

namespace AssetImport
{
    internal static class PhysicsEnvironmentHooks
    {
        [HarmonyPostfix, HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.UpdateParticles1))]
        private static void Integrate(DynamicBone __instance) { AssetPhysicsEnvironment.AfterIntegrate(__instance); }

        [HarmonyPostfix, HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.UpdateParticles2))]
        private static void Constraints(DynamicBone __instance) { AssetPhysicsEnvironment.AfterConstraints(__instance); }

        [HarmonyPostfix, HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.SkipUpdateParticles))]
        private static void Skip(DynamicBone __instance) { AssetPhysicsEnvironment.AfterConstraints(__instance); }

        [HarmonyPostfix, HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.ResetParticlesPosition))]
        private static void Reset(DynamicBone __instance) { AssetPhysicsEnvironment.AfterReset(__instance); }

        [HarmonyPostfix, HarmonyPatch(typeof(DynamicBone), nameof(DynamicBone.SetupParticles))]
        private static void Setup(DynamicBone __instance) { AssetPhysicsEnvironment.AfterReset(__instance); }
    }
}
