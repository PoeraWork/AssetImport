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

        private static string BreastRoleName(int role)
        { return role == 1 ? "Breast" : role == 2 ? "Exclude" : "Automatic"; }

        private void LoadChainBreastSettings(PhysicsChainSettings settings)
        {
            var clean = settings == null ? new PhysicsChainSettings() : settings.Copy();
            _chainBreastRole = clean.BreastRole; _chainBreastRadiusScale = clean.BreastRadiusScale;
            _chainBreastRadiusText = (100f * _chainBreastRadiusScale).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private NativeContactShape[] PreviewBreastSettings(NativeContactController contacts)
        {
            var preview = _environmentDraft.Copy();
            foreach (var root in _chainSelection)
            {
                var preference = preview.EditRoot(root);
                preference.BreastRole = _chainBreastRole; preference.BreastRadiusScale = _chainBreastRadiusScale;
            }
            return contacts.Preview(preview).Where(s => _chainSelection.Contains(s.ChainRoot)).ToArray();
        }

        private void DrawChainBreastEditor(int y)
        {
            var contacts = _environment.GetComponent<NativeContactController>();
            bool editable = contacts && _chainSelection.Count > 0;
            GUI.enabled = editable;
            for (int role = 0; role < 3; role++)
                if (CheckButton(new Rect(12 + role * 206, y + 29, 198, 25), _chainBreastRole == role, BreastRoleName(role))) _chainBreastRole = role;
            GUI.Label(new Rect(12, y + 61, 170, 22), "Sphere radius (%)");
            float value = GUI.HorizontalSlider(new Rect(190, y + 69, 332, 20), _chainBreastRadiusScale, .1f, 4f);
            if (value != _chainBreastRadiusScale)
            { _chainBreastRadiusScale = value; _chainBreastRadiusText = (value * 100f).ToString("0.##", CultureInfo.InvariantCulture); }
            _chainBreastRadiusText = GUI.TextField(new Rect(535, y + 61, 90, 23), _chainBreastRadiusText);
            float percent;
            bool valid = float.TryParse(_chainBreastRadiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out percent) &&
                PhysicsEnvironmentMath.Finite(percent) && percent >= 10f && percent <= 400f;
            if (valid) _chainBreastRadiusScale = percent / 100f;
            GUI.enabled = true;
            GUI.Label(new Rect(12, y + 87, 615, 23), contacts && _chainSelection.Count > 0
                ? "Preview: " + PreviewBreastSettings(contacts).Length + " sphere(s). Changes take effect after Apply." : "Select one or more physics chains.");
            GUI.enabled = editable && valid;
            if (GUI.Button(new Rect(12, y + 116, 218, 27), "Apply breast settings"))
            {
                foreach (string root in _chainSelection)
                {
                    var settings = _environmentDraft.EditRoot(root);
                    settings.BreastRole = _chainBreastRole; settings.BreastRadiusScale = _chainBreastRadiusScale;
                }
                ApplyPhysicsDraft();
            }
            GUI.enabled = editable;
            if (GUI.Button(new Rect(240, y + 116, 185, 27), "Reset breast settings"))
            {
                foreach (string root in _chainSelection)
                { var settings = _environmentDraft.EditRoot(root); settings.BreastRole = 0; settings.BreastRadiusScale = 1f; }
                LoadChainBreastSettings(null); ApplyPhysicsDraft();
            }
            GUI.enabled = true;
            string help = !contacts ? "Breast contacts require an imported PMX model." : !valid ? "Enter a sphere radius from 10% to 400%." :
                !_environmentDraft.NativeBreastContacts ? "Enable Breast contacts in Selected model physics to use these settings." :
                "Breast marks weighted bones in the selected chains. Radius scales PMX or bone-spacing spheres. Preview shows up to 16 spheres.";
            GUI.Label(new Rect(12, y + 149, 615, 36), help, new GUIStyle(GUI.skin.label) { wordWrap = true });
        }

        private void DrawBreastContactPreview()
        {
            if (Event.current.type != EventType.Repaint || _chainSelection.Count == 0) return;
            var contacts = _environment.GetComponent<NativeContactController>(); var camera = Camera.main;
            if (!contacts || !camera) return;
            Color old = GUI.color;
            try
            {
                GUI.color = Color.cyan;
                foreach (var shape in PreviewBreastSettings(contacts).Take(16))
                {
                    if (!shape.Bone) continue;
                    var center = shape.Bone.TransformPoint(shape.Center);
                    float radius = shape.Radius * Math.Abs(shape.Bone.lossyScale.x);
                    for (int axis = 0; axis < 3; axis++)
                        for (int i = 0; i < 24; i++)
                        {
                            float a = i * 2f * Mathf.PI / 24, b = (i + 1) * 2f * Mathf.PI / 24;
                            DrawContactArc(camera, center + CirclePoint(axis, a) * radius, center + CirclePoint(axis, b) * radius);
                        }
                }
            }
            finally { GUI.color = old; }
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
