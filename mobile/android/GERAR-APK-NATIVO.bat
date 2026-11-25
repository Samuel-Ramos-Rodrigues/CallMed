@echo off
cd /d "%~dp0"
call gradlew.bat --no-daemon :app:prepararApkSite
if errorlevel 1 goto erro
echo APK e release.json preparados para o site.
pause
exit /b 0
:erro
echo Falha ao compilar. Confira o erro acima.
pause
exit /b 1
