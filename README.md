# ClaudeCodeClicker

A lightweight Windows system tray auto-clicker for Anthropic Claude Code permission prompts.

The application attaches to the existing Claude window, monitors its UI through Windows UI Automation, detects permission buttons that require `Ctrl Enter`, and automatically confirms them with a positive action.

## Purpose

Claude Code periodically displays confirmation dialogs that require user approval.  
This project automates that step by continuously monitoring the Claude window and clicking the confirmation button whenever it appears.

The current rule is intentionally simple:

- Find a button whose label contains `Ctrl Enter`
- Treat it as the positive confirmation action
- Click it automatically

This rule was validated against observed prompt combinations such as:

- `Deny Esc`
- `Allow once Ctrl Enter`

and

- `Deny Esc`
- `Always allow for session Ctrl Enter`
- `Allow once Enter`

In both cases, the button containing `Ctrl Enter` was the correct positive confirmation action.

## Project Structure

```text
ClaudeCodeClicker/
├── src/
│   └── ClaudeCodeClicker.cs     # Tray application entry point and logic
├── bin/
│   └── ClaudeCodeClicker.exe    # Built executable (from build.bat)
├── research/
│   └── research.py              # Python tooling for monitoring / auto-click experiments
├── requirements.txt             # Python dependencies for research tools
├── 1_setup_env.bat              # Create venv and install Python deps
├── 3_autoclick_ctrl_enter.bat   # Run research.py in monitor/auto-click mode
├── build.bat                    # Build script for the C# executable
└── README.md
```

## Requirements

- **OS**: Windows 10 or later
- **Tray app**: .NET Framework 4.x with `csc.exe` available at `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` (or adjust `build.bat`)
- **Research tools (optional)**: Python 3.10+ (used for `research/research.py`)

## Building the tray application

- **From source**:
  - Run `build.bat`
  - On success, the executable is written to `bin/ClaudeCodeClicker.exe`

## Running the tray application

- Double-click `bin/ClaudeCodeClicker.exe`
- A tray icon appears:
  - The app polls once per second for a Claude Code window with a button containing `Ctrl Enter`
  - When found, it clicks that button automatically and updates the tray tooltip with the latest status
- Tray controls:
  - **Pause/Resume** via the tray context menu
  - **Exit** via the tray context menu
  - **Double-click** the tray icon to see the last status and last click information as a balloon tooltip

## Python research / CLI auto-clicker

The `research` folder provides a Python implementation and tooling that mirrors the core behavior and is useful for experiments and debugging.

- **Initial setup**:
  - Run `1_setup_env.bat`
  - This creates a `venv/` and installs packages from `requirements.txt`
- **Continuous monitoring and auto-clicking**:
  - Run `3_autoclick_ctrl_enter.bat`
  - This activates the virtual environment and runs:
    - `python research\research.py monitor --pattern "Ctrl Enter" --auto-click --interval 1.0`
  - Use `Ctrl+C` in the console to stop it

For advanced usage, you can activate the virtual environment (`venv\Scripts\activate`) and run `python research\research.py --help` to inspect all available commands and options.