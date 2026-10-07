@echo off
cd /d "%~dp0"
echo ==============================================
echo  CallMed TWA - Gerando APK de teste
 echo ==============================================
call gradlew.bat assembleDebug
if errorlevel 1 goto erro
echo.
echo APK gerado em:
echo app\build\outputs\apk\debug\app-debug.apk
echo.
pause
exit /b 0
:erro
echo.
echo Falha ao gerar o APK. Abra esta pasta no Android Studio e use Build ^> Build APK(s).
pause
exit /b 1
