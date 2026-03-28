<!--
  ════════════════════════════════════════════════════════════════════════════
  PortPane by ShackDesk — Changelog
  Project  : https://github.com/Computer-Tsu/shackdesk-portpane
  Author   : Mark McDow (N4TEK) — My Computer Guru LLC
  License  : GPL v3 / Commercial (see LICENSE-GPL.md, LICENSE-COMMERCIAL.md)
  ════════════════════════════════════════════════════════════════════════════

  FORMAT
  This file follows the Keep a Changelog format: https://keepachangelog.com
  Newest version is listed first. Unreleased changes go in [Unreleased].

  SECTION LABELS
  Added       — new features added in this release
  Changed     — changes to existing functionality
  Fixed       — bug fixes
  Security    — security-related changes or vulnerability patches
  Deprecated  — features that will be removed in a future release
  Removed     — features removed in this release

  VERSION NUMBER FORMAT
  Follows Semantic Versioning: https://semver.org
  MAJOR.MINOR.PATCH  e.g. 1.2.3
  -beta suffix       — public preview; may have known issues, API may change
  -alpha suffix      — internal/early testing; not recommended for general use
  ════════════════════════════════════════════════════════════════════════════
-->

# Changelog

All notable changes to PortPane are documented here.
Format follows [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).
Versioning follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Fixed
- CI build failure (NETSDK1135): changed `net8.0-windows` to `net8.0-windows10.0.17763.0`
  in both `.csproj` files so `TargetPlatformVersion` is explicit; resolves conflict with
  .NET SDK 10 pre-installed on `windows-latest` runners
- Added `global.json` to pin SDK selection to 8.x (`rollForward: latestFeature`),
  preventing SDK 10 from becoming the ambient default for this repo on future runner images

### Added
- GitHub Actions: `codeql.yml` — weekly C# security analysis via CodeQL
- GitHub Actions: `dependency-review.yml` — NuGet vulnerability check on PRs (moderate+)
- GitHub Actions: `json-validate.yml` — validates `data/usb_devices.json` structure and required fields
- GitHub Actions: `format-check.yml` — enforces `dotnet format` on PRs
- GitHub Actions: `markdown-lint.yml` — markdownlint on all `.md` files
- `.github/dependabot.yml` — automated NuGet and Actions dependency updates (weekly, Monday)
- `.markdownlint.json` — markdownlint config (MD013/MD033/MD041 disabled)
- `.editorconfig` — code formatting rules enforced by CI; covers C#, XAML, JSON, YAML, Markdown
- `global.json` — SDK version pinning (8.x, latestFeature rollForward)
- `keys/portpane-public.pem` — placeholder for RSA public key with full attribution header
- `src/PortPane/Logging/LogFileHeaderHooks.cs` — Serilog `FileLifecycleHooks` that writes a
  structured ~100-line header to each new daily log file (purpose, rotation policy, level
  definitions, line format, component reference, abbreviations, privacy statement,
  troubleshooting guide, support instructions)
- Localization placeholders: `Strings.es.resx`, `Strings.fr.resx`, `Strings.ja.resx`
  (Spanish, French, Japanese) with contributor-friendly headers

### Changed
- `build.yml`: added 24-line comment header documenting triggers, secrets, outputs, and
  manual trigger instructions (workflow_dispatch was already present)
- `data/usb_devices.json` + `src/PortPane/Data/usb_devices.json`: added 64 `_comment_N`
  header keys — attribution, purpose, JSON warning, complete example entry, full field
  reference, and 8-step Device Manager VID/PID guide for non-technical contributors
- `UsbDeviceDatabase.cs`: added comment confirming System.Text.Json silently skips
  `_comment_N` keys; no behavioral change
- `App.xaml.cs`: wired `LogFileHeaderHooks` into Serilog `WriteTo.File()` via `hooks:`
  parameter; added `using PortPane.Logging`
- `SettingsService.cs`: `Save()` now calls `SerializeWithHeader()` which prepends 42
  `_comment_N` keys documenting every settings field, valid values, and a manual-edit warning
- `Strings.resx`: added full contributor XML comment header
- `Strings.de.resx`: added full header; completed previously missing Settings and About strings
- `PortPane.csproj`: added `Strings.es.resx`, `Strings.fr.resx`, `Strings.ja.resx` as
  `EmbeddedResource`; changed TFM to `net8.0-windows10.0.17763.0`
- `PortPane.Tests.csproj`: changed TFM to `net8.0-windows10.0.17763.0`
- `TRANSLATING.md`: rewrote — any language welcome (not limited to a predefined list),
  check open PRs before starting, 28-entry IETF tag reference table
- `CONTRIBUTING.md`: added welcoming opening paragraph for non-programmer contributors
  (device IDs and translations require no coding experience)
- `CLA.md`: added plain-English summary before legal text
- `CHANGELOG.md`: added XML comment header explaining format, section labels, version
  number format, and `-beta` suffix meaning
- `.gitignore`: added explanatory section comments for each exclusion group; added
  secrets/keys section with strong warning about private key handling
- Issue template `usb_device_addition.md`: full 8-step Device Manager walkthrough,
  structured fields table, compatible software checklist, contribution checklist
- Issue template `translation.md`: language/IETF code fields, completeness %, tools
  used field, CLA acknowledgment checkbox
- Issue template `bug_report.md`: table format for environment info, correct log
  file paths for both portable and standard modes, privacy note before attachment prompt
- Issue template `feature_request.md`: digital mode software checklist, who-benefits
  checkboxes, structured problem/solution/alternatives sections

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
