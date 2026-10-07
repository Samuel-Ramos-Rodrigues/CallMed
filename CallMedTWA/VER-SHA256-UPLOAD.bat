@echo off
setlocal
cd /d "%~dp0"
echo.
echo SHA-256 da chave de UPLOAD local:
echo.
if not exist callmed-upload.jks (
  echo callmed-upload.jks nao encontrado.
  pause
  exit /b 1
)
keytool -list -v -keystore callmed-upload.jks -alias callmed-upload | findstr /I "SHA256 SHA-256"
echo.
echo IMPORTANTE: para o TWA instalado pela Google Play, use no Render a SHA-256 da

echo "App signing key certificate" do Play Console, e NAO esta chave de upload.
pause
