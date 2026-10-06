using System.Globalization;
using UnityEngine;

namespace AssetImport
{
    internal partial class AssetUI
    {
        private Asset _environmentAsset;
        private AssetPhysicsEnvironment _environment;
        private PhysicsEnvironmentSettings _environmentDraft;
        private string _floorOffsetText, _downwardForceText;

        private void DrawPhysicsEnvironment(ref int y, AssetPhysicsEnvironment environment, Asset asset)
        {
            if (!environment || asset == null || environment.Chains == null || environment.Chains.Length == 0)
            { _environmentAsset = null; _environment = null; return; }
            if (_environmentAsset != asset || _environment != environment)
            {
                _environmentAsset = asset; _environment = environment;
                _environmentDraft = environment.Settings.Copy();
                _floorOffsetText = _environmentDraft.FloorOffset.ToString(CultureInfo.InvariantCulture);
                _downwardForceText = _environmentDraft.DownwardForce.ToString(CultureInfo.InvariantCulture);
            }
            GUI.enabled = !PreloadUI;
            GUI.Label(new Rect(10, y += 8, 220, 22), "Selected model physics");
            if (CheckButton(new Rect(10, y += 25, 220, 25), _environmentDraft.Floor, "Ground collision"))
                _environmentDraft.Floor = !_environmentDraft.Floor;
            GUI.Label(new Rect(10, y += 28, 140, 22), "Ground offset");
            _floorOffsetText = GUI.TextField(new Rect(153, y, 77, 22), _floorOffsetText);
            GUI.Label(new Rect(10, y += 25, 140, 22), "Downward force");
            _downwardForceText = GUI.TextField(new Rect(153, y, 77, 22), _downwardForceText);
            if (CheckButton(new Rect(10, y += 25, 220, 25), _environmentDraft.Settle, "Settle on load / reset"))
                _environmentDraft.Settle = !_environmentDraft.Settle;
            var style = new GUIStyle(GUI.skin.label) { wordWrap = true };
            const string help = "Ground offset is relative to the model origin. Force: 0 to 0.02 (0 = off). Pause motion before settling.";
            float height = style.CalcHeight(new GUIContent(help), 220);
            GUI.Label(new Rect(10, y += 28, 220, height), help, style);
            y += (int)height;
            if (GUI.Button(new Rect(10, y += 4, 220, 25), "Apply physics settings"))
            {
                float offset, force;
                if (!float.TryParse(_floorOffsetText, NumberStyles.Float, CultureInfo.InvariantCulture, out offset) ||
                    !float.TryParse(_downwardForceText, NumberStyles.Float, CultureInfo.InvariantCulture, out force) ||
                    !PhysicsEnvironmentMath.Finite(offset) || !PhysicsEnvironmentMath.Finite(force) ||
                    offset < -100f || offset > 100f || force < 0f || force > .02f)
                    _physicsActionStatus = "Enter ground offset from -100 to 100 and force from 0 to 0.02.";
                else
                {
                    _environmentDraft.FloorOffset = offset; _environmentDraft.DownwardForce = force;
                    environment.Apply(_environmentDraft);
                    PhysicsEnvironmentPersistence.Write(asset, _environmentDraft);
                    _physicsActionStatus = "Physics settings applied.";
                }
            }
            if (GUI.Button(new Rect(10, y += 30, 220, 25), environment.IsSettling ? "Restart settling" : "Settle physics"))
                _physicsActionStatus = environment.RequestSettle() > 0 ? "Settling physics." : "Enable physics before settling.";
            y += 25;
            if (environment.IsSettling)
            {
                GUI.Label(new Rect(10, y += 4, 220, 22), "Settling physics...");
                y += 22;
            }
            GUI.enabled = true;
        }
    }
}
