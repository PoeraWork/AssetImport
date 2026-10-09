using System;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace AssetImport
{
    internal partial class AssetUI
    {
        private bool _chainBreastTab;
        private int _chainBreastRole;
        private float _chainBreastRadiusScale = 1f;
        private string _chainBreastRadiusText = "100";
        private bool _chainBreastMeshVolume, _breastPreviewApplied, _breastShowObstacles;
        private float _chainBreastRadiusOverride;
        private Vector3 _chainBreastOffset;
        private string _breastAbsoluteText = "0", _breastXText = "0", _breastYText = "0", _breastZText = "0";

        private static string BreastRoleName(int role)
        { return role == 1 ? "Breast" : role == 2 ? "Exclude" : "Automatic"; }

        private void LoadChainBreastSettings(PhysicsChainSettings settings)
        {
            var clean = settings == null ? new PhysicsChainSettings() : settings.Copy();
            _chainBreastRole = clean.BreastRole; _chainBreastRadiusScale = clean.BreastRadiusScale;
            _chainBreastRadiusText = (100f * _chainBreastRadiusScale).ToString("0.##", CultureInfo.InvariantCulture);
            _chainBreastMeshVolume = clean.BreastMeshVolume; _chainBreastRadiusOverride = clean.BreastRadiusOverride;
            _chainBreastOffset = new Vector3(clean.BreastOffsetX, clean.BreastOffsetY, clean.BreastOffsetZ);
            _breastAbsoluteText = clean.BreastRadiusOverride.ToString("0.####", CultureInfo.InvariantCulture);
            _breastXText = clean.BreastOffsetX.ToString("0.####", CultureInfo.InvariantCulture);
            _breastYText = clean.BreastOffsetY.ToString("0.####", CultureInfo.InvariantCulture);
            _breastZText = clean.BreastOffsetZ.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private void WriteBreastFields(PhysicsChainSettings settings)
        {
            settings.BreastRole = _chainBreastRole; settings.BreastRadiusScale = _chainBreastRadiusScale;
            settings.BreastMeshVolume = _chainBreastMeshVolume; settings.BreastRadiusOverride = _chainBreastRadiusOverride;
            settings.BreastOffsetX = _chainBreastOffset.x; settings.BreastOffsetY = _chainBreastOffset.y; settings.BreastOffsetZ = _chainBreastOffset.z;
        }

        private NativeContactShape[] PreviewBreastSettings(NativeContactController contacts)
        {
            var preview = _environmentDraft.Copy();
            foreach (var root in _chainSelection)
            {
                WriteBreastFields(preview.EditRoot(root));
            }
            return contacts.Preview(preview).Where(s => _chainSelection.Contains(s.ChainRoot)).ToArray();
        }

        private NativeContactShape[] DisplayedBreastShapes(NativeContactController contacts)
        {
            return _breastPreviewApplied ? contacts.ContactShapes.Where(s => _chainSelection.Contains(s.ChainRoot)).ToArray()
                : PreviewBreastSettings(contacts);
        }

        private void DrawChainBreastEditor(int y)
        {
            var contacts = _environment.GetComponent<NativeContactController>();
            bool editable = contacts && _chainSelection.Count > 0;
            GUI.enabled = editable;
            for (int role = 0; role < 3; role++)
                if (CheckButton(new Rect(12 + role * 206, y + 29, 198, 25), _chainBreastRole == role, BreastRoleName(role))) _chainBreastRole = role;
            if (CheckButton(new Rect(12, y + 59, 198, 25), !_chainBreastMeshVolume, "Source volumes")) ResetBreastVolume(false);
            if (CheckButton(new Rect(218, y + 59, 198, 25), _chainBreastMeshVolume, "Fit to mesh")) ResetBreastVolume(true);
            GUI.Label(new Rect(425, y + 61, 200, 22), "Resets size and center");
            GUI.Label(new Rect(12, y + 91, 170, 22), "Sphere radius (%)");
            GUI.enabled = editable && _chainBreastRadiusOverride == 0;
            float value = GUI.HorizontalSlider(new Rect(190, y + 99, 332, 20), _chainBreastRadiusScale, .1f, 4f);
            if (value != _chainBreastRadiusScale)
            { _chainBreastRadiusScale = value; _chainBreastRadiusText = (value * 100f).ToString("0.##", CultureInfo.InvariantCulture); }
            _chainBreastRadiusText = GUI.TextField(new Rect(535, y + 91, 90, 23), _chainBreastRadiusText);
            float percent;
            bool valid = float.TryParse(_chainBreastRadiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out percent) &&
                PhysicsEnvironmentMath.Finite(percent) && percent >= 10f && percent <= 400f;
            if (valid) _chainBreastRadiusScale = percent / 100f;
            GUI.enabled = editable;
            GUI.Label(new Rect(12, y + 120, 185, 22), "Radius (0 = scaled)");
            _breastAbsoluteText = GUI.TextField(new Rect(200, y + 120, 90, 23), _breastAbsoluteText);
            GUI.Label(new Rect(302, y + 120, 325, 22), "Model units; independent of source size");
            GUI.Label(new Rect(12, y + 149, 120, 22), "Center offset");
            GUI.Label(new Rect(135, y + 149, 20, 22), "X"); _breastXText = GUI.TextField(new Rect(157, y + 149, 125, 23), _breastXText);
            GUI.Label(new Rect(305, y + 149, 20, 22), "Y"); _breastYText = GUI.TextField(new Rect(327, y + 149, 125, 23), _breastYText);
            GUI.Label(new Rect(475, y + 149, 20, 22), "Z"); _breastZText = GUI.TextField(new Rect(497, y + 149, 128, 23), _breastZText);
            float absolute, x, z, offsetY;
            bool volumeValid = ReadContactValue(_breastAbsoluteText, 0, 2, out absolute) &
                ReadContactValue(_breastXText, -2, 2, out x) & ReadContactValue(_breastYText, -2, 2, out offsetY) &
                ReadContactValue(_breastZText, -2, 2, out z);
            if (volumeValid) { _chainBreastRadiusOverride = absolute; _chainBreastOffset = new Vector3(x, offsetY, z); }
            valid &= volumeValid;
            GUI.enabled = true;
            if (CheckButton(new Rect(12, y + 178, 190, 25), !_breastPreviewApplied, "Draft (cyan)")) _breastPreviewApplied = false;
            if (CheckButton(new Rect(210, y + 178, 200, 25), _breastPreviewApplied, "Applied (green)")) _breastPreviewApplied = true;
            if (CheckButton(new Rect(418, y + 178, 210, 25), _breastShowObstacles, "Body obstacles")) _breastShowObstacles = !_breastShowObstacles;
            string summary = "Select one or more physics chains.";
            if (contacts && _chainSelection.Count > 0)
            {
                var shapes = DisplayedBreastShapes(contacts);
                summary = shapes.Length + " sphere(s), radius " + (shapes.Length == 0 ? "--" :
                    shapes.Min(s => s.Radius).ToString("0.####", CultureInfo.InvariantCulture) + " - " + shapes.Max(s => s.Radius).ToString("0.####", CultureInfo.InvariantCulture)) +
                    (_breastPreviewApplied ? (_environment.Settings.NativeBreastContacts ? " (applied)" : " (contacts OFF)") : " (draft; click Apply)");
            }
            GUI.Label(new Rect(12, y + 208, 615, 23), summary);
            GUI.enabled = editable && valid;
            if (GUI.Button(new Rect(12, y + 236, 218, 27), "Apply breast settings"))
            {
                foreach (string root in _chainSelection)
                {
                    WriteBreastFields(_environmentDraft.EditRoot(root));
                }
                ApplyPhysicsDraft();
            }
            GUI.enabled = editable;
            if (GUI.Button(new Rect(240, y + 236, 185, 27), "Reset breast settings"))
            {
                LoadChainBreastSettings(null);
                foreach (string root in _chainSelection)
                    WriteBreastFields(_environmentDraft.EditRoot(root));
                ApplyPhysicsDraft();
            }
            GUI.enabled = true;
            string help = !contacts ? "Breast contacts require an imported PMX model." : !valid ? "Radius: 0 to 2. Offsets: -2 to 2. Scale: 10% to 400%." :
                !_environmentDraft.NativeBreastContacts ? "Enable Breast contacts in Selected model physics to use these settings." :
                "Fit groups deforming bones by mesh region. Yellow: allowed body obstacles (max 64). Up to 16 spheres shown; Apply activates edits.";
            GUI.Label(new Rect(12, y + 269, 615, 36), help, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }

        private void ResetBreastVolume(bool mesh)
        {
            int role = _chainBreastRole;
            LoadChainBreastSettings(new PhysicsChainSettings { BreastRole = role, BreastMeshVolume = mesh });
            _breastPreviewApplied = false;
        }
        private static bool ReadContactValue(string text, float min, float max, out float value)
        { return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && PhysicsEnvironmentMath.Finite(value) && value >= min && value <= max; }

        private void DrawBreastContactPreview()
        {
            if (Event.current.type != EventType.Repaint || _chainSelection.Count == 0) return;
            var contacts = _environment.GetComponent<NativeContactController>(); var camera = Camera.main;
            if (!contacts || !camera) return;
            Color old = GUI.color;
            try
            {
                var shapes = DisplayedBreastShapes(contacts);
                GUI.color = _breastPreviewApplied ? Color.green : Color.cyan;
                foreach (var shape in shapes.Take(16))
                {
                    if (!shape.Bone) continue;
                    var center = shape.Bone.TransformPoint(shape.Center);
                    float radius = shape.Radius * Math.Abs(shape.Bone.lossyScale.x);
                    DrawContactSphere(camera, center, radius);
                }
                if (_breastShowObstacles && contacts.Definition != null)
                {
                    GUI.color = Color.yellow;
                    foreach (var body in contacts.Definition.Obstacles.Where(o => shapes.Any(s => NativeContactPairPolicy.Exclusion(s, o) == null)).Take(64))
                    {
                        if (!body.Bone) continue;
                        var center = body.Bone.TransformPoint(body.Center);
                        float scale = Math.Abs(body.Bone.lossyScale.x), radius = body.Radius * scale;
                        var axis = body.Direction == 0 ? Vector3.right : body.Direction == 2 ? Vector3.forward : Vector3.up;
                        var half = body.Bone.rotation * body.Rotation * axis * (Math.Max(0, body.Height * .5f - body.Radius) * scale);
                        DrawContactSphere(camera, center + half, radius);
                        if (half.sqrMagnitude > 0)
                        {
                            DrawContactSphere(camera, center - half, radius);
                            var perpendicular = body.Direction == 0 ? Vector3.up : Vector3.right;
                            var side = body.Bone.rotation * body.Rotation * perpendicular * radius;
                            DrawContactArc(camera, center + half + side, center - half + side);
                            DrawContactArc(camera, center + half - side, center - half - side);
                        }
                    }
                }
            }
            finally { GUI.color = old; }
        }
        private static void DrawContactSphere(Camera camera, Vector3 center, float radius)
        {
            for (int axis = 0; axis < 3; axis++)
                for (int i = 0; i < 24; i++)
                {
                    float a = i * 2f * Mathf.PI / 24, b = (i + 1) * 2f * Mathf.PI / 24;
                    DrawContactArc(camera, center + CirclePoint(axis, a) * radius, center + CirclePoint(axis, b) * radius);
                }
        }
        private static Vector3 CirclePoint(int axis, float angle)
        {
            float a = Mathf.Cos(angle), b = Mathf.Sin(angle);
            return axis == 0 ? new Vector3(0, a, b) : axis == 1 ? new Vector3(a, 0, b) : new Vector3(a, b, 0);
        }
        private static void DrawContactArc(Camera camera, Vector3 a, Vector3 b)
        {
            a = camera.WorldToScreenPoint(a); b = camera.WorldToScreenPoint(b);
            if (a.z <= 0 || b.z <= 0) return;
            var start = new Vector2(a.x, Screen.height - a.y); var delta = new Vector2(b.x - a.x, a.y - b.y);
            var matrix = GUI.matrix;
            try
            {
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
                GUI.DrawTexture(new Rect(start.x, start.y, delta.magnitude, 1), Texture2D.whiteTexture);
            }
            finally { GUI.matrix = matrix; }
        }
    }
}
