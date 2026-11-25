@echo off
cd /d "%~dp0"
call gradlew.bat --no-daemon :app:prepararApkSite -PcallmedUsarPrecompilado
if errorlevel 1 goto erro
echo APK e release.json preparados para publicar no site.
pause
exit /b 0
:erro
echo Falha ao preparar a atualizacao. Confira o erro acima.
pause
exit /b 1
