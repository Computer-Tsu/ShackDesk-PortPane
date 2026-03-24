# Contributing to PortPane

Thank you for your interest in contributing to PortPane by ShackDesk.

## Ways to Contribute

| Type | How |
|------|-----|
| Bug report | Open a [Bug Report issue](.github/ISSUE_TEMPLATE/bug_report.md) |
| Feature request | Open a [Feature Request issue](.github/ISSUE_TEMPLATE/feature_request.md) |
| USB device addition | Open a [USB Device issue](.github/ISSUE_TEMPLATE/usb_device_addition.md) or submit a PR editing `data/usb_devices.json` |
| Translation | See [TRANSLATING.md](TRANSLATING.md) and open a [Translation issue](.github/ISSUE_TEMPLATE/translation.md) |
| Code | Fork, branch, and submit a pull request (see below) |

## CLA

All contributors must agree to the [Contributor License Agreement](CLA.md) before a pull request can be merged. By submitting a PR, you agree to its terms.

## Code Standards

- **MVVM strictly enforced.** No business logic in code-behind files (`.xaml.cs`). All logic lives in ViewModels and Services.
- **No hardcoded strings.** All branding values reference `BrandingInfo` constants.
- **No admin rights.** The app must run as standard user (`asInvoker`).
- **Logging via Serilog only.** No `Console.WriteLine`, `Debug.WriteLine`, or `Trace`.
- **C# 12 / .NET 8** target only. No `.NET Framework` or `.NET Standard` targets.

## Pull Request Process

1. Fork the repository
2. Create a branch: `feature/my-feature` or `fix/my-bug`
3. Make your changes
4. Run tests: `dotnet test`
5. Submit a pull request — fill out the PR template fully
6. A maintainer will review within 7 days

## Questions?

Post in [GitHub Discussions](https://github.com/Computer-Tsu/shackdesk-portpane/discussions).
