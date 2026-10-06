// Minimal Unity surface plus a deterministic Verlet fixture. This is not the
// native DynamicBone solver; native API compatibility is checked separately.
using AssetImport;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
    }
    public class GameObject : Object
    {
        public readonly Transform transform;
        public bool activeInHierarchy = true;
        private readonly List<MonoBehaviour> components = new();
        public GameObject() { transform = new Transform { gameObject = this }; }
        public T AddComponent<T>() where T : MonoBehaviour, new()
        {
            var c = new T { gameObject = this }; components.Add(c); return c;
        }
        public T GetComponent<T>() where T : MonoBehaviour => components.OfType<T>().FirstOrDefault();
    }
    public class MonoBehaviour : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;
        public bool enabled = true;
        public bool isActiveAndEnabled => enabled && gameObject.activeInHierarchy;
    }
    public class Transform : Object
    {
        public GameObject gameObject;
        public string name = "cloth";
        public Transform parent;
        public Vector3 position;
        public Vector3 lossyScale = new(1, 1, 1);
        public Quaternion rotation = Quaternion.identity;
        public bool IsChildOf(Transform other) => this == other || (parent && parent.IsChildOf(other));
        public Vector3 TransformVector(Vector3 value) => Vector3.From(System.Numerics.Vector3.Transform(
            new System.Numerics.Vector3(value.x * lossyScale.x, value.y * lossyScale.y, value.z * lossyScale.z), rotation.Value));
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => MathF.Sqrt(sqrMagnitude);
        public Vector3 normalized => magnitude > 0 ? this * (1f / magnitude) : new();
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float f) => new(a.x * f, a.y * f, a.z * f);
        public static Vector3 From(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    }
    public struct Quaternion
    {
        public System.Numerics.Quaternion Value;
        public static Quaternion identity => new() { Value = System.Numerics.Quaternion.Identity };
        public static Quaternion Turn(float angle) => new() { Value = System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ, angle) };
        public static float Angle(Quaternion a, Quaternion b) => 2 * MathF.Acos(Math.Min(1, Math.Abs(System.Numerics.Quaternion.Dot(a.Value, b.Value)))) * 180 / MathF.PI;
    }
}

public class DynamicBone : MonoBehaviour
{
    public class Particle
    {
        public int m_ParentIndex = -1;
        public Transform m_Transform;
        public float m_Radius;
        public Vector3 m_Position, m_PrevPosition, m_EndOffset;
    }
    public Transform m_Root;
    public List<Particle> m_Particles = new();
    public float m_UpdateRate = 60, m_Weight = 1;
    public bool m_DistantDisabled, ThrowOnUpdate;
    public Vector3 m_Force, m_Gravity;
    public object m_Colliders = new(), m_DampingDistrib = new();
    public int Steps, InitCalls;
    public void InitTransforms() { InitCalls++; }
    public void UpdateDynamicBones(float time)
    {
        if (ThrowOnUpdate) throw new InvalidOperationException("injected");
        Steps++;
        foreach (var p in m_Particles)
        {
            if (p.m_ParentIndex < 0) continue;
            var last = p.m_Position;
            p.m_Position += (p.m_Position - p.m_PrevPosition) * .75f + m_Force + m_Gravity;
            p.m_PrevPosition = last;
        }
        AssetPhysicsEnvironment.AfterIntegrate(this);
        foreach (var p in m_Particles)
            if (p.m_ParentIndex >= 0)
            {
                var parent = m_Particles[p.m_ParentIndex];
                var rest = p.m_Transform ? p.m_Transform.position - parent.m_Transform.position : parent.m_Transform.TransformVector(p.m_EndOffset);
                p.m_Position = parent.m_Position + (p.m_Position - parent.m_Position).normalized * rest.magnitude;
            }
        AssetPhysicsEnvironment.AfterConstraints(this);
    }
    public void UpdateParticles1() { }
    public void UpdateParticles2() { }
    public void SkipUpdateParticles() { }
    public void ResetParticlesPosition() { }
    public void SetupParticles() { }
}

namespace AssetImport
{
    internal static class AssetImport { internal static Log Logger = new(); }
    internal sealed class Log { internal void LogWarning(string message) => Console.WriteLine(message); }
}
namespace MessagePack
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class MessagePackObjectAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Property)] public sealed class KeyAttribute(string key) : Attribute { public string Key = key; }
}
namespace HarmonyLib
{
    public sealed class HarmonyPostfix : Attribute { }
    public sealed class HarmonyPatch(Type type, string name) : Attribute { public Type Type = type; public string Name = name; }
}
