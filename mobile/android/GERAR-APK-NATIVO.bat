@echo off
cd /d "%~dp0"
call gradlew.bat --no-daemon clean assembleRelease
if errorlevel 1 goto erro
echo APK nativo: app\build\outputs\apk\release\app-release.apk
pause
exit /b 0
:erro
echo Falha ao compilar. Confira o erro acima.
pause
exit /b 1
