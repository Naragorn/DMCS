#!/usr/bin/env bash
# Builds DMCS (Release) from a committed tree and packs the mod folder as a zip for testers:
#   dist/DMCS-<ModVersion>.zip  containing  DMCS/DMCS.dll, DMCS/manifest.json, DMCS/Skills/*.json,
#                                           DMCS/README.md, DMCS/BETA-TESTING.md
# The zip unpacks straight into BladeAndSorcery_Data/StreamingAssets/Mods/. Refuses uncommitted src changes and a build
# with probe code (as tools/deploy.sh). The game reads only *.json from a mod folder, so the .md files do not disturb it.
#   tools/package.sh
set -euo pipefail
ROOT=$(cd "$(dirname "$0")/.." && pwd)
cd "$ROOT"
if [ -n "$(git status --porcelain -- src README.md docs/BETA-TESTING.md)" ]; then
  echo "package: src, README.md or docs/BETA-TESTING.md has uncommitted changes; commit first" >&2
  exit 1
fi
export DOTNET_ROOT=${DOTNET_ROOT:-$HOME/.dotnet}
export PATH="$DOTNET_ROOT:$PATH" DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet build src/DMCS/DMCS.csproj -c Release -nologo -v q
BUILD=src/DMCS/bin/Release/net472/DMCS.dll
if strings "$BUILD" | grep -q PROBE; then
  echo "package: the build contains probe code" >&2
  exit 1
fi
VERSION=$(python3 -c 'import json,sys; print(json.load(open(sys.argv[1]))["ModVersion"])' src/DMCS/manifest.json)
STAGE=$(mktemp -d)
trap 'rm -rf "$STAGE"' EXIT
mkdir -p "$STAGE/DMCS/Skills"
cp "$BUILD" src/DMCS/manifest.json README.md docs/BETA-TESTING.md "$STAGE/DMCS/"
cp src/DMCS/Skills/*.json "$STAGE/DMCS/Skills/"
mkdir -p dist
ZIP="$ROOT/dist/DMCS-$VERSION.zip"
rm -f "$ZIP"
(cd "$STAGE" && TZ=UTC find DMCS -exec touch -t 202601010000 {} + && zip -qrX "$ZIP" DMCS)
echo "packed $ZIP from $(git rev-parse --short HEAD) (DMCS.dll md5 $(md5sum "$BUILD" | cut -d' ' -f1), $(ls src/DMCS/Skills/*.json | wc -l) skill files)"
