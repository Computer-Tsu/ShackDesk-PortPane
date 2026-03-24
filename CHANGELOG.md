# Changelog

All notable changes to PortPane are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

## [0.5.0-beta] — 2026-03-23

### Added
- Complete project scaffold with full MVVM architecture
- .NET 8 built-in dependency injection container
- COM port enumeration via WMI (VID/PID extraction) + Registry fallback
  - Ghost/phantom port detection with tooltip explanation
  - Baud rate heuristics by device type (network gear, GPS, Arduino, FTDI, etc.)
  - PuTTY integration — auto-detects installation, launches pre-configured
- Audio device enumeration via NAudio Core Audio API
  - USB/External vs Built-in device classification
  - Radio interface identification by keyword and VID/PID
  - Default device display (prominent, high-contrast)
- PC / Radio audio profile switching via Windows Core Audio API (no admin rights)
- USB hotplug detection via WMI ManagementEventWatcher (background thread)
- Known USB device database (`data/usb_devices.json`) — 16 devices including:
  SignaLink, DigiRig, RIGblaster, IC-7300/IC-705, Yaesu SCU-17, FTDI, CP210x, CH340, Arduino
- Settings system with portable mode (portable.txt detection)
  - Settings schema v1 with migration framework
  - Window position and size persistence
  - Audio profile, baud rate, scale factor persistence
- Whole-layout scaling (0.85x – 2.25x) via WPF LayoutTransform + SizeToContent
- Hidden chrome behavior — window chrome revealed by click, auto-hides after 5s
  - Always-visible 8px drag strip with grip texture
  - Full menu: File / Edit / View / Help with keyboard shortcuts
- Dark theme with system High Contrast mode compatibility
- Single-instance enforcement via named Mutex
- Velopack auto-update (background, rate-limited to once per 24h, non-interrupting)
- GPL v3 / Commercial dual license with RSA offline license validation (key placeholder)
- Opt-in anonymous telemetry (off by default)
  - `Help → View Collected Data` viewer with Send/Clear/Close
  - Pending report queue (max 10, oldest discarded)
- First run dialog with telemetry opt-in and license notice
- About dialog with hidden easter egg (5 random animations on double-click)
- Global unhandled exception handler with plain-English user message
- Serilog structured logging — 7-day rolling daily files
  - Portable mode: logs beside exe; standard mode: `%APPDATA%\ShackDesk\PortPane\logs\`
- Per-monitor DPI v2 awareness via app.manifest
- Attribution.cs copyright fingerprint (UUID: f8a2c4e6-3b1d-4f9a-8e7c-5d2b0a6c1e4f)
- Inno Setup installer script
- GitHub Actions CI/CD — build, test, publish, SHA-256 hash, GitHub Release
- Unit tests — DeviceDetection, Settings, LicenseValidation (30+ test cases)
- GitHub community files: 4 issue templates, PR template, SECURITY.md
- Community documentation: CONTRIBUTING, TRANSLATING, CLA, PRIVACY, LEGAL

### Architecture
- MVVM strictly enforced — zero business logic in code-behind files
- All services registered as interfaces via .NET DI container
- All branding values reference BrandingInfo constants (never hardcoded)
- All network operations: background thread, 3s timeout, silent offline failure
- Offline operation is a primary use case — app is fully functional without internet

---

## Legend

- **Added** — new features
- **Changed** — changes to existing functionality
- **Fixed** — bug fixes
- **Security** — security-related changes
- **Deprecated** — features to be removed in a future release
- **Removed** — features removed in this release
