#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if ! command -v dotnet >/dev/null 2>&1; then
  export DOTNET_ROOT="${DOTNET_ROOT:-/workspace/.dotnet}"
  export PATH="$DOTNET_ROOT:$PATH"
  if [[ ! -x "$DOTNET_ROOT/dotnet" ]]; then
    installer=$(mktemp)
    curl --fail --silent --show-error --location https://dot.net/v1/dotnet-install.sh -o "$installer"
    bash "$installer" --version 10.0.100 --install-dir "$DOTNET_ROOT"
    rm -f "$installer"
  fi
fi
export DOTNET_CLI_HOME="${DOTNET_CLI_HOME:-/workspace/.dotnet-home}"
export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet restore KafkaMicroservice.slnx --locked-mode
dotnet build KafkaMicroservice.slnx --no-restore -m:2
docker compose pull
