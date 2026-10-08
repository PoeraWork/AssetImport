using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssetImport
{
    public partial class Import
    {
        internal NativeContactDefinition BuildNativeContacts()
        {
            if (!IsPmx || !ImportBones || _pmx == null) return null;
            try
            {
                var plan = PmxNativeContactPlan.Build(_pmx);
                var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
                foreach (var bone in GameObject.GetComponentsInChildren<Transform>(true)) bones[bone.name] = bone;
                return new NativeContactDefinition { Model = GameObject.transform,
                    RecognitionReport = plan.Describe(_pmx),
                    Breasts = BindNativeShapes(plan.Breasts, bones), Obstacles = BindNativeShapes(plan.Obstacles, bones) };
            }
            catch (Exception error)
            {
                Logger.LogWarning("Optional breast contact metadata could not be prepared; ordinary import continues. " + error.Message);
                return null;
            }
        }

        private NativeContactShape[] BindNativeShapes(List<PmxBodyCollider> shapes, Dictionary<string, Transform> bones)
        {
            var result = new List<NativeContactShape>();
            foreach (var shape in shapes)
            {
                Transform bone;
                if (!bones.TryGetValue(PmxModel.BoneName(shape.Bone, _pmx.Bones[shape.Bone].Name), out bone))
                { Logger.LogWarning("PMX breast diagnostics: binding missing for bone=" + _pmx.Bones[shape.Bone].Name + "; body=" + shape.Index); continue; }
                var reference = _pmx.RigidBodyData[shape.Index].Position;
                result.Add(new NativeContactShape { Bone = bone, HasReferencePose = true,
                    ReferenceCenter = new Vector3(-reference.X * PmxCollisionPlan.UnitScale,
                        reference.Y * PmxCollisionPlan.UnitScale, -reference.Z * PmxCollisionPlan.UnitScale),
                    SourceBody = "[" + shape.Index + "]" + _pmx.RigidBodyData[shape.Index].Name,
                    Center = new Vector3(shape.LocalPosition.X, shape.LocalPosition.Y, shape.LocalPosition.Z),
                    Radius = shape.Radius, Height = shape.Height, Direction = shape.Direction,
                    Rotation = new Quaternion(shape.Rotation[0], shape.Rotation[1], shape.Rotation[2], shape.Rotation[3]) });
            }
            return result.ToArray();
        }
    }
}
