@echo off
setlocal
cd /d "%~dp0mobile\android" || goto :erro
set "JAVA_CMD=java.exe"
if defined JAVA_HOME if exist "%JAVA_HOME%\bin\java.exe" set "JAVA_CMD=%JAVA_HOME%\bin\java.exe"
if exist "%ProgramFiles%\Android\Android Studio\jbr\bin\java.exe" set "JAVA_CMD=%ProgramFiles%\Android\Android Studio\jbr\bin\java.exe"
"%JAVA_CMD%" AssinaturaLocal.java
if errorlevel 1 goto :erro
pause
exit /b 0
:erro
echo A assinatura nao foi concluida. Confira a mensagem acima.
pause
exit /b 1
