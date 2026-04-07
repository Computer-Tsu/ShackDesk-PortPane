# Privacy Policy

**Last updated:** 2026-03-23

## What PortPane Collects

### Telemetry (opt-in, disabled by default)

If you choose to enable anonymous telemetry in Settings, PortPane may send:

- Application version
- Windows version string (e.g. "Windows 10.0.19045")
- Event names (e.g. "startup", "refresh", "crash")
- Anonymous session identifiers (no user identity)

**PortPane never collects:**

- Your callsign or name
- Your IP address (beyond what any HTTP request inherently exposes)
- File paths or directory names
- Device serial numbers or MAC addresses
- Any personally identifiable information

### Crash Reports

If crash reporting is enabled (part of telemetry opt-in), PortPane sends:

- Exception type and message
- Application version and OS version

Stack traces are intentionally excluded to avoid leaking file paths.

## Data Storage

Collected telemetry is transmitted to `https://telemetry.shackdesk.com/report` and stored for up to 90 days for aggregate analytics. No individual records are retained longer than 90 days.

## Update Checks

On startup, PortPane checks a channel-specific URL for available updates (e.g. `https://shackdesk.com/portpane/update/stable.json`). This is a standard HTTPS GET request. No user data is sent.

## Log Files

PortPane writes log files to `%LOCALAPPDATA%\ShackDesk\PortPane\Logs\`. These files are stored **locally only** and are never transmitted anywhere. Logs older than 7 days are automatically deleted.

## Contact

For privacy questions: [shackdesk.com/privacy](https://shackdesk.com/privacy)
