# Security Policy

## Supported Versions

| Version     | Supported |
|------------|-----------|
| 0.5.x-beta | Yes       |

## Reporting a Vulnerability

**Do not open a public issue for security vulnerabilities.**

Report privately via GitHub's
[Security Advisories](https://github.com/Computer-Tsu/shackdesk-portpane/security/advisories/new),
or contact the maintainer directly via their GitHub profile.

Please include:
- Description of the vulnerability
- Steps to reproduce
- Potential impact
- Suggested remediation (if any)

You will receive acknowledgement within 7 days.
Critical vulnerabilities will be patched and released on priority.

## Scope

PortPane runs as a standard user (`asInvoker`) with no elevated privileges.
It reads USB device state via WMI and the Windows Core Audio API.
It writes only to `%LOCALAPPDATA%\ShackDesk\PortPane\` and the system clipboard.

## Out of Scope

- Issues in third-party NuGet dependencies (report to those upstream projects)
- Issues requiring physical access to the machine
