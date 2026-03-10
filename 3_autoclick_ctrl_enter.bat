@echo off
REM Auto-click the "Ctrl Enter" button whenever it appears (Ctrl+C to stop)
call "%~dp0venv\Scripts\activate.bat"
python "%~dp0research\research.py" monitor --pattern "Ctrl Enter" --auto-click --interval 1.0
