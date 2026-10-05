#!/usr/bin/env python3
"""Build both games, then run the repository's portable regression programs."""
import argparse
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet", help="SDK 8 executable")
    parser.add_argument("--no-restore", action="store_true")
    args = parser.parse_args()
    common = ["-c", "Release", "--disable-build-servers", "-p:UseSharedCompilation=false", "-v:q"]
    if args.no_restore:
        common.append("--no-restore")
    subprocess.run([args.dotnet, "build", "src/AssetImport/AssetImport.csproj", "-m:1", *common], cwd=ROOT, check=True)
    for project in sorted((ROOT / "tests").glob("AssetImport.*.Tests/*.csproj")):
        print("Checking:", project.parent.name, flush=True)
        subprocess.run([args.dotnet, "run", "--project", str(project), *common], cwd=ROOT, check=True)
    subprocess.run([sys.executable, "scripts/check_import_failure.py", "--dotnet", args.dotnet], cwd=ROOT, check=True)
    subprocess.run([sys.executable, "-m", "unittest", "discover", "-s", "tests/packaging", "-v"], cwd=ROOT, check=True)
    print("Portable checks passed. Windows game validation is separate.")


if __name__ == "__main__":
    main()
