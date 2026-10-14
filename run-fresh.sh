#!/usr/bin/env bash
# Launches OneWare Studio Desktop with a fresh, isolated profile (no settings, layouts, packages or projects)
# to test the first-run experience without touching your normal installation.
#
# Usage: ./run-fresh.sh [--profile <dir>] [--keep] [--release] [-- <OneWare Studio args>]
#
#   --profile <dir>  Use <dir> as the profile instead of a new temporary one. It is kept after exit, so running
#                    again with the same dir tests the second start.
#   --keep           Keep the temporary profile after OneWare Studio exits.
#   --release        Build and run the Release configuration.
#   -- <args>        Everything after -- is passed to OneWare Studio, e.g.
#                    ./run-fresh.sh -- --configuration-profile ./profile.json
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project="$repo_root/studio/OneWare.Studio.Desktop/OneWare.Studio.Desktop.csproj"

profile=""
keep=false
configuration=Debug
app_args=()

while [[ $# -gt 0 ]]; do
    case "$1" in
        --profile)
            [[ $# -ge 2 ]] || { echo "--profile needs a directory" >&2; exit 1; }
            profile="$2"
            keep=true
            shift 2
            ;;
        --keep) keep=true; shift ;;
        --release) configuration=Release; shift ;;
        --) shift; app_args=("$@"); break ;;
        -h|--help) sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown option: $1 (pass OneWare Studio arguments after --)" >&2; exit 1 ;;
    esac
done

if ! command -v dotnet >/dev/null 2>&1 && [[ -x "$HOME/.dotnet/dotnet" ]]; then
    export PATH="$HOME/.dotnet:$PATH"
fi

if [[ -z "$profile" ]]; then
    profile="$(mktemp -d "${TMPDIR:-/tmp}/oneware-fresh.XXXXXX")"
fi
mkdir -p "$profile"
profile="$(cd "$profile" && pwd)"

cleanup() {
    if [[ "$keep" == true ]]; then
        echo "Profile kept at: $profile"
    else
        rm -rf "$profile"
    fi
}
trap cleanup EXIT

# A separate temp dir gives the instance its own single-instance lock and IPC pipe, so it starts even while
# your normal OneWare Studio is running.
mkdir -p "$profile/tmp"

dotnet build "$project" -c "$configuration" -v minimal

echo "Starting OneWare Studio with a fresh profile in: $profile"
TMPDIR="$profile/tmp" dotnet run --project "$project" -c "$configuration" --no-build -- \
    --oneware-dir "$profile/OneWareStudio" \
    --oneware-appdata-dir "$profile/AppData" \
    --oneware-projects-dir "$profile/OneWareStudio/Projects" \
    "${app_args[@]}"
