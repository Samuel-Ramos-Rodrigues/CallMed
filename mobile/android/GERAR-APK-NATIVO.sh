#!/usr/bin/env sh
set -eu
cd "$(dirname "$0")"
./gradlew --no-daemon clean assembleRelease
printf '%s\n' 'APK nativo: app/build/outputs/apk/release/app-release.apk'
