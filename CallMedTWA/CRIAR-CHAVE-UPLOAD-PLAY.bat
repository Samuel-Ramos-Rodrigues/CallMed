@echo off
setlocal
cd /d "%~dp0"
echo.
echo ================================================
echo CallMed - Criar chave de UPLOAD da Google Play
echo ================================================
echo.
echo A chave sera criada como: callmed-upload.jks
echo O keytool pedira senha e dados do certificado.
echo GUARDE A SENHA EM LOCAL SEGURO.
echo.
if exist callmed-upload.jks (
  echo ERRO: callmed-upload.jks ja existe. Nao vou sobrescrever sua chave.
  pause
  exit /b 1
)
keytool -genkeypair -v -keystore callmed-upload.jks -alias callmed-upload -keyalg RSA -keysize 2048 -validity 10000
if errorlevel 1 (
  echo.
  echo Falha ao criar a chave. Confirme se o JDK 17 esta instalado e keytool esta no PATH.
  pause
  exit /b 1
)
if not exist keystore.properties (
  copy /Y keystore.properties.example keystore.properties >nul
  echo.
  echo Foi criado keystore.properties.
  echo Abra o arquivo e substitua SUA_SENHA_FORTE pela senha usada acima.
)
echo.
echo Chave criada com sucesso.
echo NAO envie callmed-upload.jks nem keystore.properties ao GitHub.
pause
