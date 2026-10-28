#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"
if [[ ! -f keystore.properties ]]; then
  echo "keystore.properties nao encontrado. Copie keystore.properties.example e preencha os dados da chave de upload." >&2
  exit 1
fi
chmod +x gradlew
./gradlew --no-daemon clean bundleRelease
echo "AAB: $(pwd)/app/build/outputs/bundle/release/app-release.aab"
