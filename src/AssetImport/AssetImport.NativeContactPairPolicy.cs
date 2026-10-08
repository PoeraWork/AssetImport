using System;
using UnityEngine;

namespace AssetImport
{
    internal static class NativeContactPairPolicy
    {
        // A volume carried by this breast cannot be an independent obstacle.
        // A supporting ancestor that overlaps it in the PMX reference pose is
        // part of the authored rest arrangement, not an incoming contact.
        // Independent branches (including arms) remain eligible even if the
        // user enables contact while they already touch the breast.
        internal static string Exclusion(NativeContactShape breast, NativeContactShape obstacle)
        {
            if (!breast.Bone || !obstacle.Bone) return "missing-bone";
            if (obstacle.Bone.IsChildOf(breast.Bone)) return "moves-with-breast";
            if (!breast.Bone.IsChildOf(obstacle.Bone)) return null;
            if (!breast.HasReferencePose || !obstacle.HasReferencePose) return "ancestor-without-reference-pose";
            float gap = ReferenceGap(breast, obstacle);
            if (!PhysicsEnvironmentMath.Finite(gap)) return "invalid-reference-geometry";
            return gap < 0 ? "ancestor-overlaps-reference-pose" : null;
        }

        internal static float ReferenceGap(NativeContactShape breast, NativeContactShape obstacle)
        {
            return Gap(breast.ReferenceCenter, breast.Radius, obstacle.ReferenceCenter, obstacle.Rotation,
                obstacle.Radius, obstacle.Height, obstacle.Direction);
        }
        internal static float Gap(Vector3 center, float radius, Vector3 obstacleCenter, Quaternion rotation,
            float obstacleRadius, float height, int direction)
        {
            var delta = center - obstacleCenter;
            var localAxis = direction == 0 ? new Vector3(1, 0, 0) : direction == 2 ? new Vector3(0, 0, 1) : new Vector3(0, 1, 0);
            var axis = rotation * localAxis;
            float along = delta.x * axis.x + delta.y * axis.y + delta.z * axis.z;
            float halfSegment = Math.Max(0, height * .5f - obstacleRadius);
            return (delta - axis * Math.Max(-halfSegment, Math.Min(halfSegment, along))).magnitude - radius - obstacleRadius;
        }
    }
}
