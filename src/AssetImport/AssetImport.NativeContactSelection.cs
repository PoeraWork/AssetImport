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
            return result.ToArray();
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
                    Radius = shape.Radius * clean.BreastRadiusScale, Height = shape.Height, Direction = shape.Direction,
                    SourceBody = shape.SourceBody, HasReferencePose = shape.HasReferencePose, ReferenceCenter = shape.ReferenceCenter,
                    Selection = clean.BreastRole == (int)BreastContactRole.Breast ? "manual" : "automatic",
                    ChainRoot = owner ? owner.m_Root.name : "" });
            }
        }
        private static int Depth(Transform bone)
        { int depth = 0; for (; bone; bone = bone.parent) depth++; return depth; }
    }
}
