# MaterialEditor compatibility regression

Requires the .NET 8 SDK and the KK references prepared by `python3 scripts/prepare_dependencies.py`. From the repository root:

```sh
dotnet run --project tests/AssetImport.Compatibility.Tests -c Release
```

This console test exits with a nonzero status when a compatibility check fails. It links the **production** `AssetImport.MaterialEditorLoadDataPatch.cs`; it does not duplicate the matching/rewrite algorithm.

The harness reads the pinned KK MaterialEditor 5.0.0.0 and KKS MaterialEditor 3.13.5.0 DLLs as PE metadata. It checks the actual hook target signatures, the iterator's captured controller/accessory/body fields, and the `MoveNext` instructions. Game reference assemblies from the pinned NuGet packages supply the `Studio.SceneInfo.Load(string)` signature check. Restore uses the repository's `NuGet.Config` and this project's committed `packages.lock.json`; the legacy BepInEx dependency is excluded from runtime and used only to resolve the game metadata fixture packages.

The real instruction stream is adapted to inert reflection fixtures and passed into the production patcher. The resulting guard/callback/anchor fragment is emitted and executed under .NET 8 for all body/accessory flag combinations. The assertions check the captured receiver, flag values, callback order, unchanged body condition, and incoming branch label transfer. Unknown call anchors, branch destinations, and captured receiver layouts must preserve the original instructions and labels.

No game or MaterialEditor code is executed. This checks the cross-version patch contract; it does not replace Windows/Unity/Mono testing of full character, coordinate, and Studio loading.
