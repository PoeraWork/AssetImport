using System;
using System.IO;

namespace AssetImport
{
    internal partial class AssetUI
    {
        private string SaveContactDiagnostics(AssetPhysicsEnvironment selected, string selectionStatus)
        {
            NativeContactDiagnostics.Select(selected, _environment == selected && _environmentDraft != null
                ? (bool?)_environmentDraft.NativeBreastContacts : null, selectionStatus);
            return NativeContactDiagnostics.Save("manual") ? "Saved: " + NativeContactDiagnostics.ReportPath : NativeContactDiagnostics.LastError;
        }
        private string OpenContactDiagnosticsFolder()
        {
            if (!NativeContactDiagnostics.Save("manual")) return NativeContactDiagnostics.LastError;
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                    FileName = Path.GetDirectoryName(NativeContactDiagnostics.ReportPath), UseShellExecute = true });
                return "Report: " + NativeContactDiagnostics.ReportPath;
            }
            catch (Exception error) { return "Report: " + NativeContactDiagnostics.ReportPath + ". Could not open folder: " + error.Message; }
        }
    }
}
