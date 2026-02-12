#!/usr/bin/env python3

__license__ = "MIT"

import argparse
import base64
import binascii
import concurrent.futures
import hashlib
import json
import subprocess
import tempfile
import urllib.request
import shutil
from pathlib import Path


def main() -> None:
    # Bump this to latest freedesktop runtime version.
    freedesktop_default = "24.08"
    # Bump this to an LTS dotnet version.
    dotnet_default = "8"

    parser = argparse.ArgumentParser()
    parser.add_argument("output", help="The output JSON sources file")
    parser.add_argument("project", nargs="+", help="The project file(s)")
    parser.add_argument(
        "--runtime",
        "-r",
        nargs="+",
        default=[None],
        help="The target runtime(s) to restore packages for",
    )
    parser.add_argument(
        "--freedesktop",
        "-f",
        help="The target version of the freedesktop sdk to use",
        default=freedesktop_default,
    )
    parser.add_argument(
        "--dotnet",
        "-d",
        help="The target version of dotnet to use",
        default=dotnet_default,
    )
    parser.add_argument(
        "--destdir",
        help="The directory the generated sources file will save sources to",
        default="nuget-sources",
    )
    parser.add_argument(
        "--only-arches", help="Limit the source to this Flatpak arch", default=None
    )
    parser.add_argument(
        "--dotnet-args",
        "-a",
        nargs=argparse.REMAINDER,
        help="Additional arguments to pass to the dotnet command",
    )
    args = parser.parse_args()

    sources = []
    with tempfile.TemporaryDirectory(dir=Path()) as tmp:

        def restore_project(project: str, runtime: str | None) -> None:
            subprocess.run(
                [
                    "flatpak",
                    "run",
                    "--env=DOTNET_CLI_TELEMETRY_OPTOUT=true",
                    "--env=DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true",
                    "--command=sh",
                    f"--runtime=org.freedesktop.Sdk//{args.freedesktop}",
                    "--share=network",
                    "--filesystem=host",
                    f"org.freedesktop.Sdk.Extension.dotnet{args.dotnet}//{args.freedesktop}",
                    "-c",
                    f'PATH="${{PATH}}:/usr/lib/sdk/dotnet{args.dotnet}/bin" LD_LIBRARY_PATH="$LD_LIBRARY_PATH:/usr/lib/sdk/dotnet{args.dotnet}/lib" exec dotnet restore "$@"',
                    "--",
                    "--packages",
                    tmp,
                    project,
                ]
                + (["-r", runtime] if runtime else [])
                + (args.dotnet_args or []),
                check=False,
            )

        with concurrent.futures.ThreadPoolExecutor() as executor:
            futures = []
            for project in args.project:
                if args.runtime:
                    for runtime in args.runtime:
                        futures.append(
                            executor.submit(restore_project, project, runtime)
                        )
                else:
                    futures.append(executor.submit(restore_project, project, None))
            concurrent.futures.wait(futures)

        # Detect the actual .NET runtime version being used (not SDK version)
        print("Detecting .NET runtime version...")
        try:
            result = subprocess.run(
                [
                    "flatpak",
                    "run",
                    "--env=DOTNET_CLI_TELEMETRY_OPTOUT=true",
                    "--command=sh",
                    f"--runtime=org.freedesktop.Sdk//{args.freedesktop}",
                    f"org.freedesktop.Sdk.Extension.dotnet{args.dotnet}//{args.freedesktop}",
                    "-c",
                    f'PATH="${{PATH}}:/usr/lib/sdk/dotnet{args.dotnet}/bin" exec dotnet --list-runtimes',
                ],
                capture_output=True,
                text=True,
                check=True,
            )
            # Parse output to find Microsoft.NETCore.App runtime version
            # Output looks like: Microsoft.NETCore.App 10.0.3 [/usr/lib/sdk/dotnet10/shared/Microsoft.NETCore.App]
            for line in result.stdout.strip().split('\n'):
                if 'Microsoft.NETCore.App' in line:
                    parts = line.split()
                    if len(parts) >= 2:
                        dotnet_version = parts[1]
                        print(f"Detected .NET runtime version: {dotnet_version}")
                        break
            else:
                # Fallback if parsing fails
                dotnet_version = f"{args.dotnet}.0.3"
                print(f"Failed to parse runtime version, using fallback: {dotnet_version}")
        except subprocess.CalledProcessError:
            # Fallback to a reasonable default
            dotnet_version = f"{args.dotnet}.0.3"
            print(f"Failed to detect version, using fallback: {dotnet_version}")

        # Explicitly download runtime and crossgen2 packages for each runtime
        # These packages aren't always restored automatically for all runtimes
        if args.runtime and args.runtime != [None]:
            print("Downloading runtime packages for specified runtimes...")
            
            # Package types to download: runtime packages and CrossGen2
            package_templates = [
                "microsoft.netcore.app.runtime.{runtime}",
                "microsoft.aspnetcore.app.runtime.{runtime}",
                "microsoft.netcore.app.crossgen2.{runtime}",
            ]
            
            for runtime in args.runtime:
                if runtime:  # Skip None values
                    for template in package_templates:
                        package_name = template.format(runtime=runtime)
                        package_id = package_name.lower()
                        filename = f"{package_id}.{dotnet_version}.nupkg"
                        url = f"https://api.nuget.org/v3-flatcontainer/{package_id}/{dotnet_version}/{filename}"
                        
                        # Create directory structure matching NuGet package layout
                        dest_path = Path(tmp) / package_id / dotnet_version
                        dest_path.mkdir(parents=True, exist_ok=True)
                        
                        nupkg_path = dest_path / filename
                        sha512_path = dest_path / f"{package_id}.{dotnet_version}.nupkg.sha512"
                        
                        # Skip if already downloaded
                        if nupkg_path.exists() and sha512_path.exists():
                            print(f"  ✓ {package_name} {dotnet_version} already present")
                            continue
                        
                        print(f"  Downloading {package_name} {dotnet_version}...")
                        try:
                            # Download the nupkg file
                            with urllib.request.urlopen(url) as response:
                                with open(nupkg_path, 'wb') as out_file:
                                    shutil.copyfileobj(response, out_file)
                            
                            # Try to download the sha512 file, if it doesn't exist, compute it
                            sha512_url = f"https://api.nuget.org/v3-flatcontainer/{package_id}/{dotnet_version}/{package_id}.{dotnet_version}.nupkg.sha512"
                            try:
                                with urllib.request.urlopen(sha512_url) as response:
                                    with open(sha512_path, 'wb') as out_file:
                                        shutil.copyfileobj(response, out_file)
                            except urllib.error.HTTPError as e:
                                if e.code == 404:
                                    # sha512 file doesn't exist, compute it ourselves
                                    print(f"    Computing SHA512 hash (file not provided by NuGet)...")
                                    sha512_hash = hashlib.sha512()
                                    with open(nupkg_path, 'rb') as f:
                                        for chunk in iter(lambda: f.read(4096), b''):
                                            sha512_hash.update(chunk)
                                    # Write in the same format as NuGet (.sha512 files contain base64)
                                    sha512_base64 = base64.b64encode(sha512_hash.digest()).decode('ascii')
                                    with open(sha512_path, 'w') as f:
                                        f.write(sha512_base64)
                                else:
                                    raise
                            
                            print(f"    ✓ Downloaded {filename}")
                        except Exception as e:
                            print(f"    ✗ Failed to download {filename}: {e}")

        for path in Path(tmp).glob("**/*.nupkg.sha512"):
            name = path.parent.parent.name
            version = path.parent.name
            filename = "{}.{}.nupkg".format(name, version)
            url = "https://api.nuget.org/v3-flatcontainer/{}/{}/{}".format(
                name, version, filename
            )

            with path.open() as fp:
                sha512 = binascii.hexlify(base64.b64decode(fp.read())).decode("ascii")

            data = {
                "type": "file",
                "url": url,
                "sha512": sha512,
                "dest": args.destdir,
                "dest-filename": filename,
            }

            if args.only_arches is not None:
                data["only-arches"] = [args.only_arches]

            sources.append(data)

    with open(args.output, "w", encoding="utf-8") as fp:
        json.dump(sorted(sources, key=lambda n: n.get("dest-filename")), fp, indent=4)


if __name__ == "__main__":
    main()