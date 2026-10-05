using System;
using System.Collections.Generic;
using MessagePack;

namespace AssetImport
{
    /// <summary>
    /// Representation of an Asset (Accessory/StudioItem) for saving.
    /// </summary>
    [MessagePackObject]
    public class Asset
    {
        [Key("SourceFile")]
        public string SourceFile { get; set; }
        [Key("BoneIndices")]
        public List<int> DynamicBoneIndices { get; set; }
        // Null is an older card: imported accessory physics was always enabled.
        // Studio uses its native itemInfo enableDynamicBone flag instead.
        [Key("PhysicsEnabled")]
        public bool? PhysicsEnabled { get; set; }
        // 0 (including old records) retains legacy defaults. Versioned presets
        // are applied only when building components, before external editors restore.
        [Key("PhysicsProfile")]
        public int PhysicsProfile { get; set; }
        [Key("PmxHairCollisionAssist")]
        public bool PmxHairCollisionAssist { get; set; }
        [Key("ObjectKey")]
        public int Identifier { get; set; } // in Studio: dictKey | on Character: AccessorySlot
        [Key("Scale")]
        public float[] Scale { get; set; } = new float[3];
        [Key("HasBones")]
        public bool HasBones { get; set; }
        [Key("perRendererMaterials")]
        public bool PerRendererMaterials { get; set; } = false;
        [Key("doFbxTranslation")]
        [Obsolete] public bool DoFbxTranslation { get; set; } = false;
    }
}
