# Code Signing Policy (SignPath Readiness)

## Scope

This policy defines how official PortPane release artifacts are produced and how
code signing will be integrated for trusted distribution.

## Source Code License

All source code in this repository is licensed under the MIT License
([LICENSE-MIT.md](LICENSE-MIT.md)).

## Official Builds vs Source Builds

- **Source builds**: Any binaries you build yourself from this repository are
  governed by MIT terms.
- **Official ShackDesk builds**: Binaries published by ShackDesk may include
  additional branding/support/licensing terms described in
  [LICENSE-COMMERCIAL.md](LICENSE-COMMERCIAL.md).

Commercial terms do not reduce or override MIT rights for source code.

## Trusted Build Intent

The CI workflow is structured to support a trusted signing flow:

1. Build unsigned release artifact in GitHub Actions.
2. Publish immutable workflow artifacts (exe + sha256).
3. Sign official release artifacts in a dedicated signing stage/service.
4. Publish signed artifacts and checksums to GitHub Releases.

## Current Status (as of 2026-04-08)

- CI currently builds and publishes **unsigned** artifacts.
- Signing is intentionally a placeholder in `.github/workflows/build.yml`.
- SignPath integration is planned but not yet enabled in this repository.

## Future SignPath Notes

When SignPath is enabled, release notes should identify:

- which artifacts were signed,
- the certificate subject used for signing,
- where verification guidance is documented.
