using System;
using System.IO;
using System.Linq;
using UnityEngine;
using KKAPI;
using KKAPI.Maker;
using Main = AssetImport.AssetImport;
// ReSharper disable UseObjectOrCollectionInitializer

namespace AssetImport
{
    /// <summary>
    /// Everything IMGUI
    /// </summary>
    internal class AssetUI : MonoBehaviour
    {
        // UI
        // main
        internal static bool UIActive;
        internal static Rect MainWindowRect = new Rect(500, 40, 260, 140);
        internal static string FilePath = "";
        internal static int ScaleSelection = 4;
        internal static readonly float[] Scales = { 10f, 5f, 2f, 1.5f, 1.0f, 0.5f, 0.1f, 0.01f, 0.001f, 0.0001f };
        internal static bool ImportBones = true;
        internal static bool PerRendererMaterials;
        internal static bool LoadBlendshapes = true;
        internal static bool AutoSelectPmxPhysics = true;
        internal static bool EnablePhysicsAfterImport = true;
        internal static bool UsePmxPhysicsPreset = true;
        internal static bool PmxHairCollisionAssist;
        
        // preload
        internal static bool PreloadUI = false;
        internal static Rect PreloadWindowRect = new Rect(Screen.width / 2f - 250, Screen.height / 2f - 350, 500, 600);
        internal static Vector2 ScrollPosition = Vector2.zero;
        internal static bool ArmatureMode;
        private Texture2D _buttonBase;
        private Texture2D _buttonHover;
        internal Texture2D ButtonPressed;
        internal static string CommonPathText;
        internal static bool Replacer;
        internal static string ReplaceString;
        internal static string ReplaceWith = "";
        private Import _armatureImport;
        private string _boneSearch = "";
        private bool _selectedRootsOnly;
        private string _physicsSelectionStatus = "";
        private string _mmddStatus = "";
        private string _physicsActionStatus = "";
        private Vector2 _mainScroll = Vector2.zero;
        private float _mainContentHeight = 140;

        private const KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags SingleFileFlags = KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_FILEMUSTEXIST | KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_LONGNAMES | KKAPI.Utilities.OpenFileDialog.OpenSaveFileDialgueFlags.OFN_EXPLORER;


        void Awake()
        {
            // setup textures
            const float w = 84f / 255f;
            _buttonBase = ColorTexture(new Color(w, w, w));
            _buttonHover = ColorTexture(new Color(w + 0.2f, w + 0.2f, w + 0.2f));
            ButtonPressed = ColorTexture(new Color(w + 0.3f, w + +0.3f, w + 0.3f));
        }

        void Update()
        {
            if (Main.hotkey.Value.IsDown() && (KoikatuAPI.GetCurrentGameMode() == GameMode.Maker || KoikatuAPI.GetCurrentGameMode() == GameMode.Studio))
            {
                UIActive = !UIActive;
                return;
            }
            if (KoikatuAPI.GetCurrentGameMode() != GameMode.Studio && KoikatuAPI.GetCurrentGameMode() != GameMode.Maker)
                UIActive = false;
    
        }

        void OnGUI()
        {
            if (!(KoikatuAPI.GetCurrentGameMode() == GameMode.Maker || KoikatuAPI.GetCurrentGameMode() == GameMode.Studio)) return;
            if (UIActive)
            {
                MainWindowRect = GUI.Window(33361, MainWindowRect, WindowFunction, "Asset Import v" + Main.Version);
                KKAPI.Utilities.IMGUIUtils.EatInputInRect(MainWindowRect);
            }
            if (PreloadUI)
            {
                PreloadWindowRect = GUI.Window(
                    33362,
                    PreloadWindowRect,
                    PreLoadWindowFunction,
                    Main.currentLoadProcess.Import.SourceFileName,
                    KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.window
                );
                KKAPI.Utilities.IMGUIUtils.EatInputInRect(PreloadWindowRect);
            }
        }
        private static Texture2D ColorTexture(Color color)
        {
            Texture2D txt = new Texture2D(1, 1);
            txt.SetPixel(1, 1, color);
            txt.Apply();
            return txt;
        }


        private void WindowFunction(int windowID)
        {
            float viewHeight = Math.Min(_mainContentHeight, Math.Max(180, Screen.height - 100));
            _mainScroll = GUI.BeginScrollView(new Rect(0, 20, 260, viewHeight), _mainScroll,
                new Rect(0, 20, 240, _mainContentHeight));
            var y = 0;
            FilePath = GUI.TextField(new Rect(10, y += 20, 195, 20), FilePath);
            FilePath = FilePath.Replace("\\", "/");
            FilePath = FilePath.Replace("\"", "");
            if (GUI.Button(new Rect(205, y, 25, 20), "..."))
            {
                string dir = (FilePath == "") ? Main.defaultDir.Value : FilePath.Replace(FilePath.Substring(FilePath.LastIndexOf("/")), "");
                string[] file = KKAPI.Utilities.OpenFileDialog.ShowDialog("Open 3D file", dir,
                        "3D files (*.pmx; *.fbx; *.dae; *.gltf; *.3ds; *.ase; *.obj; *.ifc; *.xgl; *.ply; *.dxf; *.lwo; *.lws; *.lxo; *.stl; *.x; *.ac; *.ms3d; *.smd) " +
                        "|*.pmx; *.fbx; *.dae; *.gltf; *.3ds; *.ase; *.obj; *.ifc; *.xgl; *.ply; *.dxf; *.lwo; *.lws; *.lxo; *.stl; *.x; *.ac; *.ms3d; *.smd | All files (*.*)|*.*",
                        "obj", SingleFileFlags);
                if (file != null)
                {
                    FilePath = file[0];
                }
            }

            if (FilePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase) &&
                GUI.Button(new Rect(10, y += 25, 220, 25), "PMX / VMD preset"))
            {
                ImportBones = true;
                LoadBlendshapes = true;
                PerRendererMaterials = false;
                ScaleSelection = 4;
                AutoSelectPmxPhysics = true;
                EnablePhysicsAfterImport = true;
                UsePmxPhysicsPreset = true;
            }
            if (GUI.Button(new Rect(10, y += 25, 220, 25), ImportBones ? "☑ Import Armature" : "☐ Import Armature"))
            {
                ImportBones = !ImportBones;
            }
            if (GUI.Button(new Rect(10, y += 25, 220, 25), LoadBlendshapes ? "☑ Import Blendshapes" : "☐ Import Blendshapes"))
            {
                LoadBlendshapes = !LoadBlendshapes;
            }

            if (GUI.Button(new Rect(10, y += 25, 220, 25), PerRendererMaterials ? "☑ Material per Renderer" : "☐ Material per Renderer"))
            {
                PerRendererMaterials = !PerRendererMaterials;
            }
            if (FilePath.EndsWith(".pmx", StringComparison.OrdinalIgnoreCase))
            {
                GUI.enabled = ImportBones;
                if (GUI.Button(new Rect(10, y += 25, 220, 25), (AutoSelectPmxPhysics ? "☑ " : "☐ ") + "Auto PMX physics chains"))
                    AutoSelectPmxPhysics = !AutoSelectPmxPhysics;
                if (GUI.Button(new Rect(10, y += 25, 220, 25), (EnablePhysicsAfterImport ? "☑ " : "☐ ") + "Enable physics after import"))
                    EnablePhysicsAfterImport = !EnablePhysicsAfterImport;
                if (GUI.Button(new Rect(10, y += 25, 220, 25), (UsePmxPhysicsPreset ? "☑ " : "☐ ") + "PMX physics preset"))
                    UsePmxPhysicsPreset = !UsePmxPhysicsPreset;
                if (GUI.Button(new Rect(10, y += 25, 220, 25), (PmxHairCollisionAssist ? "☑ " : "☐ ") + "Hair body collision assist"))
                    PmxHairCollisionAssist = !PmxHairCollisionAssist;
                GUI.enabled = true;
            }
            
            // no longer used as of v4.0.0
            /*
            if (Path.GetExtension(FilePath).ToLower() == ".fbx")
            {
                if (GUI.Button(new Rect(10, y += 25, 220, 25), DoFbxTranslation ? "☑ Ignore Root translation" : "☐ Ignore Root Translation"))
                {
                    DoFbxTranslation = !DoFbxTranslation;
                }
            }
            */

            GUI.Label(new Rect(10, y+=30, 160, 25), $"Scaling-factor: {Scales[ScaleSelection]}");
            if (ScaleSelection == 0) GUI.enabled = false;
            if (GUI.Button(new Rect(190, y, 20, 20), "+"))
            {
                ScaleSelection--;
            }
            GUI.enabled = true;
            if (ScaleSelection == 9) GUI.enabled = false;
            if (GUI.Button(new Rect(210, y, 20, 20), "-"))
            {
                ScaleSelection++;
            }
            GUI.enabled = true;
            if (string.IsNullOrEmpty(FilePath)) GUI.enabled = false;
            if (GUI.Button(new Rect(10, y+=25, 220, 30), "Import"))
            {
                _mmddStatus = "";
                _physicsActionStatus = "";
                if (KoikatuAPI.GetCurrentGameMode() == GameMode.Studio)
                    Main.asc.Import(FilePath, new Vector3(Scales[ScaleSelection], Scales[ScaleSelection], Scales[ScaleSelection]), ImportBones, PerRendererMaterials, LoadBlendshapes);
                else if (KoikatuAPI.GetCurrentGameMode() == GameMode.Maker)
                {
                    int slot = AccessoriesApi.SelectedMakerAccSlot;
                    if (slot != -1)
                    {
                        MakerAPI.GetCharacterControl()
                        .gameObject.GetComponentInChildren<AssetCharaController>()?
                        .Import(
                            slot,
                            MakerAPI.GetCharacterControl().nowCoordinate.accessory.parts[slot].type,
                            MakerAPI.GetCharacterControl().nowCoordinate.accessory.parts[slot].parentKey,
                            FilePath,
                            new Vector3(Scales[ScaleSelection], Scales[ScaleSelection], Scales[ScaleSelection]),
                            ImportBones,
                            PerRendererMaterials,
                            LoadBlendshapes);
                    }
                    else Main.Logger.LogMessage("Please select an accessory which you want to replace!");
                }
            }
            GUI.enabled = true;

            var mode = KoikatuAPI.GetCurrentGameMode();
            AssetCharaController character = mode == GameMode.Maker
                ? MakerAPI.GetCharacterControl()?.gameObject.GetComponentInChildren<AssetCharaController>() : null;
            string physicsStatus = mode == GameMode.Studio ? Main.asc?.GetSelectedPhysicsStatus()
                : character?.GetSelectedAccessoryPhysicsStatus();
            var physicsStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            string displayPhysicsStatus = physicsStatus ?? "Select an imported model to control its physics.";
            float physicsHeight = physicsStyle.CalcHeight(new GUIContent(displayPhysicsStatus), 220);
            GUI.Label(new Rect(10, y += 35, 220, physicsHeight), displayPhysicsStatus, physicsStyle);
            y += (int)physicsHeight;
            GUI.enabled = !PreloadUI && (mode == GameMode.Studio ? Main.asc != null : character != null);
            if (GUI.Button(new Rect(10, y += 5, 107, 25), "Physics ON"))
                _physicsActionStatus = mode == GameMode.Studio ? Main.asc.SetSelectedPhysicsEnabled(true)
                    : character.SetSelectedAccessoryPhysicsEnabled(true);
            if (GUI.Button(new Rect(122, y, 108, 25), "Physics OFF"))
                _physicsActionStatus = mode == GameMode.Studio ? Main.asc.SetSelectedPhysicsEnabled(false)
                    : character.SetSelectedAccessoryPhysicsEnabled(false);
            if (GUI.Button(new Rect(10, y += 30, 220, 25), "Reset physics motion"))
                _physicsActionStatus = mode == GameMode.Studio ? Main.asc.ResetSelectedPhysics()
                    : character.ResetSelectedAccessoryPhysics();
            GUI.enabled = true;
            y += 25;
            if (!string.IsNullOrEmpty(_physicsActionStatus))
            {
                string actionFeedback = "Last action: " + _physicsActionStatus;
                float height = physicsStyle.CalcHeight(new GUIContent(actionFeedback), 220);
                GUI.Label(new Rect(10, y += 5, 220, height), actionFeedback, physicsStyle);
                y += (int)height;
            }

            if (KoikatuAPI.GetCurrentGameMode() == GameMode.Studio)
            {
                GUI.enabled = !PreloadUI && Main.asc != null;
                if (GUI.Button(new Rect(10, y += 35, 220, 25), "Repair selected PMX Auto"))
                    _mmddStatus = Main.asc.RepairSelectedPmxAuto();
                GUI.enabled = true;
                if (!string.IsNullOrEmpty(_mmddStatus))
                {
                    var statusStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                    float height = statusStyle.CalcHeight(new GUIContent(_mmddStatus), 220);
                    GUI.Label(new Rect(10, y += 30, 220, height), _mmddStatus, statusStyle);
                    y += (int)height;
                }
            }

            GUI.EndScrollView();
            _mainContentHeight = y + 20;
            MainWindowRect.height = Math.Min(_mainContentHeight, Math.Max(180, Screen.height - 100)) + 40;
            GUI.DragWindow(new Rect(0, 0, 260, 20));
        }

        private void PreLoadWindowFunction(int windowID)
        {
            if (!PreloadUI) return;
            if (!(Main.currentLoadProcess.Import.HasBones || Main.currentLoadProcess.Import.HasTextures))
            {
                if (KoikatuAPI.GetCurrentGameMode() == GameMode.Studio)
                {
                    Main.asc?.FinishLoadProcess(Main.currentLoadProcess);
                    _mmddStatus = Main.currentLoadProcess.Import.LastMmddExportStatus;
                }
                else if (KoikatuAPI.GetCurrentGameMode() == GameMode.Maker)
                    MakerAPI.GetCharacterControl().gameObject.GetComponentInChildren<AssetCharaController>()?.FinishLoadProcess(Main.currentLoadProcess);
                return;
            }

            if (!(Main.currentLoadProcess.Import.HasBones && Main.currentLoadProcess.Import.HasTextures)) GUI.enabled = false;
            if (GUI.Button(new Rect(10, 20, 480, 25), ArmatureMode ? "Switch to Textures" : "Switch to Armature"))
            {
                ArmatureMode = !ArmatureMode;
            }
            GUI.enabled = true;

            if (ArmatureMode)
            {
                Import import = Main.currentLoadProcess.Import;
                if (_armatureImport != import)
                {
                    _armatureImport = import;
                    _boneSearch = "";
                    _selectedRootsOnly = false;
                    _physicsSelectionStatus = import.InitialPhysicsSelectionStatus;
                    ScrollPosition = Vector2.zero;
                }
                // custom button styles
                GUIStyle normal = new GUIStyle(KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.button);
                normal.normal.background = _buttonBase;
                normal.hover.background = _buttonHover;
                normal.active.background = ButtonPressed;
                normal.alignment = TextAnchor.UpperLeft;

                GUIStyle dynamicRoot = new GUIStyle(normal);
                dynamicRoot.normal.textColor = Color.green;
                dynamicRoot.hover.textColor = Color.green;

                GUIStyle dynamic = new GUIStyle(normal);
                dynamic.normal.textColor = new Color(0.7f, 1, 0);
                dynamic.hover.textColor = new Color(0.7f, 1, 0);

                GUIStyle arrow = new GUIStyle(normal);
                arrow.alignment = TextAnchor.MiddleCenter;
                arrow.hover.textColor = new Color(1, 1, 0);

                GUI.Label(new Rect(10, 48, 480, 40), "All bones are already imported. Click a name to select a PHYSICS ROOT.\nGreen = physics root; yellow-green = children. Do not select the whole skeleton.");

                float controlsY = 92;
                if (import.IsPmx)
                {
                    if (GUI.Button(new Rect(10, controlsY, 235, 25), "All PMX physics chains"))
                    {
                        _physicsSelectionStatus = import.SelectPmxPhysicsRoots();
                        ScrollPosition = Vector2.zero;
                    }
                    if (GUI.Button(new Rect(250, controlsY, 240, 25), "Add hair roots (incl. ponytails)"))
                    {
                        _physicsSelectionStatus = import.SelectPmxHairRoots();
                        ScrollPosition = Vector2.zero;
                    }
                    controlsY += 29;
                    if (GUI.Button(new Rect(10, controlsY, 200, 25), "Clear physics roots"))
                    {
                        import.ClearPhysicsRoots();
                        _physicsSelectionStatus = "Physics roots cleared. The full skeleton is still imported.";
                    }
                    if (GUI.Button(new Rect(215, controlsY, 275, 25), (import.PhysicsEnabledAfterImport ? "☑ " : "☐ ") + "Enable physics after import"))
                        import.PhysicsEnabledAfterImport = !import.PhysicsEnabledAfterImport;
                    controlsY += 29;
                    if (GUI.Button(new Rect(10, controlsY, 480, 25), "Physics settings: " + (import.PhysicsProfile == 1 ? "PMX preset (preview)" : "Legacy defaults")))
                        import.PhysicsProfile = import.PhysicsProfile == 1 ? 0 : 1;
                    controlsY += 29;
                    if (GUI.Button(new Rect(10, controlsY, 480, 25), (import.PmxHairCollisionAssist ? "☑ " : "☐ ") + "Hair body collision assist (override PMX groups)"))
                        import.PmxHairCollisionAssist = !import.PmxHairCollisionAssist;
                    controlsY += 29;

                }
                GUI.Label(new Rect(10, controlsY, 50, 25), "Search:");
                string search = GUI.TextField(new Rect(60, controlsY, 235, 25), _boneSearch);
                if (search != _boneSearch) { _boneSearch = search; ScrollPosition = Vector2.zero; }
                if (GUI.Button(new Rect(300, controlsY, 190, 25), (_selectedRootsOnly ? "☑ " : "☐ ") + "Selected roots only"))
                {
                    _selectedRootsOnly = !_selectedRootsOnly;
                    ScrollPosition = Vector2.zero;
                }
                controlsY += 29;
                if (GUI.Button(new Rect(10, controlsY, 110, 25), "Expand all"))
                    foreach (var node in import.BoneNodes.OrderBy(n => n.Depth)) node.SetNodeOpen(true);
                if (GUI.Button(new Rect(125, controlsY, 110, 25), "Collapse all"))
                    foreach (var node in import.BoneNodes.OrderByDescending(n => n.Depth)) node.SetNodeOpen(false);
                GUI.Label(new Rect(245, controlsY, 245, 25), "Selected physics roots: " + import.BoneNodes.Count(n => n.IsDynamicRoot));
                controlsY += 29;
                if (!string.IsNullOrEmpty(_physicsSelectionStatus))
                {
                    var statusStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                    float height = statusStyle.CalcHeight(new GUIContent(_physicsSelectionStatus), 480);
                    GUI.Label(new Rect(10, controlsY, 480, height), _physicsSelectionStatus, statusStyle);
                    controlsY += height + 4;
                }

                bool filtering = _selectedRootsOnly || !string.IsNullOrEmpty(_boneSearch);
                var visibleNodes = import.BoneNodes.Where(node =>
                    (filtering || node.UIActive) && (!_selectedRootsOnly || node.IsDynamicRoot) &&
                    (string.IsNullOrEmpty(_boneSearch) || node.GameObject.name.IndexOf(_boneSearch, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
                float contentWidth = visibleNodes.Length == 0 ? 470 : Math.Max(470, visibleNodes.Max(n => 65 + n.Depth * 20 + n.GameObject.name.Length * 8));
                Rect scrollContentRect = new Rect(0, 0, contentWidth, 22 * visibleNodes.Length + 10);
                ScrollPosition = GUI.BeginScrollView(new Rect(0, controlsY, 490, Math.Max(80, PreloadWindowRect.height - controlsY - 55)), ScrollPosition, scrollContentRect);

                var y = 0;
                foreach (BoneNode node in visibleNodes)
                {
                    int x = 10 + node.Depth * 20;
                    if (GUI.Button(new Rect(x + 25, y, contentWidth - x - 25, 22), node.GameObject.name, node.IsDynamicRoot ? dynamicRoot : node.IsDynamic ? dynamic : normal))
                    {
                        node.SetDynamic(!node.IsDynamic);
                    }
                    if (node.Children.Count > 0)
                        if (GUI.Button(new Rect(x, y, 25, 22), node.NodeOpen ? "▼" : "►", arrow))
                        {
                            node.SetNodeOpen(!node.NodeOpen);
                        }
                    y += 22;
                }
                GUI.EndScrollView();
            }
            else
            {
                // custom styles 
                GUIStyle goodPath = new GUIStyle(KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.textField);
                goodPath.normal.textColor = Color.green;
                goodPath.hover.textColor = Color.green;
                goodPath.focused.textColor = Color.green;

                GUIStyle badPath = new GUIStyle(goodPath);
                badPath.normal.textColor = Color.red;
                badPath.hover.textColor = Color.red;
                badPath.focused.textColor = Color.red;

                GUIStyle label = new GUIStyle(KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.label);
                label.alignment = TextAnchor.MiddleCenter;

                GUIStyle box = new GUIStyle(KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.box);
                box.alignment = TextAnchor.MiddleCenter;
                
                if (GUI.Button(new Rect(10, 50, 480, 25), Replacer ? "◀ Replace in all paths ▶" : "◀ Replace common or full Path: ▶", KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.button))
                {
                    Replacer = !Replacer;
                }
                if (!Replacer)
                {
                    CommonPathText = GUI.TextField(new Rect(10, 75, 335, 25), CommonPathText, Directory.Exists(CommonPathText) ? goodPath : badPath);

                    if (!Directory.Exists(CommonPathText)) GUI.enabled = false;
                    if (GUI.Button(new Rect(350, 75, 480 - 350 - 70, 25), "Common"))
                    {
                        Main.currentLoadProcess.Import.CommonPath = CommonPathText;
                    }
                    if (GUI.Button(new Rect(350+70, 75, 480 - 350 - 70, 25), "Full"))
                    {
                        Main.currentLoadProcess.Import.ReplacePathInAllTextures(CommonPathText);
                    }
                    GUI.enabled = true;
                }
                else
                {
                    if (ReplaceString == null) ReplaceString = CommonPathText;
                    ReplaceString = GUI.TextField(new Rect(10, 75, 215, 25), ReplaceString, KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.textField);
                    if (GUI.Button(new Rect(230, 75, 40, 25), "with")) 
                    { 
                        foreach (Material mat in Main.currentLoadProcess.Import.MaterialTextures.Keys)
                        {
                            foreach (TexturePath tPath in Main.currentLoadProcess.Import.MaterialTextures[mat])
                            {
                                tPath.Path = tPath.Path.Replace(ReplaceString, ReplaceWith);
                            }
                        }
                    }
                    ReplaceWith = GUI.TextField(new Rect(275, 75, 215, 25), ReplaceWith, KKAPI.Utilities.IMGUIUtils.SolidBackgroundGuiSkin.textField);
                }

                Rect scrollContentRect = new Rect(0, 0, 470, 0);
                foreach (Material mat in Main.currentLoadProcess.Import.MaterialTextures.Keys)
                {
                    scrollContentRect.height += 30;
                    scrollContentRect.height += 30 * Main.currentLoadProcess.Import.MaterialTextures[mat].Count(tPath => tPath.Type == Assimp.TextureType.Diffuse || tPath.Type == Assimp.TextureType.Normals);
                }

                ScrollPosition = GUI.BeginScrollView(new Rect(0, 110, 490, 440), ScrollPosition, scrollContentRect);

                var y = 0;
                foreach (Material mat in Main.currentLoadProcess.Import.MaterialTextures.Keys)
                {
                    GUI.Box(new Rect(10, y, 460, 25), mat.name);

                    foreach (TexturePath tPath in Main.currentLoadProcess.Import.MaterialTextures[mat].Where(tPath => tPath.Type == Assimp.TextureType.Diffuse || tPath.Type == Assimp.TextureType.Normals))
                    {
                        y += 30;
                        if (!tPath.PathOkay())
                        {
                            GUI.enabled = false;
                            if (tPath.Use) tPath.Use = false;
                        }
                        if (GUI.Button(new Rect(10, y, 25, 25), tPath.Use ? "☑" : "☐"))
                        {
                            tPath.Use = !tPath.Use;
                        }
                        GUI.enabled = true;
                        GUI.Box(new Rect(35, y, 100, 25), "");
                        GUI.Label(new Rect(35, y, 100, 25), tPath.Type.ToString(), label);
                        tPath.Path = GUI.TextField(
                            new Rect(135, y, 470 - 160, 25),
                            tPath.Path,
                            tPath.PathOkay() ? goodPath : badPath
                        );
                        if (!GUI.Button(new Rect(470 - 25, y, 25, 25), "...")) continue;
                        tPath.Path = tPath.Path.Replace("\\", "/");
                        string[] file = KKAPI.Utilities.OpenFileDialog.ShowDialog("Select Texture", Main.currentLoadProcess.Import.SourceIdentifier,
                            "Image files (*.png; *.jpg; *.jpeg; *.tga; *.bmp; *.dds) |*.png; *.jpg; *.jpeg; *.tga; *.bmp; *.dds | All files (*.*)|*.*", "png", SingleFileFlags);
                        if (file != null)
                        {
                            tPath.Path = file[0];
                        }
                    }

                    y += 30;
                }

                GUI.EndScrollView();
            }

            GUI.enabled = true;

            if (!GUI.Button(new Rect(10, PreloadWindowRect.height - 45, PreloadWindowRect.width - 20, 40),
                    "Finish")) return;
            if (KoikatuAPI.GetCurrentGameMode() == GameMode.Studio)
            {
                Main.asc.FinishLoadProcess(Main.currentLoadProcess);
                _mmddStatus = Main.currentLoadProcess.Import.LastMmddExportStatus;
            }
            else if (KoikatuAPI.GetCurrentGameMode() == GameMode.Maker)
                MakerAPI.GetCharacterControl().gameObject.GetComponentInChildren<AssetCharaController>()?.FinishLoadProcess(Main.currentLoadProcess);
        }
    }
}
