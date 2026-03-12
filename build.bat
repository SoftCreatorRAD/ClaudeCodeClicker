@echo off
REM ============================================
REM  ClaudeCodeClicker - Build Script
REM  Compiles C# source to exe using csc.exe
REM ============================================

setlocal

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set SRC=%~dp0src\ClaudeCodeClicker.cs
set OUT_DIR=%~dp0bin
set OUT=%OUT_DIR%\ClaudeCodeClicker.exe

echo === ClaudeCodeClicker Build ===
echo.

REM Check compiler
if not exist "%CSC%" (
    echo ERROR: C# compiler not found at %CSC%
    pause
    exit /b 1
)

REM Create output directory
if not exist "%OUT_DIR%" mkdir "%OUT_DIR%"

echo Compiling...
echo   Source: %SRC%
echo   Output: %OUT%
echo.

"%CSC%" /target:winexe /out:"%OUT%" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "%SRC%"

if errorlevel 1 (
    echo.
    echo ERROR: Build failed.
    pause
    exit /b 1
)

echo.
echo === Build Successful ===
echo   Output: %OUT%
echo.
echo To run: bin\ClaudeCodeClicker.exe
echo.
pause
