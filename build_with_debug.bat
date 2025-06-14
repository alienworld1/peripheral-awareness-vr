@echo off
echo =================================
echo VR Letter Trainer - Android Build
echo =================================
echo.

echo Building APK with voice recognition...
echo.

REM Check if Unity is in PATH
where unity >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Unity not found in PATH
    echo Please add Unity to your PATH or modify this script with Unity path
    echo Example: set UNITY_PATH="C:\Program Files\Unity\Hub\Editor\2022.3.X\Editor\Unity.exe"
    pause
    exit /b 1
)

REM Build the project
echo Starting Unity build...
unity -batchmode -quit -projectPath "%CD%" -buildTarget Android -executeMethod BuildScript.BuildAndroid

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ===================================
    echo BUILD SUCCESSFUL!
    echo ===================================
    echo.
    echo APK should be located at: %CD%\vrletter.apk
    echo.
    echo Debug Information:
    echo - Voice recognition is enabled
    echo - Microphone permissions will be requested on first run
    echo - Press V key to show voice status
    echo - Press R key to restart voice recognition
    echo - Press T key to test with current letter
    echo.
    echo Connect your Android device and install:
    echo adb install -r vrletter.apk
    echo.
) else (
    echo.
    echo ===================================
    echo BUILD FAILED!
    echo ===================================
    echo Please check Unity console for errors
)

echo.
pause
