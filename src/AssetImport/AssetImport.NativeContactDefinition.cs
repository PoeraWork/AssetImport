using System;
using UnityEngine;

namespace AssetImport
{
    [Serializable]
    internal sealed class NativeContactShape
    {
        public Transform Bone;
        public Transform Driver, ContactRoot;
        public Vector3 Center;
        public Quaternion Rotation = new Quaternion(0, 0, 0, 1);
        public float Radius, Height;
        public int Direction;
        public string SourceBody;
        public bool HasReferencePose;
        public Vector3 ReferenceCenter;
        public string Selection, ChainRoot;
        public bool HasMeshBounds;
        public Vector3 MeshMin, MeshMax;
    }

    [Serializable]
    internal sealed class NativeContactDefinition
    {
        public Transform Model;
        public string RecognitionReport;
        public NativeContactShape[] Breasts = new NativeContactShape[0];
        public NativeContactShape[] ManualCandidates = new NativeContactShape[0];
        public NativeContactShape[] Obstacles = new NativeContactShape[0];
    }
}
