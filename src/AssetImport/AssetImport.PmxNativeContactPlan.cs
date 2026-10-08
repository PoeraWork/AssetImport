using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AssetImport
{
    // Collision geometry only. The game owns the simulation algorithm.
    internal sealed class PmxNativeContactPlan
    {
        internal readonly List<PmxBodyCollider> Breasts = new List<PmxBodyCollider>();
        internal readonly List<PmxBodyCollider> Obstacles = new List<PmxBodyCollider>();

        internal static PmxNativeContactPlan Build(PmxModel model)
        {
            var result = new PmxNativeContactPlan();
            if (model == null || model.Bones == null) return result;
            var bodies = model.RigidBodyData ?? new PmxModel.RigidBody[0];
            var moving = new HashSet<int>();
            foreach (var body in bodies) if (body != null && body.Mode != 0) moving.Add(body.Bone);
            var breastBones = new HashSet<int>();
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i]; PmxBodyCollider shape;
                if (body == null || body.Bone < 0 || body.Bone >= model.Bones.Length ||
                    !PmxCollisionPlan.TryCollider(body, i, model.Bones, out shape)) continue;
                if ((body.Mode == 1 || body.Mode == 2) && IsBreast(model.Bones[body.Bone].Name) &&
                    model.Bones[body.Bone].Parent >= 0 && Weighted(model, body.Bone) && breastBones.Add(body.Bone))
                {
                    // One spherical probe per deforming bone; no generated tail
                    // and no root-selection changes in the ordinary chain.
                    result.Breasts.Add(shape);
                }
                if (body.Mode != 0) continue;
                int ancestor = body.Bone, depth = 0;
                while (ancestor >= 0 && ancestor < model.Bones.Length && depth++ < model.Bones.Length && !moving.Contains(ancestor))
                    ancestor = model.Bones[ancestor].Parent;
                if (ancestor < 0) result.Obstacles.Add(shape);
            }
            result.Breasts.Sort((a, b) => Depth(a.Bone, model).CompareTo(Depth(b.Bone, model)));
            return result;
        }

        private static int Depth(int bone, PmxModel model)
        { int depth = 0; for (; bone >= 0 && bone < model.Bones.Length && depth < model.Bones.Length; bone = model.Bones[bone].Parent) depth++; return depth; }

        internal string Describe(PmxModel model)
        {
            var report = new StringBuilder();
            var bodies = model.RigidBodyData ?? new PmxModel.RigidBody[0];
            report.AppendLine("bones=" + model.Bones.Length + "; bodies=" + bodies.Length + "; selectedBreasts=" + Breasts.Count + "; selectedObstacles=" + Obstacles.Count);
            int matches = 0;
            for (int bone = 0; bone < model.Bones.Length; bone++)
            {
                if (!IsBreast(model.Bones[bone].Name)) continue;
                matches++;
                bool found = false;
                for (int i = 0; i < bodies.Length; i++)
                {
                    var body = bodies[i]; if (body == null || body.Bone != bone) continue;
                    found = true; PmxBodyCollider shape;
                    string reason = !Weighted(model, bone) ? "unweighted" : model.Bones[bone].Parent < 0 ? "no-parent" :
                        body.Mode != 1 && body.Mode != 2 ? "not-dynamic" : !PmxCollisionPlan.TryCollider(body, i, model.Bones, out shape) ? "invalid-shape" :
                        Breasts.Exists(s => s.Index == i) ? "selected" : "duplicate-bone";
                    report.AppendLine("breast bone=" + model.Bones[bone].Name + "; body[" + i + "]=" + body.Name + "; mode=" + body.Mode + "; result=" + reason);
                }
                if (!found) report.AppendLine("breast bone=" + model.Bones[bone].Name + "; result=no-rigid-body");
            }
            report.AppendLine("recognizedBoneNames=" + matches);
            // Include a bounded source inventory so unknown naming conventions
            // can be distinguished from absent physics metadata.
            int listed = 0;
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i]; if (body == null || body.Mode == 0) continue;
                if (listed++ >= 128) continue;
                string bone = body.Bone >= 0 && body.Bone < model.Bones.Length ? model.Bones[body.Bone].Name : "unbound";
                report.AppendLine("source dynamic body[" + i + "]=" + body.Name + "; bone=" + bone + "; mode=" + body.Mode);
            }
            if (listed > 128) report.AppendLine("Source inventory truncated: " + (listed - 128) + " further dynamic bodies.");
            return report.ToString();
        }

        internal static bool IsBreast(string name)
        {
            var key = Regex.Replace((name ?? "").Normalize().ToLowerInvariant(), @"[\s_\.\-0-9]", "");
            return Regex.IsMatch(key, @"^(?:左|右)(?:胸|乳)(?:上|下|先|先端)?$") ||
                Regex.IsMatch(key, @"^(?:left|right|l|r)?(?:breast|bust|boob)(?:left|right|l|r)?(?:tip|end)?$");
        }

        private static bool Weighted(PmxModel model, int bone)
        {
            foreach (var vertex in model.Vertices ?? new PmxModel.Vertex[0])
                if (vertex != null && vertex.Bones != null && vertex.Weights != null)
                    for (int i = 0; i < Math.Min(vertex.Bones.Length, vertex.Weights.Length); i++)
                        if (vertex.Bones[i] == bone && vertex.Weights[i] > .01f) return true;
            return false;
        }
    }
}
