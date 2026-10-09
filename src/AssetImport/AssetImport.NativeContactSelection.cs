using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssetImport
{
    internal static class NativeContactSelection
    {
        internal static DynamicBone Owner(Transform bone, DynamicBone[] chains)
        {
            DynamicBone owner = null;
            foreach (var chain in chains ?? new DynamicBone[0])
                if (chain && chain.m_Root && bone && bone.IsChildOf(chain.m_Root) &&
                    (!owner || chain.m_Root.IsChildOf(owner.m_Root))) owner = chain;
            return owner;
        }

        internal static NativeContactShape[] Resolve(NativeContactDefinition source, DynamicBone[] chains, PhysicsEnvironmentSettings settings)
        {
            if (source == null) return new NativeContactShape[0];
            var result = new List<NativeContactShape>(); var seen = new HashSet<Transform>();
            Add(source.Breasts, false, chains, settings, result, seen);
            Add(source.ManualCandidates, true, chains, settings, result, seen);
            result.Sort((a, b) => Depth(a.Bone).CompareTo(Depth(b.Bone)));
            FitMeshRegions(result, settings);
            foreach (var shape in result)
            {
                var choice = Preference(settings, shape.ChainRoot);
                shape.Radius = choice.BreastRadiusOverride > 0 ? choice.BreastRadiusOverride : shape.Radius * choice.BreastRadiusScale;
                var offset = new Vector3(choice.BreastOffsetX, choice.BreastOffsetY, choice.BreastOffsetZ);
                shape.Center += offset; shape.ReferenceCenter += offset;
            }
            return result.ToArray();
        }

        private static PhysicsChainSettings Preference(PhysicsEnvironmentSettings settings, string root)
        {
            var choice = settings == null ? null : settings.ForRoot(root);
            return choice == null ? new PhysicsChainSettings() : choice.Copy();
        }

        // Collapse a deforming subtree to its highest selected bone, within
        // one chain. Driving every small child independently can pin the skin
        // back to the input pose even when its parent has been displaced.
        private static void FitMeshRegions(List<NativeContactShape> shapes, PhysicsEnvironmentSettings settings)
        {
            for (int i = 0; i < shapes.Count; i++)
            {
                var root = shapes[i];
                if (!Preference(settings, root.ChainRoot).BreastMeshVolume) continue;
                bool found = false; var min = Vector3.zero; var max = Vector3.zero;
                var members = new List<int>();
                for (int j = i; j < shapes.Count; j++)
                {
                    var part = shapes[j];
                    if (part.ChainRoot != root.ChainRoot || !part.Bone.IsChildOf(root.Bone)) continue;
                    members.Add(j);
                    if (!part.HasMeshBounds) continue;
                    if (!found) { min = part.MeshMin; max = part.MeshMax; found = true; }
                    else
                    {
                        min = new Vector3(Math.Min(min.x, part.MeshMin.x), Math.Min(min.y, part.MeshMin.y), Math.Min(min.z, part.MeshMin.z));
                        max = new Vector3(Math.Max(max.x, part.MeshMax.x), Math.Max(max.y, part.MeshMax.y), Math.Max(max.z, part.MeshMax.z));
                    }
                }
                var size = max - min;
                float radius = .5f * Math.Max(size.x, Math.Max(size.y, size.z));
                if (!found || !PhysicsEnvironmentMath.Finite(radius) || radius <= .000001f) continue;
                var center = (min + max) * .5f;
                // Imported PMX rest bones have identity rotation; subtract the
                // original reference center to move the local center by the
                // same amount, independent of the current animation pose.
                root.Center += center - root.ReferenceCenter;
                root.ReferenceCenter = center; root.Radius = radius; root.Height = 0;
                root.SourceBody = "Weighted mesh region (" + members.Count + " bones)";
                root.MeshMin = min; root.MeshMax = max; root.HasMeshBounds = true;
                for (int k = members.Count - 1; k > 0; k--) shapes.RemoveAt(members[k]);
            }
        }

        private static void Add(NativeContactShape[] shapes, bool manualOnly, DynamicBone[] chains, PhysicsEnvironmentSettings settings,
            List<NativeContactShape> result, HashSet<Transform> seen)
        {
            foreach (var shape in shapes ?? new NativeContactShape[0])
            {
                if (shape == null || !shape.Bone || seen.Contains(shape.Bone)) continue;
                var owner = Owner(shape.Bone, chains);
                var preference = settings == null || !owner ? null : settings.ForRoot(owner.m_Root.name);
                var clean = preference == null ? new PhysicsChainSettings() : preference.Copy();
                if (clean.BreastRole == (int)BreastContactRole.Exclude ||
                    (manualOnly && clean.BreastRole != (int)BreastContactRole.Breast)) continue;
                seen.Add(shape.Bone);
                result.Add(new NativeContactShape { Bone = shape.Bone, Center = shape.Center, Rotation = shape.Rotation,
                    Radius = shape.Radius, Height = shape.Height, Direction = shape.Direction,
                    SourceBody = shape.SourceBody, HasReferencePose = shape.HasReferencePose, ReferenceCenter = shape.ReferenceCenter,
                    HasMeshBounds = shape.HasMeshBounds, MeshMin = shape.MeshMin, MeshMax = shape.MeshMax,
                    Selection = clean.BreastRole == (int)BreastContactRole.Breast ? "manual" : "automatic",
                    ChainRoot = owner ? owner.m_Root.name : "" });
            }
        }
        private static int Depth(Transform bone)
        { int depth = 0; for (; bone; bone = bone.parent) depth++; return depth; }
    }
}
