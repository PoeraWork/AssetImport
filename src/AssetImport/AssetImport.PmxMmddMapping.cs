using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LitJson;

namespace AssetImport
{
    internal sealed class PmxMorphTarget
    {
        internal string Renderer, Shape;
    }
    internal sealed class PmxMmddResult
    {
        internal string MappingJson, TargetsJson, ReportJson, ModelId;
        internal int BoneCount, MorphCount, IkCount;
        internal string[] Warnings;
    }
    internal static class PmxMmddMapping
    {
        private static Dictionary<string, object> Obj(params object[] values)
        {
            var d = new Dictionary<string, object>();
            for (int i = 0; i < values.Length; i += 2) d.Add((string)values[i], values[i + 1]);
            return d;
        }
        internal static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
        // The runtime extension applies PMX per-axis limits only to our bones.
        private static string Key(int i) { return "assetimport_pmx_" + i.ToString("D4"); }
        private static bool HasGrant(PmxModel.Bone b) { return (b.Flags & 0x300) != 0 && b.GrantParent >= 0 && b.GrantWeight != 0; }
        internal static PmxMmddResult Build(PmxModel model, byte[] source, string label, IEnumerable<PmxMorphTarget> morphTargets,
            IEnumerable<int> dynamicRoots)
        {
            // Keep physics out of MMDD's transform writes. Include user-selected
            // roots as well as PMX physics metadata so editors cannot fight MMDD.
            var excluded = new HashSet<int>(model.PhysicsBones);
            // Append followers of a PMX-simulated bone cannot be evaluated by
            // MMDD's virtual skeleton. Keep that whole physics-owned branch out
            // of animation writes instead of rejecting unrelated body/morph data.
            bool expanded;
            do
            {
                expanded = false;
                for (int i = 0; i < model.Bones.Length; i++)
                {
                    var b = model.Bones[i];
                    if (excluded.Contains(b.Parent) || (HasGrant(b) && excluded.Contains(b.GrantParent)))
                        expanded |= excluded.Add(i);
                }
            } while (expanded);
            var physicsAppend = Enumerable.Range(0, model.Bones.Length)
                .Where(i => HasGrant(model.Bones[i]) && excluded.Contains(model.Bones[i].GrantParent)).ToArray();
            excluded.UnionWith(dynamicRoots);
            bool changed;
            do
            {
                changed = false;
                for (int i = 0; i < model.Bones.Length; i++)
                {
                    var b = model.Bones[i];
                    if (excluded.Contains(b.Parent) || ((b.Flags & 0x20) != 0 &&
                        (excluded.Contains(b.IkTarget) || b.IkLinks.Any(l => excluded.Contains(l.Bone)))))
                        changed |= excluded.Add(i);
                }
            } while (changed);
            var pending = new HashSet<int>(Enumerable.Range(0, model.Bones.Length).Where(i => !excluded.Contains(i)));
            var dependencies = new Dictionary<int, HashSet<int>>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var warnings = new List<string>();
            foreach (int i in physicsAppend)
                warnings.Add("Physics append follower excluded from MMDD (Dynamic Bone does not simulate PMX append grants): " + model.Bones[i].Name);
            foreach (int i in pending)
            {
                var b = model.Bones[i]; var dep = new HashSet<int>();
                if (string.IsNullOrEmpty(b.Name) || !names.Add(b.Name))
                    throw new FormatException("MMDD auto mapping needs unique nonempty motion bone names: " + b.Name);
                if ((b.Flags & 0x2000) != 0) throw new FormatException("MMDD auto mapping does not support PMX external-parent transforms: " + b.Name);
                if (b.Parent >= 0) dep.Add(b.Parent);
                if ((b.Flags & 0x300) != 0 && b.GrantParent == -1)
                    warnings.Add("Ignored append flag without a source bone: " + b.Name);
                if (HasGrant(b))
                {
                    if ((b.Flags & 0x280) != 0)
                        throw new FormatException("MMDD auto mapping does not support position/local append transforms: " + b.Name);
                    dep.Add(b.GrantParent);
                }
                if ((b.Flags & 0x20) != 0)
                {
                    if (b.IkTarget < 0 || b.IkIterations < 1 || b.IkIterations > 10000 || b.IkAngle <= 0 || b.IkLinks.Count == 0)
                        throw new FormatException("Invalid/unsupported PMX IK: " + b.Name);
                    dep.Add(b.IkTarget); foreach (var l in b.IkLinks) dep.Add(l.Bone);
                    foreach (var l in b.IkLinks.Where(l => l.Limited))
                        if (l.Min.X > l.Max.X || l.Min.Y > l.Max.Y || l.Min.Z > l.Max.Z)
                            throw new FormatException("Inverted PMX IK limits: " + b.Name);
                }
                if (dep.Any(d => d < 0 || !pending.Contains(d)))
                    throw new FormatException("Animation bone depends on a physics/excluded bone: " + b.Name + ". Select only hair/skirt roots, not head/waist/body roots.");
                dependencies.Add(i, dep);
            }
            var ordered = new List<int>(); var done = new HashSet<int>();
            while (pending.Count > 0)
            {
                var ready = pending.Where(i => dependencies[i].IsSubsetOf(done)).OrderBy(i => model.Bones[i].Layer).ThenBy(i => i).ToList();
                if (ready.Count == 0) throw new FormatException("Cyclic PMX animation dependencies.");
                int next = ready[0]; pending.Remove(next); done.Add(next); ordered.Add(next);
            }
            var definitions = new List<object>(); int ikCount = 0;
            foreach (int i in ordered)
            {
                var b = model.Bones[i]; string target = PmxModel.BoneName(i, b.Name);
                var d = Obj("name", Key(i), "vmdName", b.Name, "group", "PMX bones",
                    "positionData", Obj("mode", "LOCATE_AT_TARGET", "target", target),
                    "movable", (b.Flags & 4) != 0, "rotatable", (b.Flags & 2) != 0,
                    "weightBone", target, "weightSyncMode", "GLOBAL",
                    "adjustment", Obj("rotAdjAuto", false, "rotAxisAuto", false));
                if (b.Parent >= 0) d["parent"] = Key(b.Parent);
                if (HasGrant(b) && (b.Flags & 0x100) != 0)
                { d["isTrack"] = true; d["trackData"] = new object[] { "r", b.GrantWeight, Key(b.GrantParent) }; d["rotatable"] = true; }
                if ((b.Flags & 0x400) != 0)
                {
                    var axis = new[] { -b.FixedAxis.X, b.FixedAxis.Y, -b.FixedAxis.Z };
                    d["isLimitAxis"] = true; d["limitAxisData"] = new[] { axis, axis };
                }
                if ((b.Flags & 0x20) != 0)
                {
                    var links = new List<string> { Key(b.IkTarget) }; var limits = new List<object>();
                    foreach (var l in b.IkLinks)
                    {
                        links.Add(Key(l.Bone));
                        if (!l.Limited) { limits.Add(null); continue; }
                        var min = new[] { -l.Min.X, l.Min.Y, -l.Min.Z }; var max = new[] { -l.Max.X, l.Max.Y, -l.Max.Z };
                        limits.Add(Enumerable.Range(0, 3).Select(k => new[] { Math.Min(min[k], max[k]) * 180 / Math.PI, Math.Max(min[k], max[k]) * 180 / Math.PI }).ToArray());
                    }
                    d["isIK"] = true; d["ikData"] = links;
                    d["ikSolverSetting"] = Obj("name", Key(i), "enable", true, "solver", "MMDIKSolver", "iterations", b.IkIterations,
                        "stepAngle", b.IkAngle * 180 / Math.PI, "limits", limits); ikCount++;
                }
                definitions.Add(d);
            }
            var targets = new Dictionary<string, object>(); var morphs = new List<object>();
            if (definitions.Count == 0) throw new FormatException("No motion bones remain. Select only hair/skirt physics roots, not the entire skeleton.");
            // Use actual converted renderers, including every KK mesh chunk.
            var targetGroups = morphTargets.GroupBy(t => t.Shape).ToList();
            foreach (var group in targetGroups)
            {
                var funcs = new List<object>();
                foreach (string renderer in group.Select(t => t.Renderer).Distinct())
                {
                    // MMDD retains bound get/set methods on actors. Namespace this
                    // list-valued bridge so old 4.2.1 scalar methods cannot win.
                    string key = "v2_" + Hash(Encoding.UTF8.GetBytes(renderer + "\0" + group.Key)).Substring(0, 16);
                    targets[key] = new[] { renderer, group.Key };
                    funcs.Add(new object[] { "get_assetimport_" + key, "set_assetimport_" + key, "range", 0, 100 });
                }
                morphs.Add(Obj("name", "morph_" + morphs.Count.ToString("D4"), "vmdName", group.Key,
                    "group", "PMX vertex morphs", "enable", true, "type", "FUNC", "funcList", funcs));
            }
            string id = Hash(source).Substring(0, 12);
            // Physics-root and BlendShape selections need independent definitions.
            string variant = Hash(Encoding.UTF8.GetBytes("runtime-v3\n" + string.Join(",", excluded.OrderBy(i => i).Select(i => i.ToString()).ToArray()) + "\n" + JsonMapper.ToJson(targets))).Substring(0, 8);
            id += "_" + variant;
            var mapping = Obj("general", Obj("description", label + " / AssetImport PMX Auto / " + id,
                "boneType", "ITEM", "enableAutoRotAdj", false, "overwriteDefaultBlend", false), "bones", definitions, "morphs", morphs);
            var report = Obj("mapped_bones", definitions.Count, "ik_solvers", ikCount, "mapped_morphs", morphs.Count,
                "source_sha256", Hash(source), "model_id", id, "morph_targets", targets, "warnings", warnings,
                "morph_extension", "MmddExtMorphFunc_AssetImport_Pmx_" + id + ".py",
                "excluded_from_mmdd", excluded.OrderBy(i => i).Select(i => PmxModel.BoneName(i, model.Bones[i].Name)).ToArray(),
                "unit_scale", .0832, "windows_game", "NOT RUN", "unmapped_morphs", model.Morphs.Where(m => !targetGroups.Any(g => g.Key == m.Name)).Select(m => m.Name).ToArray());
            return new PmxMmddResult { MappingJson = JsonMapper.ToJson(mapping), TargetsJson = JsonMapper.ToJson(targets),
                ReportJson = JsonMapper.ToJson(report), ModelId = id, BoneCount = definitions.Count, MorphCount = morphs.Count, IkCount = ikCount, Warnings = warnings.ToArray() };
        }
    }
}
