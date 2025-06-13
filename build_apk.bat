@echo off
echo 🔨 Building VR Letter Trainer APK...

REM Set Unity path (adjust if needed)
set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\2022.3.42f1\Editor\Unity.exe"

REM Check if Unity exists
if not exist %UNITY_PATH% (
    echo ❌ Unity not found at %UNITY_PATH%
    echo Please update the UNITY_PATH in this script
    pause
    exit /b 1
)

REM Build the project
echo 🏗️ Building Android APK...
%UNITY_PATH% -quit -batchmode -projectPath "%~dp0" -buildTarget Android -executeMethod BuildScript.BuildAPK

if %ERRORLEVEL% == 0 (
    echo ✅ Build completed successfully!
    echo 📱 APK location: %~dp0vrletter.apk
) else (
    echo ❌ Build failed with error code %ERRORLEVEL%
)

pause
