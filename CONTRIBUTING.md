# Contributing

## Build

Install Git, the .NET 8 SDK, and Python 3.9 or later. A Unity editor installation is not required. From the repository root:

```powershell
python scripts/prepare_dependencies.py
dotnet restore src/AssetImport/AssetImport.csproj --locked-mode
dotnet build src/AssetImport/AssetImport.csproj -c Release
```

The project builds KK (`net35`) and KKS (`net462`). Downloaded build references are pinned by hash in `scripts/dependencies.json`; NuGet versions are recorded in the lock files. KK output uses AssimpNet and Assimp 5, while KKS uses AssimpNetter and its matching Assimp runtime.

## Automated checks

```powershell
python scripts/validate.py
```

This builds both targets and runs the standalone C# regression programs, packaging tests, and MMDD Python tests. The C# checks are console programs, not `dotnet test` projects. Use `--dotnet <path>` for a custom SDK executable or `--no-restore` after restoring dependencies.

Checks cover geometry, hierarchy, textures, PMX parsing, animation mapping, physics configuration, caching, and compiled MaterialEditor compatibility. They use engine substitutes or assembly metadata; rendering and Unity physics still require in-game verification.

For model-specific diagnostics, see [tests/README.md](tests/README.md). Add small synthetic fixtures for regressions where practical; keep third-party models, motions, logs, and generated reports outside tracked source.

## Packaging

Build from a clean commit, then run:

```powershell
dotnet build src/AssetImport/AssetImport.csproj -c Release
python scripts/package.py --source
python scripts/prepare_kks_runtime.py
python scripts/package_kks.py
```

Packages are written to `artifacts/g<commit>/` with a `BUILD.txt`, file manifest, and SHA-256 checksum. The packager verifies that the DLL was built from the current clean commit. Rebuild after committing to refresh the embedded identity.

`package.py --dll-update` creates a plugin-only update for an installation with matching runtime dependencies. `--test-assets` creates a separate archive of synthetic fixtures. `--source` exports committed files. Neither game package requires local test notes.

Both packagers accept `--notes <file>` to explicitly include an additional UTF-8 text file as `TEST-NOTES.txt`. KKS runtime downloads and checksums are defined in `scripts/kks-runtime.json`.

## Changes and bug reports

Describe the problem, resulting behavior, and relevant validation in a pull request. Keep changes focused and preserve compatibility with both target frameworks. Check imported scene/card restoration when changing persistence or hierarchy behavior.

For bugs, include the game, build identifier, reproduction steps, import options, and relevant log excerpts. For VMD playback, specify the affected frame range. Remove personal paths and unrelated log content before posting publicly.