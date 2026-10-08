using AssetImport;

internal static class NativeContactPlanChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var model = new PmxModel {
            Bones = [new() { Name = "上半身", Parent = -1 }, new() { Name = "左胸", Parent = 0 },
                new() { Name = "腕", Parent = 0 }, new() { Name = "左胸先", Parent = 1 }, new() { Name = "breast_ribbon", Parent = 0 }],
            Vertices = [new() { Bones = [1, 3, 4], Weights = [.5f, .3f, .2f] }],
            RigidBodyData = [Body(3, 1), Body(1, 1), Body(2, 0), Body(3, 0), Body(4, 1)] };
        var plan = PmxNativeContactPlan.Build(model);
        check(plan.Breasts.Select(s => s.Bone).SequenceEqual(new[] { 1, 3 }), "weighted breast bodies sorted parent before child, including leaf bone");
        check(plan.Obstacles.Count == 1 && plan.Obstacles[0].Bone == 2, "stable animated arm retained and dynamic descendants excluded");
        check(plan.Breasts.All(s => Math.Abs(s.Radius - .0416f) < 1e-6f), "contact radius uses existing PMX coordinate conversion");
        model.Vertices = []; check(PmxNativeContactPlan.Build(model).Breasts.Count == 0, "unweighted helper bones cannot be selected as deforming breasts");
        foreach (string name in new[] { "左胸", "右乳", "左胸上", "Breast_L", "Right Breast 01", "bust.r" })
            check(PmxNativeContactPlan.IsBreast(name), "recognized breast name " + name);
        foreach (string name in new[] { "上半身", "breast ribbon", "breastplate", "胸飾り", "left arm" })
            check(!PmxNativeContactPlan.IsBreast(name), "accessory/body names excluded " + name);
        check(PmxNativeContactPlan.Build(null).Breasts.Count == 0, "non-PMX input has no native contact plan");
    }
    private static PmxModel.RigidBody Body(int bone, int mode) => new() {
        Bone = bone, Mode = mode, Name = "body", Shape = 0, Size = new(.5f, .5f, .5f), Group = 0, CollisionMask = 0 };

    internal static void Corpus(PmxModel model, Action<bool, string> check)
    {
        var plan = PmxNativeContactPlan.Build(model);
        check(plan.Breasts.Select(s => s.Bone).Distinct().Count() == plan.Breasts.Count, "native contact plan owns each breast once");
        foreach (var breast in plan.Breasts)
        {
            check(model.RigidBodyData[breast.Index].Mode != 0 && PmxNativeContactPlan.IsBreast(model.Bones[breast.Bone].Name), "real-model native breast retains its source dynamic body");
            check(float.IsFinite(breast.Radius) && breast.Radius > 0, "real-model native breast radius is finite and positive");
        }
        foreach (var obstacle in plan.Obstacles)
            check(model.RigidBodyData[obstacle.Index].Mode == 0 && float.IsFinite(obstacle.Radius) && obstacle.Radius > 0, "real-model native obstacle is static and finite");
        Console.WriteLine($"Native contact geometry: breasts={plan.Breasts.Count}; obstacles={plan.Obstacles.Count}. Native game simulation NOT run.");
    }
}
