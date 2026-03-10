@echo off
REM ============================================
REM  ClaudeCodeClicker - Research Tool Setup
REM  Creates Python venv and installs dependencies
REM ============================================

setlocal

set VENV_DIR=%~dp0venv
set REQUIREMENTS=%~dp0requirements.txt

echo === ClaudeCodeClicker Setup ===
echo.

REM Check Python is available
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found in PATH. Please install Python 3.10+.
    pause
    exit /b 1
)

echo Found Python:
python --version
echo.

REM Create venv if it doesn't exist
if not exist "%VENV_DIR%\Scripts\activate.bat" (
    echo Creating virtual environment in %VENV_DIR% ...
    python -m venv "%VENV_DIR%"
    if errorlevel 1 (
        echo ERROR: Failed to create virtual environment.
        pause
        exit /b 1
    )
    echo Virtual environment created.
) else (
    echo Virtual environment already exists at %VENV_DIR%
)
echo.

REM Activate and install
echo Installing dependencies...
call "%VENV_DIR%\Scripts\activate.bat"
pip install --upgrade pip >nul 2>&1
pip install -r "%REQUIREMENTS%"
if errorlevel 1 (
    echo ERROR: Failed to install dependencies.
    pause
    exit /b 1
)

echo.
echo === Setup Complete ===
echo.
echo Usage:
echo   1. Activate:  venv\Scripts\activate
echo   2. Run:       python research\research.py --help
echo.
pause
