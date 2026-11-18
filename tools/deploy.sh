#!/usr/bin/env bash
# Builds DMCS (Release) and copies DMCS.dll + manifest.json into BOTH game installs: the Linux Steam copy and the
# Windows Steam copy (Vortex manages that Mods folder; DMCS lies next to the others). Refuses a dirty tree or a
# build with probe code, then checks that all three copies have the same md5.
#   tools/deploy.sh
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
LINUX_MODS="${DMCS_LINUX_MODS:-$HOME/.local/share/Steam/steamapps/common/Blade & Sorcery/BladeAndSorcery_Data/StreamingAssets/Mods}"
WINDOWS_MODS="${DMCS_WINDOWS_MODS:-/run/media/nadi/Programs/Steam/steamapps/common/Blade & Sorcery/BladeAndSorcery_Data/StreamingAssets/Mods}"
cd "$ROOT"
if [ -n "$(git status --porcelain -- src)" ]; then
  echo "deploy: src has uncommitted changes; commit first" >&2
  exit 1
fi
export DOTNET_ROOT=${DOTNET_ROOT:-$HOME/.dotnet}
export PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build src/DMCS/DMCS.csproj -c Release -nologo -v q
BUILD=src/DMCS/bin/Release/net472/DMCS.dll
if strings "$BUILD" | grep -q PROBE; then
  echo "deploy: the build contains probe code" >&2
  exit 1
fi
for mods in "$LINUX_MODS" "$WINDOWS_MODS"; do
  if [ ! -d "$mods" ]; then
    echo "deploy: Mods folder not found: $mods" >&2
    exit 1
  fi
  mkdir -p "$mods/DMCS"
  cp "$BUILD" src/DMCS/manifest.json "$mods/DMCS/"
done
want=$(md5sum "$BUILD" | cut -d' ' -f1)
for mods in "$LINUX_MODS" "$WINDOWS_MODS"; do
  got=$(md5sum "$mods/DMCS/DMCS.dll" | cut -d' ' -f1)
  [ "$got" = "$want" ] || { echo "deploy: md5 mismatch in $mods" >&2; exit 1; }
done
echo "deployed $(git rev-parse --short HEAD) (DMCS.dll md5 $want) to Linux and Windows"
