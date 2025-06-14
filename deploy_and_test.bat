@echo off
setlocal enabledelayedexpansion

echo ===========================================
echo VR Letter Trainer - Build and Deploy
echo ===========================================
echo.

set APK_NAME=vrletter.apk
set PACKAGE_NAME=com.DefaultCompany.Working_VR
set ACTIVITY_NAME=com.unity3d.player.UnityPlayerActivity

echo [1/5] Building APK...
if exist "%APK_NAME%" (
    echo Found existing APK: %APK_NAME%
) else (
    echo No APK found. Please build the project first.
    echo You can use Unity Build Settings or run the build script.
    pause
    exit /b 1
)

echo [2/5] Checking ADB connection...
adb devices
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: ADB not found or no devices connected
    echo Please ensure:
    echo 1. Android SDK is installed
    echo 2. ADB is in your PATH
    echo 3. Android device is connected with USB debugging enabled
    pause
    exit /b 1
)

echo [3/5] Installing APK...
adb install -r "%APK_NAME%"
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Failed to install APK
    pause
    exit /b 1
)

echo [4/5] Granting permissions...
echo Granting microphone permission...
adb shell pm grant %PACKAGE_NAME% android.permission.RECORD_AUDIO
adb shell pm grant %PACKAGE_NAME% android.permission.WRITE_EXTERNAL_STORAGE
adb shell pm grant %PACKAGE_NAME% android.permission.READ_EXTERNAL_STORAGE

echo [5/5] Starting application...
adb shell am start -n %PACKAGE_NAME%/%ACTIVITY_NAME%

echo.
echo ===============================================
echo DEPLOYMENT COMPLETE!
echo ===============================================
echo.
echo The app should now be running on your device.
echo.
echo VOICE RECOGNITION DEBUG:
echo - Look for microphone permission dialog
echo - Check logcat for voice recognition status
echo - Use keyboard shortcuts for testing:
echo   * V key = Show voice status
echo   * R key = Restart voice recognition  
echo   * T key = Test with current letter
echo.
echo To view logs:
echo adb logcat -s VoiceBridge AndroidVoiceInput Unity
echo.
echo To clear logs and start fresh:
echo adb logcat -c
echo.

choice /C YN /M "Do you want to start logcat monitoring now?"
if errorlevel 2 goto :end
if errorlevel 1 goto :logcat

:logcat
echo Starting logcat monitoring...
echo Press Ctrl+C to stop monitoring
echo.
adb logcat -s VoiceBridge:* Unity:* *VoiceInput*:*

:end
pause
