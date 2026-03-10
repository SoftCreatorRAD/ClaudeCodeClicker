"""
ClaudeCodeClicker Research Tool
===============================
Diagnostic script for exploring the UI Automation tree of the
Claude Code desktop application (Electron/Chromium).

Subcommands:
    find      - Find the Claude Code window
    tree      - Dump the UI Automation element tree
    buttons   - Find and list all buttons
    click     - Click a button by name substring
    monitor   - Continuous polling for button appearance

Usage:
    python research.py find
    python research.py tree --depth 5
    python research.py buttons --pattern Allow --pattern Deny
    python research.py click "Allow"
    python research.py monitor --pattern Allow --interval 1.0
"""

import argparse
import sys
import time
from datetime import datetime
from typing import Optional, List, Dict, Any

try:
    from pywinauto import Desktop
    from pywinauto.controls.uiawrapper import UIAWrapper
except ImportError:
    print("ERROR: pywinauto not installed. Run: pip install pywinauto>=0.6.9")
    sys.exit(1)


# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

DEFAULT_TITLE_PATTERN = "Claude"
DEFAULT_MAX_DEPTH = 8
MONITOR_POLL_INTERVAL = 2.0


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def safe_prop(element, prop_name: str, default: str = "<error>") -> Any:
    """Safely access an element_info property."""
    try:
        return getattr(element.element_info, prop_name)
    except Exception:
        return default


def safe_text(wrapper, default: str = "") -> str:
    """Safely get window_text from a wrapper."""
    try:
        return wrapper.window_text() or ""
    except Exception:
        return default


def timestamp() -> str:
    return datetime.now().strftime("%H:%M:%S")


# ---------------------------------------------------------------------------
# Window Discovery
# ---------------------------------------------------------------------------

def find_windows(title_pattern: str) -> List[Dict[str, Any]]:
    """
    Find all top-level windows whose title contains the given substring.
    Returns list of dicts with window info.
    """
    results = []
    pattern_lower = title_pattern.lower()

    try:
        desktop = Desktop(backend="uia")
        windows = desktop.windows()
    except Exception as e:
        print(f"ERROR: Failed to enumerate windows: {e}")
        return results

    for win in windows:
        try:
            title = safe_text(win)
            if pattern_lower in title.lower():
                results.append({
                    "title": title,
                    "handle": win.handle,
                    "process_id": safe_prop(win, "process_id", 0),
                    "class_name": safe_prop(win, "class_name", ""),
                    "control_type": safe_prop(win, "control_type", ""),
                    "wrapper": win,
                })
        except Exception:
            continue

    return results


def find_claude_window(title_pattern: str) -> Optional[Dict[str, Any]]:
    """Find the first matching window."""
    results = find_windows(title_pattern)
    if results:
        return results[0]
    return None


def print_window_info(info: dict) -> None:
    """Pretty-print window info."""
    handle = info["handle"]
    handle_str = f"0x{handle:08X}" if handle else "0x00000000"
    print(f"=== Window Found ===")
    print(f"  Title:        {info['title']}")
    print(f"  Handle:       {handle_str}")
    print(f"  Process ID:   {info['process_id']}")
    print(f"  Class Name:   {info['class_name']}")
    print(f"  Control Type: {info['control_type']}")


def print_all_windows(windows: List[Dict[str, Any]]) -> None:
    """Print all matching windows."""
    print(f"=== Found {len(windows)} matching window(s) ===\n")
    for i, info in enumerate(windows):
        handle = info["handle"]
        handle_str = f"0x{handle:08X}" if handle else "0x00000000"
        print(f"  [{i}] \"{info['title']}\"")
        print(f"      Handle: {handle_str}  PID: {info['process_id']}  Class: {info['class_name']}")
        print()


# ---------------------------------------------------------------------------
# Tree Walking
# ---------------------------------------------------------------------------

def walk_tree(
    element,
    max_depth: int = DEFAULT_MAX_DEPTH,
    current_depth: int = 0,
    filter_control_type: Optional[str] = None,
    lines: Optional[List[str]] = None,
) -> List[str]:
    """
    Recursively walk the UI Automation tree from element downward.
    Returns list of formatted lines.
    """
    if lines is None:
        lines = []

    ctrl_type = safe_prop(element, "control_type", "?")
    name = safe_text(element) if hasattr(element, "window_text") else safe_prop(element, "name", "")
    auto_id = safe_prop(element, "automation_id", "")
    class_name = safe_prop(element, "class_name", "")

    try:
        rect = element.rectangle()
        rect_str = f"({rect.left}, {rect.top}, {rect.right}, {rect.bottom})"
    except Exception:
        rect_str = "(?)"

    indent = "  " * current_depth

    # Build display line
    parts = [f"{indent}{ctrl_type}"]
    if name:
        # Truncate long names for readability
        display_name = name if len(name) <= 80 else name[:77] + "..."
        parts.append(f'Name="{display_name}"')
    if auto_id:
        parts.append(f"AutoId={auto_id}")
    if class_name:
        parts.append(f"Class={class_name}")
    parts.append(f"Rect={rect_str}")

    line = " | ".join(parts)

    # Apply filter
    if filter_control_type is None:
        lines.append(line)
    elif ctrl_type and filter_control_type.lower() in ctrl_type.lower():
        lines.append(line)

    # Recurse into children
    if current_depth < max_depth:
        try:
            children = element.children()
            for child in children:
                walk_tree(
                    child,
                    max_depth=max_depth,
                    current_depth=current_depth + 1,
                    filter_control_type=filter_control_type,
                    lines=lines,
                )
        except Exception:
            pass

    return lines


# ---------------------------------------------------------------------------
# Button Discovery
# ---------------------------------------------------------------------------

def find_buttons(
    window_wrapper,
    name_patterns: Optional[List[str]] = None,
) -> List[Dict[str, Any]]:
    """
    Find all Button-type elements within the window.
    If name_patterns given, marks which patterns each button matches.
    """
    buttons = []

    try:
        descendants = window_wrapper.descendants(control_type="Button")
    except Exception as e:
        print(f"ERROR: Failed to enumerate buttons: {e}")
        return buttons

    for btn in descendants:
        name = safe_text(btn)
        auto_id = safe_prop(btn, "automation_id", "")

        try:
            rect = btn.rectangle()
            rect_dict = {
                "left": rect.left, "top": rect.top,
                "right": rect.right, "bottom": rect.bottom,
            }
        except Exception:
            rect_dict = None

        matches = []
        if name_patterns:
            name_lower = name.lower()
            for pat in name_patterns:
                if pat.lower() in name_lower:
                    matches.append(pat)

        buttons.append({
            "name": name,
            "automation_id": auto_id,
            "rectangle": rect_dict,
            "matches": matches,
            "wrapper": btn,
        })

    return buttons


def print_buttons(
    buttons: List[Dict[str, Any]],
    only_matched: bool = False,
) -> None:
    """Print button discovery results."""
    display = buttons
    if only_matched:
        display = [b for b in buttons if b["matches"]]

    print(f"=== Buttons Found: {len(display)} ===\n")

    for i, btn in enumerate(display):
        rect = btn["rectangle"]
        if rect:
            w = rect["right"] - rect["left"]
            h = rect["bottom"] - rect["top"]
            rect_str = f"({rect['left']}, {rect['top']}) - ({rect['right']}, {rect['bottom']})  [{w}x{h}]"
        else:
            rect_str = "(?)"

        print(f"  [{i+1}] Name: \"{btn['name']}\"")
        if btn["automation_id"]:
            print(f"       AutomationId: {btn['automation_id']}")
        print(f"       Rectangle: {rect_str}")
        if btn["matches"]:
            print(f"       ** MATCHES: {', '.join(btn['matches'])} **")
        print()


# ---------------------------------------------------------------------------
# Click Action
# ---------------------------------------------------------------------------

def click_button(
    window_wrapper,
    button_name: str,
    method: str = "invoke",
) -> bool:
    """
    Find a button by name substring and click it.

    Methods tried in order:
        invoke      - UIA Invoke pattern (best for Electron)
        click_input - Physical mouse click (needs window visible)
        click       - WM_LBUTTONDOWN/UP messages

    Returns True on success.
    """
    buttons = find_buttons(window_wrapper, name_patterns=[button_name])
    matched = [b for b in buttons if b["matches"]]

    if not matched:
        print(f"ERROR: No button found matching \"{button_name}\"")
        print(f"       Available buttons:")
        for b in buttons:
            print(f"         - \"{b['name']}\"")
        return False

    if len(matched) > 1:
        print(f"WARNING: Multiple buttons match \"{button_name}\", using first:")
        for b in matched:
            print(f"         - \"{b['name']}\"")
        print()

    target = matched[0]
    btn_wrapper = target["wrapper"]
    print(f"Target button: \"{target['name']}\"")

    # Try requested method, then fallbacks
    methods_order = {
        "invoke": ["invoke", "click_input", "click"],
        "click_input": ["click_input", "invoke", "click"],
        "click": ["click", "invoke", "click_input"],
    }

    for m in methods_order.get(method, ["invoke", "click_input", "click"]):
        try:
            if m == "invoke":
                print(f"  Trying invoke()... ", end="", flush=True)
                btn_wrapper.invoke()
                print("OK")
                return True
            elif m == "click_input":
                print(f"  Trying click_input()... ", end="", flush=True)
                btn_wrapper.click_input()
                print("OK")
                return True
            elif m == "click":
                print(f"  Trying click()... ", end="", flush=True)
                btn_wrapper.click()
                print("OK")
                return True
        except Exception as e:
            print(f"FAILED ({type(e).__name__}: {e})")
            continue

    print("ERROR: All click methods failed.")
    return False


# ---------------------------------------------------------------------------
# Monitor Mode
# ---------------------------------------------------------------------------

def monitor_buttons(
    title_pattern: str,
    watch_patterns: Optional[List[str]] = None,
    interval: float = MONITOR_POLL_INTERVAL,
    auto_click: bool = False,
) -> None:
    """
    Continuously poll the Claude Code window for buttons.
    Prints changes (buttons appearing/disappearing).
    If auto_click is True, clicks the first matching button.
    """
    print(f"=== Monitor Mode ===")
    print(f"  Title pattern: \"{title_pattern}\"")
    print(f"  Watch patterns: {watch_patterns or '(all buttons)'}")
    print(f"  Poll interval: {interval}s")
    print(f"  Auto-click: {auto_click}")
    print(f"  Press Ctrl+C to stop.\n")

    prev_button_names = set()
    window_info = None

    while True:
        # Find window if not found yet
        if window_info is None:
            window_info = find_claude_window(title_pattern)
            if window_info is None:
                print(f"[{timestamp()}] Window not found, retrying...")
                time.sleep(interval)
                continue
            print(f"[{timestamp()}] Window found: \"{window_info['title']}\" (PID {window_info['process_id']})")

        # Scan buttons
        try:
            buttons = find_buttons(window_info["wrapper"], name_patterns=watch_patterns)
        except Exception as e:
            print(f"[{timestamp()}] Scan error: {e}. Re-finding window...")
            window_info = None
            time.sleep(interval)
            continue

        # Get matched buttons (or all if no patterns)
        if watch_patterns:
            relevant = [b for b in buttons if b["matches"]]
        else:
            relevant = buttons

        current_names = {b["name"] for b in relevant}

        # Detect changes
        appeared = current_names - prev_button_names
        disappeared = prev_button_names - current_names

        if appeared or disappeared:
            for name in appeared:
                print(f"[{timestamp()}]   >> APPEARED: \"{name}\"")
            for name in disappeared:
                print(f"[{timestamp()}]   << GONE: \"{name}\"")

            # Auto-click first match
            if auto_click and appeared:
                first_new = next(b for b in relevant if b["name"] in appeared)
                print(f"[{timestamp()}]   ** AUTO-CLICK: \"{first_new['name']}\"")
                try:
                    first_new["wrapper"].invoke()
                    print(f"[{timestamp()}]   ** invoke() OK")
                except Exception as e:
                    print(f"[{timestamp()}]   ** invoke() FAILED: {e}")
                    try:
                        first_new["wrapper"].click_input()
                        print(f"[{timestamp()}]   ** click_input() OK")
                    except Exception as e2:
                        print(f"[{timestamp()}]   ** click_input() FAILED: {e2}")
        else:
            # Periodic status (every 10 intervals to avoid spam)
            pass

        prev_button_names = current_names
        time.sleep(interval)


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="research.py",
        description="Claude Code UI Automation Research Tool",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python research.py find
  python research.py find --title "Claude"
  python research.py tree --depth 5
  python research.py tree --depth 3 --type Button
  python research.py buttons
  python research.py buttons --pattern Allow --pattern Deny
  python research.py click "Allow"
  python research.py click Allow --method invoke
  python research.py monitor --pattern Allow --pattern Deny --interval 1.0
  python research.py monitor --pattern Allow --auto-click
        """,
    )

    sub = parser.add_subparsers(dest="command", required=True)

    # --- find ---
    p_find = sub.add_parser("find", help="Find the Claude Code window")
    p_find.add_argument("--title", default=DEFAULT_TITLE_PATTERN,
                        help=f"Window title substring (default: '{DEFAULT_TITLE_PATTERN}')")

    # --- tree ---
    p_tree = sub.add_parser("tree", help="Dump UI Automation element tree")
    p_tree.add_argument("--title", default=DEFAULT_TITLE_PATTERN,
                        help=f"Window title substring (default: '{DEFAULT_TITLE_PATTERN}')")
    p_tree.add_argument("--depth", type=int, default=DEFAULT_MAX_DEPTH,
                        help=f"Max tree depth (default: {DEFAULT_MAX_DEPTH})")
    p_tree.add_argument("--type", dest="control_type", default=None,
                        help="Filter by ControlType (e.g., Button, Text, Edit)")

    # --- buttons ---
    p_btn = sub.add_parser("buttons", help="Find and list all buttons")
    p_btn.add_argument("--title", default=DEFAULT_TITLE_PATTERN,
                       help=f"Window title substring (default: '{DEFAULT_TITLE_PATTERN}')")
    p_btn.add_argument("--pattern", action="append", default=None,
                       help="Button name substring to match (repeatable)")

    # --- click ---
    p_click = sub.add_parser("click", help="Click a button by name substring")
    p_click.add_argument("button_name", help="Name or substring of the button to click")
    p_click.add_argument("--title", default=DEFAULT_TITLE_PATTERN,
                         help=f"Window title substring (default: '{DEFAULT_TITLE_PATTERN}')")
    p_click.add_argument("--method", choices=["invoke", "click_input", "click"],
                         default="invoke", help="Click method (default: invoke)")

    # --- monitor ---
    p_mon = sub.add_parser("monitor", help="Continuously watch for buttons (Ctrl+C to stop)")
    p_mon.add_argument("--title", default=DEFAULT_TITLE_PATTERN,
                       help=f"Window title substring (default: '{DEFAULT_TITLE_PATTERN}')")
    p_mon.add_argument("--pattern", action="append", default=None,
                       help="Button name substring to watch (repeatable)")
    p_mon.add_argument("--interval", type=float, default=MONITOR_POLL_INTERVAL,
                       help=f"Poll interval in seconds (default: {MONITOR_POLL_INTERVAL})")
    p_mon.add_argument("--auto-click", action="store_true", default=False,
                       help="Auto-click first matching button (use with caution)")

    return parser


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> int:
    parser = build_parser()
    args = parser.parse_args()

    if args.command == "find":
        windows = find_windows(args.title)
        if not windows:
            print(f"ERROR: No window found matching \"{args.title}\"")
            return 1
        print_all_windows(windows)
        return 0

    elif args.command == "tree":
        winfo = find_claude_window(args.title)
        if not winfo:
            print(f"ERROR: No window found matching \"{args.title}\"")
            return 1
        print_window_info(winfo)
        print()
        print(f"=== UI Automation Tree (max depth: {args.depth}) ===")
        print("Scanning... this may take 10-30 seconds for Electron apps.\n")
        lines = walk_tree(
            winfo["wrapper"],
            max_depth=args.depth,
            filter_control_type=args.control_type,
        )
        for line in lines:
            print(line)
        print(f"\n=== Total elements: {len(lines)} ===")

        if len(lines) < 5:
            print()
            print("NOTE: Very few elements found. The Electron app may need")
            print("      accessibility to be enabled. Try one of:")
            print("        set ELECTRON_FORCE_RENDERER_ACCESSIBILITY=1")
            print("        --force-renderer-accessibility flag on app launch")
        return 0

    elif args.command == "buttons":
        winfo = find_claude_window(args.title)
        if not winfo:
            print(f"ERROR: No window found matching \"{args.title}\"")
            return 1
        print_window_info(winfo)
        print()
        buttons = find_buttons(winfo["wrapper"], name_patterns=args.pattern)
        print_buttons(buttons)

        if not buttons:
            print("NOTE: No buttons found. Possible reasons:")
            print("  1. No permission dialog is currently visible")
            print("  2. Electron accessibility not enabled")
            print("     Try: set ELECTRON_FORCE_RENDERER_ACCESSIBILITY=1")
        return 0

    elif args.command == "click":
        winfo = find_claude_window(args.title)
        if not winfo:
            print(f"ERROR: No window found matching \"{args.title}\"")
            return 1
        print_window_info(winfo)
        print()
        success = click_button(winfo["wrapper"], args.button_name, method=args.method)
        return 0 if success else 1

    elif args.command == "monitor":
        monitor_buttons(
            title_pattern=args.title,
            watch_patterns=args.pattern,
            interval=args.interval,
            auto_click=args.auto_click,
        )
        return 0

    return 1


if __name__ == "__main__":
    try:
        sys.exit(main())
    except KeyboardInterrupt:
        print("\nStopped.")
        sys.exit(0)
