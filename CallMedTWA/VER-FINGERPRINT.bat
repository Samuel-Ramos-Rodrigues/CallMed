@echo off
setlocal
set /p KEYSTORE=Informe o caminho do keystore: 
set /p ALIAS=Informe o alias: 
keytool -list -v -keystore "%KEYSTORE%" -alias "%ALIAS%" | findstr /I "SHA256 SHA-256"
echo.
echo Se este keystore for a CHAVE DE UPLOAD, esta SHA nao e a assinatura final usada pela Play.
echo Para o assetlinks do TWA publicado, use a SHA-256 do "App signing key certificate" no Play Console.
pause
