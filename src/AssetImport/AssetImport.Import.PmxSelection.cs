using System;
using System.Linq;

namespace AssetImport
{
    public partial class Import
    {
        private PmxPhysicsRootSuggestions _pmxRootSuggestions;

        internal string SelectPmxHairRoots()
        {
            return SelectPmxSuggestedRoots(true);
        }

        internal string SelectPmxPhysicsRoots()
        {
            return SelectPmxSuggestedRoots(false);
        }

        private string SelectPmxSuggestedRoots(bool hairOnly)
        {
            if (!IsPmx || !ImportBones || _pmx == null)
                return "Enable Import Armature to select PMX physics roots.";
            if (_pmxRootSuggestions == null) _pmxRootSuggestions = PmxPhysicsRootSuggestions.Build(_pmx);
            var roots = hairOnly ? _pmxRootSuggestions.HairRoots : _pmxRootSuggestions.PhysicsRoots;
            string label = hairOnly ? "Hair" : "Physics";
            var names = BoneNodes.ToDictionary(n => n.GameObject.name, StringComparer.Ordinal);
            int added = 0, existing = 0, skipped = 0;
            foreach (int index in roots)
            {
                BoneNode node;
                string name = PmxModel.BoneName(index, _pmx.Bones[index].Name);
                if (!names.TryGetValue(name, out node))
                {
                    skipped++;
                    Logger.LogInfo("PMX auto physics root skipped: " + name + "; imported bone node is unavailable.");
                    continue;
                }
                if (node.IsDynamicRoot) { existing++; continue; }
                node.SetDynamic(true);
                if (!node.IsDynamicRoot)
                {
                    skipped++;
                    Logger.LogInfo("PMX auto physics root skipped: " + name + "; overlaps an existing parent/child root. Existing selection preserved.");
                    continue;
                }
                added++;
                for (var parent = node; parent != null; parent = parent.Parent) parent.SetNodeOpen(true);
                Logger.LogInfo("PMX auto " + (hairOnly ? "hair" : "physics") + " root selected: " + node.GameObject.name);
            }
            foreach (string diagnostic in _pmxRootSuggestions.Diagnostics)
                if (hairOnly) Logger.LogDebug("PMX root suggestions: " + diagnostic);
                else Logger.LogInfo("PMX root suggestions: " + diagnostic);
            string status = roots.Length == 0
                ? (hairOnly ? "No confident hair roots found. Select the hair chain roots manually." : "No usable PMX dynamic chains found; bone names alone do not create physics.")
                : label + " roots: " + added + " added, " + existing + " already selected, " + skipped + " skipped. Other selections kept.";
            if (!hairOnly && (_pmxRootSuggestions.SkippedRootCount != 0 || _pmxRootSuggestions.IgnoredBodyCount != 0))
                status += " " + _pmxRootSuggestions.SkippedRootCount + " unsupported roots and " + _pmxRootSuggestions.IgnoredBodyCount + " unbound bodies excluded (see log).";
            if (skipped > 0) status += " Check overlapping parent/child roots.";
            Logger.LogInfo("PMX auto " + (hairOnly ? "hair" : "physics") + " selection: " + status);
            return status;
        }

        internal void ClearPhysicsRoots()
        {
            foreach (var root in BoneNodes.Where(n => n.IsDynamicRoot).ToArray()) root.SetDynamic(false);
        }
    }
}
