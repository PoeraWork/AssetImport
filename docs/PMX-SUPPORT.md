# PMX guide

AssetImport reads PMX 2.0 directly and imports the result as a Studio item or accessory.

## Import

1. Extract the model with its companion texture directories intact.
2. Open **Left Alt + I**, choose the `.pmx` file, and select **PMX / VMD preset**. Start with **Scaling-factor: 1**.
3. Enable **Import Armature** for animation and **Import Blendshapes** for vertex expressions. Leave **Material per Renderer** off initially.
4. Inspect the preview, texture paths, and selected physics roots, then finish importing.
5. Wait for **Textures ready** before saving, copying, or switching coordinates.

The complete skeleton is imported. Selecting bones in the physics preview changes the DynamicBone roots, not which bones are imported. Avoid selecting the whole model, head, or waist as a physics root.

## VMD playback with MMDD 3

In Studio, open MMDD's **Load VMD Advanced**, select the imported item, and choose its **AssetImport PMX Auto** definition. The regular character wizard does not list item definitions.

Mappings use the original PMX bone names to match VMD tracks and the imported hierarchy to locate Unity transforms. Supported features include IK limits, fixed axes, non-local rotation/position grants, and vertex expressions. Physics chains and their dependants are excluded from animation synchronization where needed.

The AssetImport runtime extension evaluates each mapped bone's animation, then its append transform, then IK. Enabled IK runs even when its control bone has no VMD animation track; VMD IK switches still apply. Non-local rotation grants compose after the bone's own rotation, and position grants inherit displacement relative to the source's rest pose.

If the definition is missing, select the item and click **Repair selected PMX Auto** in AssetImport, then reopen MMDD's definition selector and bind the regenerated definition. The panel reports whether installation succeeded or only a backup was written.

After updating AssetImport, restart Studio and MMDD to load the updated runtime extension. Regenerate and rebind existing PMX Auto definitions when applying animation-mapping fixes.

AssetImport detects a unique MMDD library under `BepInEx/plugins`. For another location, set **PMX / MMDD Library Path** to the MMDD `Lib` directory and regenerate the mapping. Backups and diagnostic reports are stored under `UserData/AssetImport/MMDD/`.

## Physics

- **Auto PMX physics chains** selects usable chains, including supported skirt and accessory chains.
- **Enable physics after import** controls the initial enabled state.
- **PMX physics preset** applies initial settings for hair, skirts, and accessories.
- **Physics ON / OFF** changes the selected item's or accessory's physics state.
- **Reset physics motion** resets the simulation after seeking or repositioning.

Static PMX body shapes can become DynamicBone colliders. PMX collision groups determine which chains use them. **Hair body collision assist** can add body collisions for hair excluded by those groups; it is optional and takes effect on a new import. **PMX / Enable Body Collisions** controls collider generation and requires reloading the model or scene after a change.

DynamicBone approximates secondary motion; it is not MMD's rigid-body/joint solver. Box colliders are approximated, and fast motion can still cause penetration. Adjust chain roots and parameters for the model through AssetImport or a compatible physics editor. Saved scenes retain their stored settings.

## Textures and color conversion

PNG/JPEG are supported along with BMP, TGA, and common DDS formats. Supported DDS data includes BC1/2/3 and common RGB(A) layouts; BC7, HDR, cubemaps, arrays, and volume textures are not supported. Missing or unsupported textures are reported separately.

**Convert diffuse to KK color** applies a 3D `.cube` LUT to diffuse textures. Set **Textures / KK Color LUT** to the desired PS-to-KK LUT; the default path is `UserData/AssetImport/PS转KK色.cube`. LUT files are supplied separately. Conversion is disabled by default and leaves the original texture files unchanged.

Texture preparation runs in the background with bounded buffers; Unity and MaterialEditor updates run on the game thread. Large images still require decoding, encoding, and GPU upload. Replacing or deleting the target cancels pending work.

## Supported data and limitations

| Data | Support |
| --- | --- |
| PMX version | 2.0; 2.1 is rejected |
| Geometry | Material meshes, UVs, normals, vertex weights, bind pose, and full bone hierarchy |
| Skinning | Linear; SDEF is approximated |
| Morphs | Vertex morphs and groups containing only supported vertex morphs |
| Animation mapping | Supported IK, fixed axes, and non-local rotation/position grants |
| Physics | DynamicBone chains and supported static body colliders |
| Not implemented | Full rigid-body/joint simulation, soft bodies, local grants, bone/UV/material morphs, toon/sphere shading |

Invalid optional IK or cyclic grant/group-morph dependencies can be disabled with diagnostics while usable data is retained. Unsupported mixed morph groups are skipped as a whole. Necessary geometry corruption and cyclic parent hierarchies still fail validation. Check the log and mapping report when a model imports but a motion or expression differs from MMD.

Matching an IK target does not guarantee the same intermediate joint pose as MMD. Multi-axis IK limits, fixed-axis projection, and bone evaluation order can still produce differences. PMX operations marked for evaluation after rigid-body physics are not reproduced as a separate MMD physics stage.

## Troubleshooting

- **Dark colors:** check the material and lighting; if the model requires PS-to-KK conversion, enable the diffuse LUT option before importing.
- **Missing expressions:** enable blend shapes and inspect the mapping report for unsupported or cyclic morphs.
- **Unexpected foot motion:** confirm the matching model definition, scale, and physics roots. Report the affected VMD frame range with the mapping report.
- **Textures absent after saving:** wait for texture processing to complete and inspect missing-file diagnostics.

Include the build identifier and relevant `BepInEx/LogOutput.log` or `output_log.txt` when reporting a problem.
