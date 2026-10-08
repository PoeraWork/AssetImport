using System;
using System.Collections.Generic;
using UnityEngine;

namespace AssetImport
{
    [DefaultExecutionOrder(-32000)]
    internal sealed partial class NativeContactController : MonoBehaviour
    {
        public NativeContactDefinition Definition;
        private AssetPhysicsEnvironment _environment;
        private NativeContactEngine[] _engines;
        private List<DynamicBoneCollider> _obstacles;
        // Unity cloning remaps these references. A cloned controller restores
        // the input pose and removes cloned helpers before making its own rig.
        [SerializeField] private List<GameObject> _objects = new List<GameObject>();
        [SerializeField] private Vector3[] _baseLocal, _writtenLocal;
        [SerializeField] private bool _written;
        private Vector3[] _baseWorld, _displacements;
        private bool _readyForCamera, _failed, _reported;
        private int _frame = -1;
        private float _scale;
        internal string Status { get; private set; } = "Breast contacts: Off";
        internal int EligibleCount { get { return Definition == null ? 0 : Definition.Breasts.Length; } }

        internal static void Attach(GameObject host, NativeContactDefinition definition)
        {
            if (definition == null) return;
            var controller = host.GetComponent<NativeContactController>() ?? host.AddComponent<NativeContactController>();
            controller.ResetContacts();
            controller.Definition = definition;
            controller._environment = host.GetComponent<AssetPhysicsEnvironment>();
            controller.Status = "Breast contacts: Off (" + controller.EligibleCount + " eligible)";
            AssetImport.Logger.LogInfo("PMX native breast candidates: model=" + definition.Model.name + "; breasts=" +
                definition.Breasts.Length + "; bodyObstacles=" + definition.Obstacles.Length + ". Disabled until explicitly enabled.");
            controller.DiagnosticEvent("attached; " + controller.DiagnosticSummary.Replace('\n', ';'));
            AssetImport.Logger.LogInfo("PMX breast recognition: model=" + definition.Model.name + "\n" + definition.RecognitionReport);
        }
        private void OnEnable() { ResetContacts(); Camera.onPreCull += BeforeCamera; AssetPhysicsEnvironment.ResetRequested += OnPhysicsReset; }
        private void OnDisable() { Camera.onPreCull -= BeforeCamera; AssetPhysicsEnvironment.ResetRequested -= OnPhysicsReset; ResetContacts(); }
        private void OnDestroy() { ResetContacts(); }
        private void OnPhysicsReset(DynamicBone chain) { if (chain && chain.gameObject == gameObject) ResetContacts(); }
        private void Update() { _readyForCamera = false; Restore(); }
        private void LateUpdate() { _readyForCamera = true; DiagnosticHeartbeat(); }
        private void BeforeCamera(Camera camera)
        {
            _cameraCallbacks++;
            if (!_readyForCamera || _frame == Time.frameCount || !isActiveAndEnabled) return;
            _frame = Time.frameCount;
            Tick(Time.deltaTime);
        }

        internal void ResetContacts()
        {
            Restore(); Release(); _failed = false; _reported = false; _frame = -1;
            _resets++; _lastDisplacement = 0; _diagnosticState = "awaiting-update";
            _nextDiagnostic = Time.realtimeSinceStartup + 5f;
        }

        private bool EnabledFor(NativeContactShape shape)
        {
            bool any = false;
            foreach (var chain in _environment.Chains)
            {
                if (!chain || !chain.m_Root) continue;
                bool enabled = chain.enabled && chain.m_Weight > 0 && _environment.Mode(chain) != PhysicsChainMode.Animation;
                any |= enabled;
                if (shape.Bone.IsChildOf(chain.m_Root)) return enabled;
            }
            return any;
        }

        internal void Tick(float elapsed)
        {
            _ticks++; _lastElapsed = elapsed;
            Restore();
            if (!_environment) _environment = GetComponent<AssetPhysicsEnvironment>();
            if (!_environment) { Blocked("missing-environment", "Breast contacts: physics component missing"); return; }
            if (!_environment.isActiveAndEnabled) { Blocked("environment-disabled", "Breast contacts: physics component disabled"); return; }
            if (Definition == null || !Definition.Model) { Blocked("missing-definition", "Breast contacts: model data missing"); return; }
            if (!Definition.Model.IsChildOf(transform)) { Blocked("model-owner-mismatch", "Breast contacts: model ownership changed"); return; }
            if (!Definition.Model.gameObject.activeInHierarchy) { Blocked("model-inactive", "Breast contacts: model inactive"); return; }
            if (!_environment.Settings.NativeBreastContacts)
            { Release(); _failed = false; _reported = false; Status = "Breast contacts: Off (" + EligibleCount + " eligible)"; DiagnosticState("off"); return; }
            if (_failed) return;
            try
            {
                float scale = UniformScale(Definition.Model);
                UniformScale(transform);
                if (EligibleCount == 0 || Definition.Obstacles.Length == 0)
                {
                    Status = "Breast contacts: no usable PMX bodies";
                    DiagnosticState(EligibleCount == 0 ? "no-breast-bodies" : "no-body-obstacles");
                    if (!_reported) AssetImport.Logger.LogWarning("PMX native breast unavailable: no matching weighted breast body or stable PMX body obstacles.");
                    _reported = true; return;
                }
                bool enabled = false;
                foreach (var breast in Definition.Breasts) if (breast.Bone && EnabledFor(breast)) enabled = true;
                if (!enabled) { Blocked("physics-off", "Breast contacts: physics is off"); return; }
                if (_engines != null && Math.Abs(scale - _scale) > .00001f * scale) Release();
                if (_engines == null) Initialize(scale);
                // Capture every input before writing any parent or child bone.
                int active = 0; float maximum = 0;
                for (int i = 0; i < EligibleCount; i++)
                {
                    var shape = Definition.Breasts[i];
                    if (!shape.Bone || !shape.Bone.IsChildOf(Definition.Model)) throw new InvalidOperationException("Breast bone is no longer owned by the model.");
                    _baseLocal[i] = shape.Bone.localPosition; _baseWorld[i] = shape.Bone.position;
                    _displacements[i] = Vector3.zero;
                    if (!EnabledFor(shape)) continue;
                    _displacements[i] = _engines[i].Step(shape.Bone.TransformPoint(shape.Center), shape.Bone.rotation, elapsed);
                    if (elapsed > 0) _solverSteps++;
                    maximum = Math.Max(maximum, _displacements[i].magnitude); active++;
                }
                // Import binding is sorted by ancestry. Real bone rotations and
                // scales remain the original animation/ordinary physics output.
                _written = true;
                for (int i = 0; i < EligibleCount; i++)
                {
                    Definition.Breasts[i].Bone.position = _baseWorld[i] + _displacements[i];
                    _writtenLocal[i] = Definition.Breasts[i].Bone.localPosition;
                }
                Status = "Breast contacts: " + active + " active / " + EligibleCount;
                _poseWrites++; _lastDisplacement = maximum; _maximumDisplacement = Math.Max(_maximumDisplacement, maximum);
                DiagnosticState(elapsed <= 0 ? "zero-delta-time" : "running");
                if (!_reported && maximum > .0001f * scale)
                {
                    _reported = true;
                    AssetImport.Logger.LogInfo("PMX native breast response: active=" + active + "; displacement=" + maximum +
                        "; obstacles=" + _obstacles.Count + ". Native response only; visible contact requires in-game verification.");
                }
            }
            catch (Exception error)
            {
                Restore(); Release(); _failed = true; _lastDisplacement = 0;
                Status = "Breast contacts unavailable; see log";
                var detail = error.InnerException ?? error;
                _lastFailure = detail.ToString();
                DiagnosticState("failed");
                AssetImport.Logger.LogWarning("PMX native breast adapter stopped; ordinary physics remains active. " + detail);
            }
        }

        private void Initialize(float scale)
        {
            _scale = scale;
            _obstacles = new List<DynamicBoneCollider>();
            foreach (var shape in Definition.Obstacles)
            {
                if (!shape.Bone || !shape.Bone.IsChildOf(Definition.Model)) continue;
                var obj = new GameObject("AI_BreastContactObstacle"); _objects.Add(obj);
                obj.transform.SetParent(shape.Bone, false);
                obj.transform.localPosition = shape.Center; obj.transform.localRotation = shape.Rotation;
                var collider = obj.AddComponent<DynamicBoneCollider>();
                collider.m_Radius = shape.Radius; collider.m_Height = shape.Height;
                collider.m_Direction = (DynamicBoneCollider.Direction)shape.Direction;
                collider.m_Bound = DynamicBoneCollider.Bound.Outside;
                _obstacles.Add(collider);
            }
            if (_obstacles.Count == 0) throw new InvalidOperationException("No owned body obstacles remain.");
            int count = EligibleCount;
            _engines = new NativeContactEngine[count];
            _baseLocal = new Vector3[count]; _baseWorld = new Vector3[count];
            _writtenLocal = new Vector3[count]; _displacements = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                var shape = Definition.Breasts[i];
                if (!shape.Bone || !shape.Bone.IsChildOf(Definition.Model)) throw new InvalidOperationException("Invalid breast owner.");
                float radius = shape.Radius * UniformScale(shape.Bone);
                if (!PhysicsEnvironmentMath.Finite(radius) || radius <= .000001f) throw new InvalidOperationException("Invalid breast radius.");
                DiagnosticEvent("initializing bone=" + shape.Bone.name + "; radius=" + Number(radius) + "; obstacles=" + _obstacles.Count);
                _engines[i] = new NativeContactEngine(transform, shape.Bone.TransformPoint(shape.Center), shape.Bone.rotation, radius, _obstacles,
                    message => DiagnosticEvent("bone=" + shape.Bone.name + "; " + message));
                _objects.Add(_engines[i].Host);
                AssetImport.Logger.LogInfo("PMX native breast initialized: bone=" + shape.Bone.name + "; radius=" + radius +
                    "; obstacles=" + _obstacles.Count + "; nativeParticles=" + _engines[i].ParticleCount +
                    "; sphereSelfCheck=passed; engine=" + typeof(DynamicBone_Ver02).Assembly.GetName().Version + ".");
            }
        }

        internal void Restore()
        {
            if (!_written || Definition == null) return;
            for (int i = 0; i < Definition.Breasts.Length; i++)
            {
                var bone = Definition.Breasts[i].Bone;
                if (bone && (bone.localPosition - _writtenLocal[i]).sqrMagnitude < 1e-16f) bone.localPosition = _baseLocal[i];
                else if (bone) _changedAfterWrite++;
            }
            _written = false;
        }
        private void Release()
        {
            if (_engines != null) foreach (var engine in _engines) if (engine != null) engine.Dispose();
            _engines = null;
            foreach (var obj in _objects) if (obj && obj.transform.IsChildOf(transform)) UnityEngine.Object.Destroy(obj);
            _objects.Clear(); _obstacles = null;
        }
        private static float UniformScale(Transform bone)
        {
            var s = bone.lossyScale;
            if (!PhysicsEnvironmentMath.Finite(s.sqrMagnitude) || s.x <= .000001f || s.y <= 0 || s.z <= 0 ||
                Math.Abs(s.x - s.y) > s.x * .001f || Math.Abs(s.x - s.z) > s.x * .001f)
                throw new InvalidOperationException("Breast contacts require positive uniform scale.");
            return s.x;
        }
    }
}
