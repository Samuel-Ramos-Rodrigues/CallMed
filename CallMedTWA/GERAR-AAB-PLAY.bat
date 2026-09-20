@echo off
setlocal
cd /d "%~dp0"
echo.
echo ================================================
echo CallMed - Gerar AAB assinado para Google Play
echo ================================================
echo.
if not exist keystore.properties (
  echo ERRO: keystore.properties nao encontrado.
  echo Execute CRIAR-CHAVE-UPLOAD-PLAY.bat ou copie keystore.properties.example.
  pause
  exit /b 1
)
call gradlew.bat --no-daemon clean bundleRelease
if errorlevel 1 (
  echo.
  echo Falha ao gerar o AAB. Veja o erro acima.
  pause
  exit /b 1
)
echo.
echo AAB pronto:
echo %CD%\app\build\outputs\bundle\release\app-release.aab
echo.
echo Esse e o arquivo para enviar no Play Console.
pause
