using System.Text;
using Serilog.Sinks.File;

namespace PortPane.Logging;

/// <summary>
/// Serilog FileLifecycleHooks implementation that writes a structured,
/// human-readable header block at the top of each newly created log file.
/// Registered in App.xaml.cs via the .WriteTo.File(hooks:) parameter.
/// </summary>
public sealed class LogFileHeaderHooks : FileLifecycleHooks
{
    public override Stream OnFileOpened(Stream underlyingStream, Encoding encoding)
    {
        using var writer = new StreamWriter(underlyingStream, encoding, bufferSize: 4096, leaveOpen: true);
        writer.WriteLine(BuildHeader());
        writer.Flush();
        return underlyingStream;
    }

    private static string BuildHeader()
    {
        string line = new string('=', 78);
        string thin = new string('-', 78);
        string now  = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";

        return $"""
{line}
  PortPane by ShackDesk — Application Log
  Project  : https://github.com/Computer-Tsu/shackdesk-portpane
  Suite    : ShackDesk | App: PortPane | Version: {BrandingInfo.Version}
  Author   : {BrandingInfo.AuthorName} ({BrandingInfo.AuthorCallsign}) — {BrandingInfo.AuthorCompany}
  Support  : {BrandingInfo.SupportURL}
  Log opened: {now}
{line}

  PURPOSE
  This log records PortPane runtime events for troubleshooting and support.
  It is stored locally on your machine and is never uploaded automatically.
  You control whether usage data is shared — see the Privacy Statement below.

{thin}
  ROTATION & RETENTION POLICY
{thin}
  - A new log file is created each day (daily rolling).
  - Log files are kept for 7 days; older files are deleted automatically.
  - File naming: portpane-YYYYMMDD.log
  - Portable mode logs: <exe folder>\PortPane-Data\logs\
  - Standard mode logs: %APPDATA%\ShackDesk\PortPane\logs\

{thin}
  LOG LEVEL DEFINITIONS
{thin}
  DBG  Debug    — detailed diagnostic information (device detection, DI setup)
  INF  Info     — normal application events (start, stop, profile switch)
  WRN  Warning  — recoverable problems (settings unreadable, device not in DB)
  ERR  Error    — failures that affect a feature but the app continues running
  FTL  Fatal    — unhandled exceptions that caused the app to close

{thin}
  LOG LINE FORMAT
{thin}
  YYYY-MM-DD HH:MM:SS.mmm [LVL] Message {{structured properties}}
  Example:
  2026-03-23 14:07:42.318 [INF] Audio profile switched {{"From": "PC", "To": "Radio"}}

  Fields:
    YYYY-MM-DD HH:MM:SS.mmm — Timestamp in UTC (see Timestamp Format below)
    [LVL]                   — Log level abbreviation (DBG/INF/WRN/ERR/FTL)
    Message                 — Human-readable event description
    {{property: value}}      — Structured key-value pairs appended inline

{thin}
  SOURCE COMPONENT REFERENCE
{thin}
  AudioService      — USB/built-in audio device enumeration, profile switching
  ComPortService    — COM port enumeration via WMI and registry, baud heuristics
  HotplugService    — USB device connect/disconnect detection (WMI event watcher)
  UpdateService     — Velopack update check (background, rate-limited 24h)
  TelemetryService  — Optional anonymous usage reporting (opt-in, queue-based)
  LicenseService    — Offline license key validation (RSA signature check)
  SettingsService   — JSON settings file load/save, portable mode detection
  MainViewModel     — Main window coordination, chrome/scale/always-on-top state
  App               — Application startup, DI container, global exception handler

{thin}
  ABBREVIATIONS
{thin}
  VID    USB Vendor ID  (4 hex chars, e.g. 10C4 = Silicon Labs)
  PID    USB Product ID (4 hex chars, e.g. EA60 = CP2102)
  CODEC  USB Audio CODEC chip (e.g. C-Media CM108, CM119)
  CAT    Computer Aided Transceiver — rig control over serial port
  PTT    Push-To-Transmit — signal that keys the radio transmitter
  WMI    Windows Management Instrumentation — device enumeration API
  DI     Dependency Injection — service container pattern used throughout

{thin}
  ENCODED VALUE REFERENCE
{thin}
  Boolean      : logged as true/false in structured properties
  VID / PID    : logged as 4-character uppercase hex (e.g. VID=10C4 PID=EA60)
  Base64        : license key content is never logged in full (truncated to prefix)
  Scale factor  : logged as decimal (e.g. 1.35 = 135% zoom)
  Profile       : "PC" or "Radio" — the two audio routing modes

{thin}
  TIMESTAMP FORMAT
{thin}
  All timestamps are UTC. Format: YYYY-MM-DD HH:MM:SS.mmm
  Example: 2026-03-23 14:07:42.318
  To convert to local time, add your UTC offset (e.g. UTC-5 = subtract 5 hours).

{thin}
  PRIVACY STATEMENT
{thin}
  What IS in this log:
    - Device names, VID/PID values, COM port assignments
    - Audio device friendly names
    - App startup/shutdown events and version
    - Settings changes (field names and new values, no passwords)
    - Errors and stack traces (for troubleshooting only)

  What is NOT in this log:
    - Your callsign, name, or any personally identifying information
    - Radio frequencies, modes, or transmission content
    - License key content (only tier and expiry are logged)
    - Any data from your radio or digital mode software

  This file is stored only on your computer. It is not uploaded automatically.
  If you choose to submit it for support, please review it first.

{thin}
  TROUBLESHOOTING GUIDE
{thin}
  Device not recognized (not in the known-device list):
    - Search this log for "VID=" to find the VID:PID values.
    - Open a USB Device Database Addition issue on GitHub with those values.
    - The app still works; baud rate and radio classification use heuristics.

  App crash (FTL entry followed by process exit):
    - The last FTL line and stack trace identify the cause.
    - Attach the full log file when submitting a bug report.
    - Include your Windows version and PortPane version from the line above.

  Profile switching failure (audio device not changing):
    - Search for "ERR" near "AudioService" or "profile".
    - Check that no other app has exclusive control of the audio device.
    - Try running PortPane again after closing your digital mode software.

  Update check failure (WRN from UpdateService):
    - This is non-critical; the app works fully without an internet connection.
    - If you want to update manually, check the GitHub Releases page.

  Settings file unreadable (WRN from SettingsService):
    - The app resets to defaults and saves a fresh settings file.
    - If this happens repeatedly, delete settings.json and restart.

{thin}
  SUPPORT SUBMISSION INSTRUCTIONS
{thin}
  To report a bug or request help:
  1. Go to: https://github.com/Computer-Tsu/shackdesk-portpane/issues
  2. Click "New issue" and choose "Bug Report".
  3. Fill out the template, including:
       - PortPane version (shown in About dialog and first line of this log)
       - Windows version (Settings > System > About)
       - Your USB device name (brand/model)
       - Steps to reproduce the problem
  4. Attach this log file. Review it before attaching — see Privacy above.
  5. A maintainer will respond within 7 days.

  For sensitive issues (security vulnerabilities), see SECURITY.md instead.
  Do NOT post license keys or personal information in public issues.

{line}
  Log entries begin below this line.
{line}

""";
    }
}
