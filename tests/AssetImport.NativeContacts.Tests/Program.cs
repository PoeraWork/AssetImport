using System.Reflection;
using AssetImport;
using UnityEngine;

int checks = 0;
void Check(bool value, string why) { checks++; if (!value) throw new Exception(why); }
void Invoke(NativeContactController c, string name) => typeof(NativeContactController).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(c, null);
Transform Child(string name, Transform parent, Vector3 local)
{ var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = local; return t; }

foreach (string target in new[] { "KK", "KKS" })
{
    using var game = new MetadataAssembly(Path.Combine(AppContext.BaseDirectory, "Fixtures", target, "Assembly-CSharp.dll"));
    foreach (string name in new[] { "InitNodeParticle", "InitLocalPosition", "SetupParticles", "InitTransforms", "ApplyParticlesToTransforms", "ResetParticlesPosition" })
        Check(!game.RequireMethod("DynamicBone_Ver02", name).IsNil, target + " native method " + name);
    Check(!game.RequireMethod("DynamicBone_Ver02", "setPtn", "System.Int32", "System.Boolean").IsNil, target + " pattern API");
    Check(!game.RequireMethod("DynamicBone_Ver02", "UpdateDynamicBones", "System.Single").IsNil, target + " update API");
    foreach (var type in new[] { typeof(DynamicBone_Ver02), typeof(DynamicBone_Ver02.BoneParameter), typeof(DynamicBone_Ver02.BonePtn), typeof(DynamicBone_Ver02.Particle) })
    {
        var real = game.Reader.GetTypeDefinition(game.FindType(type.FullName!));
        var fields = real.GetFields().Select(h => game.Reader.GetString(game.Reader.GetFieldDefinition(h).Name)).ToHashSet();
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            Check(fields.Contains(field.Name), target + " data field " + type.Name + "." + field.Name);
    }
}

foreach (float scale in new[] { .1f, 1f, 3f })
{
    var host = new GameObject(); var model = Child("model", host.transform, new()); model.localScale = new(scale, scale, scale);
    var torso = Child("torso", model, new(0, 1, 0)); var breast = Child("breast", torso, new(0, 0, .2f));
    var arm = Child("arm", torso, new(.3f, 0, .2f));
    var ordinary = host.AddComponent<DynamicBone>(); ordinary.m_Root = breast;
    var environment = host.AddComponent<AssetPhysicsEnvironment>(); environment.Chains = [ordinary];
    var definition = new NativeContactDefinition { Model = model,
        Breasts = [new() { Bone = breast, Radius = .1f }], Obstacles = [new() { Bone = arm, Radius = .05f, Height = .2f }] };
    NativeContactController.Attach(host, definition); var controller = host.GetComponent<NativeContactController>();
    Invoke(controller, "OnEnable");
    Vector3 original = breast.position; int beforeInstances = DynamicBone_Ver02.Instances;
    controller.Tick(1f / 60);
    Check(DynamicBone_Ver02.Instances == beforeInstances && breast.position.sqrMagnitude == original.sqrMagnitude, "default off has no proxy or pose mutation");
    environment.Settings.NativeBreastContacts = true;
    DynamicBone_Ver02.Output = new(.02f * scale, 0, 0);
    controller.Tick(1f / 60);
    Check((breast.position - original - DynamicBone_Ver02.Output).magnitude < 1e-6f, "native proxy output is applied in world units");
    Check(ordinary.enabled && ordinary.m_Weight == 1 && ordinary.m_Root == breast, "ordinary physics is not disabled or reconfigured");
    controller.Restore(); Check((breast.position - original).magnitude < 1e-6f, "original physics output restored before next animation");
    for (int i = 0; i < 120; i++) controller.Tick(1f / 60);
    Check((breast.position - original - DynamicBone_Ver02.Output).magnitude < 1e-6f, "contact offsets never accumulate on the original skeleton");
    breast.localPosition += new Vector3(0, .03f, 0); var edit = breast.localPosition;
    controller.Restore(); Check((breast.localPosition - edit).sqrMagnitude < 1e-12f, "post-render user edit is preserved");
    original = breast.position;
    int steps = DynamicBone_Ver02.Steps;
    Time.frameCount++; Invoke(controller, "Update"); Camera.Render();
    Check(DynamicBone_Ver02.Steps == steps, "camera before LateUpdate cannot capture incomplete animation");
    Invoke(controller, "LateUpdate"); Camera.Render(); Camera.Render();
    Check(DynamicBone_Ver02.Steps == steps + 1, "multiple cameras advance once per frame");
    AssetPhysicsEnvironment.Reset(ordinary);
    Check((breast.position - original).magnitude < 1e-6f, "reset immediately restores the input pose");
    ordinary.enabled = false; steps = DynamicBone_Ver02.Steps; controller.Tick(1f / 60);
    Check(DynamicBone_Ver02.Steps == steps, "physics-off cannot add contacts");
    ordinary.enabled = true;
    environment.Settings.EditRoot("breast").Mode = (int)PhysicsChainMode.Animation;
    controller.Tick(1f / 60); Check(DynamicBone_Ver02.Steps == steps, "no-simulation override prevents contact motion");
    environment.Settings.Chains = [];
    controller.ResetContacts(); DynamicBone_Ver02.FailSetup = true;
    controller.Tick(1f / 60);
    Check(controller.Status.Contains("unavailable") && ordinary.enabled && (breast.position - original).magnitude < 1e-6f, "missing native movable particles fail without touching ordinary physics");
    beforeInstances = DynamicBone_Ver02.Instances; controller.Tick(1f / 60);
    Check(DynamicBone_Ver02.Instances == beforeInstances, "failed engine does not retry each frame");
    DynamicBone_Ver02.FailSetup = false; controller.ResetContacts(); DynamicBone_Ver02.FailStep = true;
    controller.Tick(1f / 60);
    Check(controller.Status.Contains("unavailable") && (breast.position - original).magnitude < 1e-6f, "native update exception leaves original pose intact");
    DynamicBone_Ver02.FailStep = false; controller.ResetContacts(); DynamicBone_Ver02.Output = new(float.NaN, 0, 0);
    controller.Tick(1f / 60);
    Check(controller.Status.Contains("unavailable") && (breast.position - original).magnitude < 1e-6f, "nonfinite native output cannot contaminate the model");
    controller.ResetContacts(); DynamicBone_Ver02.FailCalibration = true;
    controller.Tick(1f / 60);
    Check(controller.Status.Contains("unavailable") && ordinary.enabled, "a native engine that never responds to the validation sphere is rejected before pose writeback");
    DynamicBone_Ver02.FailCalibration = false;
    controller.ResetContacts(); DynamicBone_Ver02.Output = new(.02f * scale, 0, 0); controller.Tick(1f / 60);
    environment.Settings.NativeBreastContacts = false; controller.Tick(1f / 60);
    Check((breast.position - original).magnitude < 1e-6f && ordinary.enabled, "disabling the feature restores ordinary physics immediately");
    Invoke(controller, "OnDisable");
    steps = DynamicBone_Ver02.Steps; Time.frameCount++; Camera.Render(); Check(DynamicBone_Ver02.Steps == steps, "disabled controller unsubscribes camera callbacks");
}

var asset = new Asset(); Check(!PhysicsEnvironmentPersistence.Read(asset).NativeBreastContacts, "old scenes default off");
var preference = new PhysicsEnvironmentSettings { NativeBreastContacts = true };
PhysicsEnvironmentPersistence.Write(asset, preference);
Check(PhysicsEnvironmentPersistence.Read(asset).NativeBreastContacts && preference.Copy().NativeBreastContacts, "opt-in survives scene/card setting copies");
Console.WriteLine($"PASS: {checks} native contact adapter lifecycle and KK/KKS metadata checks. Collision output was injected; game physics and visual behavior are NOT tested.");
