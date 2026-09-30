using System.Collections.Generic;
using System;
using System.IO;
using UnityEngine;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using KKAPI;
using KKAPI.Studio.SaveLoad;
using KKAPI.Chara;
using HarmonyLib;
using KK_Plugins.MaterialEditor;
using KKAPI.Utilities;

namespace AssetImport
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency(KK_Plugins.MaterialEditor.MaterialEditorPlugin.PluginGUID, MinimumMaterialEditorVersion)]
    [BepInDependency(KK_Plugins.DynamicBoneEditor.Plugin.PluginGUID, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency("LoadFileLmitedFix")]
    public class AssetImport : BaseUnityPlugin
    {
#if KK
        public const string PluginName = "KK_AssetImport";
        // Keep the supported minimum independent of future build-reference upgrades.
        internal const string MinimumMaterialEditorVersion = "4.0.3";
#else
        public const string PluginName = "KKS_AssetImport";
        internal const string MinimumMaterialEditorVersion = "3.13.5";
#endif
        public const string GUID = "org.njaecha.plugins.assetimport";
        public const string Version = "4.1.2";

        internal new static ManualLogSource Logger;
        internal static AssetSceneController asc;
        internal static AssetUI UI;
        internal static AssetImport instance;

        // Config
        internal static ConfigEntry<KeyboardShortcut> hotkey;
        internal static ConfigEntry<string> defaultDir;
        internal static ConfigEntry<bool> dumpAssets;

        // current import
        internal static LoadProcess currentLoadProcess;

#if !KK
        internal static ComputeShader vertexDeltaComputeShader;
#endif

        void Awake()
        {
            Logger = base.Logger;
#if KK
            // Match the original Packed ZIP layout; do not depend on the working directory.
            string nativePath = Path.Combine(Paths.GameRootPath,
                "runtimes/win-x64/native/assimp.dll");
            if (IntPtr.Size != 8 || !File.Exists(nativePath))
            {
                Logger.LogError("KK AssetImport requires the full Windows x64 package. Missing native library: " + nativePath);
                enabled = false;
                return;
            }
            try
            {
                var library = Assimp.Unmanaged.AssimpLibrary.Instance;
                if (library.IsLibraryLoaded && !string.Equals(Path.GetFullPath(library.LibraryPath),
                    Path.GetFullPath(nativePath), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Another plugin has already loaded AssimpNet from " +
                        library.LibraryPath + ". Restart with only one AssimpNet importer enabled.");
                if (!library.LoadLibrary(nativePath))
                    throw new InvalidOperationException("Assimp returned an unsuccessful library load.");
            }
            catch (Exception exception)
            {
                Logger.LogError("Could not load the bundled Assimp 5 library: " + exception);
                enabled = false;
                return;
            }
#endif
            
            KeyboardShortcut defaultShortcut = new KeyboardShortcut(KeyCode.I, KeyCode.LeftAlt);
            hotkey = Config.Bind("_General_", "Hotkey", defaultShortcut, "Press this key to open the UI");
            defaultDir = Config.Bind("_General_", "Default Directory", Paths.GameRootPath, "The default directory of the file dialogue.");
            dumpAssets = Config.Bind("Backend", "Dump Assets", false, "Dumps assets to /UserData/AssetImport/ when loading a card with assets.");

            UI = this.GetOrAddComponent<AssetUI>();

            // custom controllers and hooks
            Harmony harmony = Harmony.CreateAndPatchAll(typeof(Hooks), null);

            StudioSaveLoadApi.RegisterExtraBehaviour<AssetSceneController>(GUID);
            CharacterApi.RegisterExtraBehaviour<AssetCharaController>(GUID);

            KKAPI.Maker.AccessoriesApi.AccessoryKindChanged += AccessoryKindChanged;
            KKAPI.Maker.AccessoriesApi.AccessoriesCopied += AccessoryCopied;
            KKAPI.Maker.AccessoriesApi.AccessoryTransferred += AccessoryTransferred;

            instance = this;
            
#if !KK
            // KK uses CPU deltas; Unity 5.6 cannot load this 2019 bundle.
            byte[] data = ResourceUtils.GetEmbeddedResource("assetimport-resources");
            AssetBundle bundle = AssetBundle.LoadFromMemory(data);
            // vertexDelta ComputeShader
            ComputeShader vertexDelta = bundle.LoadAsset<ComputeShader>("VertexDelta");
            vertexDeltaComputeShader = vertexDelta;
#endif
        }

        private void AccessoryTransferred(object sender, KKAPI.Maker.AccessoryTransferEventArgs e)
        {
            int dSlot = e.DestinationSlotIndex;
            int sSlot = e.SourceSlotIndex;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<AssetCharaController>().AccessoryTransferedEvent(sSlot, dSlot);
        }

        private void AccessoryCopied(object sender, KKAPI.Maker.AccessoryCopyEventArgs e)
        {
            ChaFileDefine.CoordinateType dType = e.CopyDestination;
            ChaFileDefine.CoordinateType sType = e.CopySource;
            IEnumerable<int> slots = e.CopiedSlotIndexes;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<AssetCharaController>().AccessoryCopiedEvent((int)sType, (int)dType, slots);
        }

        private void AccessoryKindChanged(object sender, KKAPI.Maker.AccessorySlotEventArgs e)
        {
            int slot = e.SlotIndex;
            KKAPI.Maker.MakerAPI.GetCharacterControl().gameObject.GetComponent<AssetCharaController>().AccessoryChangeEvent(slot);
        }
    }
}
