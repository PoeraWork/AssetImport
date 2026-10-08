using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace AssetImport
{
    internal sealed partial class NativeContactController
    {
        private long _cameraCallbacks, _ticks, _solverSteps, _poseWrites, _changedAfterWrite, _resets, _heartbeatTicks;
        private float _lastElapsed, _lastDisplacement, _maximumDisplacement, _nextDiagnostic;
        private string _diagnosticState = "awaiting-update", _lastFailure = "none";
        private readonly Queue<string> _diagnosticEvents = new Queue<string>();
        private bool _diagnosticFailureReported;

        internal string DiagnosticSummary
        {
            get { return "Bodies: " + EligibleCount + " breast / " + (Definition == null ? 0 : Definition.Obstacles.Length) +
                " obstacles\nUpdates: " + _ticks + "; shift: " + Number(_lastDisplacement); }
        }
        internal static string Number(float value) { return value.ToString("G6", CultureInfo.InvariantCulture); }
        private static string Vector(Vector3 v) { return "(" + Number(v.x) + "," + Number(v.y) + "," + Number(v.z) + ")"; }
        private string ModelName { get { return Definition != null && Definition.Model ? Definition.Model.name : transform.name; } }
        internal void ReportSettingsApplied()
        { DiagnosticEvent("settings-applied; enabled=" + (_environment && _environment.Settings.NativeBreastContacts)); }

        private void Blocked(string state, string status)
        { Release(); _lastDisplacement = 0; Status = status; DiagnosticState(state); }
        private void DiagnosticState(string state)
        {
            if (_diagnosticState == state) return;
            _diagnosticState = state;
            DiagnosticEvent("state=" + state + "; enabled=" + (_environment && _environment.Settings.NativeBreastContacts) +
                "; breasts=" + EligibleCount + "; obstacles=" + (Definition == null ? 0 : Definition.Obstacles.Length));
        }
        private void DiagnosticEvent(string message)
        {
            // Diagnostics must not change whether the contact layer can run.
            try
            {
                string line = "t=" + Number(Time.realtimeSinceStartup) + "; model=" + ModelName + "; " + message;
                if (_diagnosticEvents.Count >= 64) _diagnosticEvents.Dequeue();
                _diagnosticEvents.Enqueue(line);
                NativeContactDiagnostics.Request("model-state");
                AssetImport.Logger.LogInfo("PMX breast diagnostics: " + line);
            }
            catch { }
        }
        private string Counters()
        {
            return "state=" + _diagnosticState + "; cameraCallbacks=" + _cameraCallbacks + "; ticks=" + _ticks +
                "; solverSteps=" + _solverSteps + "; poseWrites=" + _poseWrites + "; dt=" + Number(_lastElapsed) +
                "; lastDisplacement=" + Number(_lastDisplacement) + "; maxDisplacement=" + Number(_maximumDisplacement) +
                "; changedAfterWrite=" + _changedAfterWrite + "; resets=" + _resets;
        }
        private void DiagnosticHeartbeat()
        {
            if (!_environment) _environment = GetComponent<AssetPhysicsEnvironment>();
            if (!_environment || !_environment.Settings.NativeBreastContacts || Time.realtimeSinceStartup < _nextDiagnostic) return;
            _nextDiagnostic = Time.realtimeSinceStartup + 5f;
            try
            {
                long since = _ticks - _heartbeatTicks; _heartbeatTicks = _ticks;
                if (since == 0 && !_failed) Status = "Breast contacts: no camera update; save diagnostics";
                DiagnosticEvent("summary; ticksSinceSummary=" + since + "; " + Counters());
                if (Definition != null)
                    foreach (var shape in Definition.Breasts) DiagnosticEvent(ContactSample(shape));
            }
            catch (Exception error)
            {
                if (!_diagnosticFailureReported)
                { _diagnosticFailureReported = true; DiagnosticEvent("diagnostic sampling failed: " + error.Message); }
            }
        }

        // Read-only geometric observation, not a collision solver or proof that
        // the native component used a pair. Sample only at report time.
        internal static float SurfaceGap(Vector3 center, float radius, NativeContactShape obstacle)
        {
            float scale = Math.Abs(obstacle.Bone.lossyScale.x);
            return NativeContactPairPolicy.Gap(center, radius, obstacle.Bone.TransformPoint(obstacle.Center),
                obstacle.Bone.rotation * obstacle.Rotation, obstacle.Radius * scale, obstacle.Height * scale, obstacle.Direction);
        }
        private string ContactSample(NativeContactShape shape)
        {
            if (!shape.Bone) return "sample: missing breast bone";
            float radius = shape.Radius * Math.Abs(shape.Bone.lossyScale.x);
            Vector3 center = shape.Bone.TransformPoint(shape.Center);
            float gap = float.PositiveInfinity; string nearest = "none"; int overlap = 0, excluded = 0;
            foreach (var obstacle in Definition.Obstacles)
            {
                if (NativeContactPairPolicy.Exclusion(shape, obstacle) != null) { excluded++; continue; }
                float distance = SurfaceGap(center, radius, obstacle);
                if (distance <= 0) overlap++;
                if (distance < gap) { gap = distance; nearest = obstacle.Bone.name + "/" + obstacle.SourceBody; }
            }
            return "sample bone=" + shape.Bone.name + "; center=" + Vector(center) + "; radius=" + Number(radius) +
                "; nearest=" + nearest + "; surfaceGap=" + Number(gap) + "; estimatedOverlaps=" + overlap +
                "; excludedPairs=" + excluded + "; chainEnabled=" + (_environment && EnabledFor(shape)) + "; geometric estimate only";
        }

        private string ProbeState(int i)
        {
            var bone = Definition.Breasts[i].Bone;
            return "Probe: " + (bone ? bone.name : "missing") + "; boundObstacles=" + _pairCounts[i] +
                "; rawDisplacement=" + Number(_engines[i].LastRawDisplacement) +
                "; maxRawDisplacement=" + Number(_engines[i].MaximumRawDisplacement) +
                "; outputRebases=" + _engines[i].OutputRebases;
        }

        internal string BuildDiagnosticReport()
        {
            if (!_environment) _environment = GetComponent<AssetPhysicsEnvironment>();
            var report = new StringBuilder();
            report.AppendLine("Model: " + ModelName);
            report.AppendLine("Status: " + Status);
            report.AppendLine("Controller active: " + isActiveAndEnabled + "; environment present: " + (bool)_environment);
            report.AppendLine("Applied enabled: " + (_environment && _environment.Settings.NativeBreastContacts));
            report.AppendLine(Counters());
            report.AppendLine("Last failure (retained across retries): " + _lastFailure);
            if (Definition != null)
            {
                report.AppendLine("Recognition:\n" + Definition.RecognitionReport);
                report.AppendLine("Bound breasts=" + EligibleCount + "; obstacles=" + Definition.Obstacles.Length);
                for (int i = 0; i < Definition.Breasts.Length; i++)
                {
                    var shape = Definition.Breasts[i];
                    report.AppendLine(ContactSample(shape));
                    if (_engines != null && i < _engines.Length && _engines[i] != null)
                        report.AppendLine(ProbeState(i));
                    foreach (var obstacle in Definition.Obstacles)
                        report.AppendLine("Pair: breast=" + (shape.Bone ? shape.Bone.name : "missing") + "; obstacle=" + obstacle.SourceBody +
                            "; result=" + (NativeContactPairPolicy.Exclusion(shape, obstacle) ?? "allowed") +
                            "; referenceGap=" + (shape.HasReferencePose && obstacle.HasReferencePose ? Number(NativeContactPairPolicy.ReferenceGap(shape, obstacle)) : "unavailable"));
                }
                foreach (var shape in Definition.Obstacles)
                    report.AppendLine("Obstacle: " + shape.SourceBody + "; bone=" + (shape.Bone ? shape.Bone.name : "missing") +
                        "; localCenter=" + Vector(shape.Center) + "; radius=" + Number(shape.Radius) + "; height=" + Number(shape.Height));
            }
            if (_environment)
                foreach (var chain in _environment.Chains)
                    if (chain) report.AppendLine("Chain: " + (chain.m_Root ? chain.m_Root.name : "missing root") +
                        "; enabled=" + chain.enabled + "; weight=" + Number(chain.m_Weight) + "; mode=" + _environment.Mode(chain));
            report.AppendLine("Recent events:");
            foreach (var line in _diagnosticEvents) report.AppendLine(line);
            report.AppendLine("Surface gaps are sampled sphere/capsule approximations. Changed-after-write also includes manual edits.");
            return report.ToString();
        }
    }
}
