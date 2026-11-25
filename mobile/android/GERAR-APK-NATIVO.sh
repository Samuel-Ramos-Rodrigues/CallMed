#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
./gradlew --no-daemon :app:prepararApkSite
printf '%s\n' 'APK e release.json preparados para o site.'
