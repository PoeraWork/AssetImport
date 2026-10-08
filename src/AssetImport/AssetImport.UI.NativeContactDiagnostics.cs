using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace AssetImport
{
    internal partial class AssetUI
    {
        private string SaveContactDiagnostics(AssetPhysicsEnvironment selected, string selectionStatus)
        {
            try
            {
                var report = new StringBuilder();
                var assembly = typeof(AssetImport).Assembly;
                var build = (AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute));
                report.AppendLine("AssetImport breast contact diagnostics");
                report.AppendLine("UTC: " + DateTime.UtcNow.ToString("o"));
                report.AppendLine("Build: " + (build == null ? "unknown" : build.InformationalVersion));
                report.AppendLine("Plugin: " + AssetImport.PluginName + "; Unity: " + Application.unityVersion);
                report.AppendLine("Selection: " + selectionStatus);
                report.AppendLine("Selected environment: " + (selected ? selected.transform.name : "none"));
                if (selected)
                {
                    report.AppendLine("Applied contacts enabled: " + selected.Settings.NativeBreastContacts);
                    if (_environment == selected && _environmentDraft != null)
                        report.AppendLine("Panel draft contacts enabled: " + _environmentDraft.NativeBreastContacts);
                    if (!selected.GetComponent<NativeContactController>())
                        report.AppendLine("Selected model has no contact controller. Check PMX import, armature and physics chain selection.");
                }
                var controllers = Resources.FindObjectsOfTypeAll<NativeContactController>();
                report.AppendLine("Loaded contact controllers (including inactive models): " + controllers.Length);
                if (controllers.Length == 0)
                    report.AppendLine("No contact controller was created. Reload/import a PMX model with armature and physics chains; include the import log if still absent.");
                for (int i = 0; i < controllers.Length; i++)
                {
                    report.AppendLine("\n--- Controller " + i + " ---");
                    try { report.AppendLine(controllers[i].BuildDiagnosticReport()); }
                    catch (Exception error) { report.AppendLine("Diagnostic capture failed: " + error); }
                }
                string directory = Path.Combine(BepInEx.Paths.GameRootPath, "UserData/AssetImport/Diagnostics");
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "breast-contacts.txt");
                File.WriteAllText(path, report.ToString(), new UTF8Encoding(true));
                AssetImport.Logger.LogInfo("PMX breast diagnostics saved: " + path);
                return "Saved: " + path;
            }
            catch (Exception error)
            {
                AssetImport.Logger.LogWarning("PMX breast diagnostics export failed: " + error);
                return "Could not save diagnostics: " + error.Message;
            }
        }
    }
}
