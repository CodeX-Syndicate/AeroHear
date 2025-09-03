@echo off
setlocal

echo.
echo 🧪 AeroHear Virtual Audio Device Test Suite
echo ============================================

REM Check if we're on Windows
if not "%OS%"=="Windows_NT" (
    echo ❌ This test suite requires Windows
    pause
    exit /b 1
)

REM Build the project first
echo 🔧 Building AeroHear...
dotnet build -c Debug

if errorlevel 1 (
    echo ❌ Build failed
    pause
    exit /b 1
)

echo ✅ Build successful

REM Run tests
echo.
echo 🧪 Running Virtual Audio Device Tests...
echo.

dotnet run --project . --configuration Debug TestRunner.cs

if errorlevel 1 (
    echo ❌ Test execution failed
    pause
    exit /b 1
)

echo.
echo ✅ Test execution completed
pause