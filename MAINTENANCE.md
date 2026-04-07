# PortPane — Maintainer Reference

This document covers tasks that require maintainer access: key management, releases,
code signing, and other operational procedures. It is not intended for general contributors.

---

## Table of Contents

1. [RSA License Key Setup](#1-rsa-license-key-setup)
2. [Key Rotation](#2-key-rotation)
3. [Cutting a Release](#3-cutting-a-release)
4. [Velopack Auto-Update Setup](#4-velopack-auto-update-setup)
5. [Code Signing Setup](#5-code-signing-setup)
6. [Cross-Org PAT — ShackDesk-Site Dispatch](#6-cross-org-pat--shackdesk-site-dispatch)
7. [NuGet and Actions Dependency Updates](#7-nuget-and-actions-dependency-updates)

---

## 1. RSA License Key Setup

PortPane uses RSA-2048 / SHA-256 / PKCS1 to verify commercial license keys offline.
The private key signs license keys. The public key is embedded in the app to verify them.

### Generate a new key pair

Run this PowerShell script on a secure machine. Do not run it in a CI environment.

```powershell
# Generate RSA-2048 key pair in .NET XML format
$rsa = [System.Security.Cryptography.RSA]::Create(2048)

# Private key — contains all parameters — KEEP SECRET
$privateKeyXml = $rsa.ToXmlString($true)
$privateKeyXml | Out-File -FilePath "portpane-private.xml" -Encoding UTF8 -NoNewline

# Public key — modulus and exponent only — safe to embed in source
$publicKeyXml = $rsa.ToXmlString($false)
$publicKeyXml | Out-File -FilePath "portpane-public.xml" -Encoding UTF8 -NoNewline

$rsa.Dispose()

Write-Host "Done."
Write-Host "Private key -> portpane-private.xml  (store securely, then delete this file)"
Write-Host "Public key  -> portpane-public.xml   (embed in source, copy to keys/)"
```

> **IMPORTANT:** Delete `portpane-private.xml` from your local machine after completing
> the steps below. Never commit it. Never email it. Store it only in GitHub Secrets
> and a secure offline backup.

---

### Store the private key in GitHub Actions

1. Open the repository on GitHub.
2. Go to **Settings → Secrets and variables → Actions**.
3. Click **New repository secret**.
4. Name: `RSA_LICENSE_PRIVATE_KEY`
5. Value: paste the entire contents of `portpane-private.xml` (single-line XML is fine —
   `RSA.FromXmlString()` does not require line breaks or indentation).
6. Click **Add secret**.

The secret is used only by the license key generator tool — not by the app build itself.

---

### Embed the public key in the app

1. Open `src/PortPane/Services/LicenseService.cs`.
2. Find the `PublicKeyXml` constant (search for `<RSAKeyValue>`).
3. Replace the existing value with the contents of `portpane-public.xml`.
   Format it as a single concatenated string:

   ```csharp
   private const string PublicKeyXml =
       "<RSAKeyValue>" +
       "<Modulus>...your modulus here...</Modulus>" +
       "<Exponent>AQAB</Exponent>" +
       "</RSAKeyValue>";
   ```

4. Copy `portpane-public.xml` to `keys/portpane-public.pem` (update the file content,
   keeping the header comment). This is the reference copy checked into the repo.
5. Commit with a message like `Update RSA public key`.

---

### Signing payload format

The payload signed by the private key is a pipe-delimited string in this exact order:

```text
portpane|{type}|{licensee}|{email}|{issued}|{expires}
```

| Field      | Value                                              |
|------------|----------------------------------------------------|
| `portpane` | Literal string — always "portpane"                 |
| `type`     | `personal`, `club`, or `emcomm`                    |
| `licensee` | Full name of the licensee                          |
| `email`    | Email address of the licensee                      |
| `issued`   | ISO 8601 UTC string, e.g. `2026-03-27T00:00:00Z`   |
| `expires`  | ISO 8601 UTC string, or the literal string `never` |

Use empty string for any null/missing field. Values must match the JSON exactly.

**License key generator snippet (C#):**

```csharp
using var rsa = RSA.Create();
rsa.FromXmlString(privateKeyXml);           // from RSA_LICENSE_PRIVATE_KEY secret

string payload = string.Join("|",
    "portpane", type, licensee, email, issued, expires);

byte[] data   = Encoding.UTF8.GetBytes(payload);
byte[] sig    = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
string sigB64 = Convert.ToBase64String(sig);

var licenseJson = JsonSerializer.Serialize(new {
    app         = "portpane",
    type        = type,
    licensee    = licensee,
    email       = email,
    issued      = issued,
    expires     = expires,
    version_max = "1.x",
    signature   = sigB64
});

string licenseKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(licenseJson));
// licenseKey is what you deliver to the customer
```

---

## 2. Key Rotation

RSA keys do not expire on their own. Rotate the key pair if:

- The private key is ever suspected to be compromised.
- The GitHub repository or secrets are accessed by an unauthorized party.
- You choose a scheduled rotation policy (e.g. every 2 years).

**Rotation steps:**

1. Generate a new key pair following [Section 1](#1-rsa-license-key-setup).
2. Store the new private key in the `RSA_LICENSE_PRIVATE_KEY` GitHub secret
   (overwrite the existing secret).
3. Embed the new public key in `LicenseService.cs` and `keys/portpane-public.pem`.
4. Re-issue license keys for all current commercial licensees signed with the new key.
   Keys signed with the old key will no longer validate after the update is shipped.
5. Notify affected licensees before releasing the update so they can request new keys.
6. Commit and tag a new release.

> After rotation, any existing commercial license key becomes invalid. Plan accordingly.

---

## 3. Cutting a Release

### Files to update before tagging

| File | What to change |
| ------ | ---------------- |
| `src/PortPane/BrandingInfo.cs` | `Version` constant |
| `src/PortPane/PortPane.csproj` | `<Version>`, `<FileVersion>`, `<AssemblyVersion>` |
| `installer/PortPane.iss` | `#define AppVersion` |
| `CHANGELOG.md` | Move `[Unreleased]` items to a new `[x.y.z] — YYYY-MM-DD` section |

### Steps

```powershell
# 1. Make sure main is clean and all changes are committed
git status

# 2. Update the four files listed above, then commit
git add src/PortPane/BrandingInfo.cs src/PortPane/PortPane.csproj installer/PortPane.iss CHANGELOG.md
git commit -m "Release v0.6.0-beta"

# 3. Create and push a version tag — this triggers the full CI release pipeline
git tag v0.6.0-beta
git push origin main
git push origin v0.6.0-beta
```

### What the CI pipeline does on a version tag

1. **build-and-test** — builds Debug, runs xUnit tests
2. **publish** — builds self-contained `PortPane.exe`, computes SHA-256
3. **installer** — builds Inno Setup installer (if ISCC.exe is available on runner)
4. **release** — creates a GitHub Release with the exe, installer, and CHANGELOG notes

The `-beta` or `-alpha` suffix in the tag name automatically marks the GitHub Release
as a pre-release.

### Version number format

```text
MAJOR.MINOR.PATCH[-suffix]

Examples:
  0.5.0-beta    Pre-release beta
  0.5.1-beta    Beta patch (bug fixes only)
  1.0.0         First stable release
  1.1.0         Minor feature addition
  1.1.1         Patch / bug fix
```

---

## 4. Velopack Auto-Update Setup

The app checks `BrandingInfo.UpdateEndpoint` for updates at most once every 24 hours.

### Update manifest

Host a `latest.json` file at the `UpdateEndpoint` URL. Velopack's expected format:

```json
{
  "version": "0.6.0-beta",
  "url": "https://shackdesk.com/releases/PortPane-0.6.0-beta-win-x64.exe",
  "sha256": "abc123...",
  "releaseNotes": "See https://github.com/Computer-Tsu/shackdesk-portpane/releases"
}
```

Update this file each time a new release is published. The CI `publish` job outputs
the SHA-256 hash as a step output and artifact (`PortPane.exe.sha256`).

### Updating the endpoint URL

The endpoint is defined in `BrandingInfo.cs`:

```csharp
public const string UpdateEndpoint = "https://shackdesk.com/update/latest.json";
```

Change this before the first stable release if the hosting URL differs.

---

## 5. Code Signing Setup

Code signing is currently a placeholder in the CI pipeline. When you obtain an
EV or OV certificate:

### In `build.yml`

Uncomment and configure the signing step (currently lines ~117–127):

```yaml
- name: Sign executable
  run: >
    signtool.exe sign
      /fd sha256
      /tr http://timestamp.digicert.com
      /td sha256
      /f certificate.pfx
      /p ${{ secrets.SIGNING_PASSWORD }}
      ./publish/PortPane.exe
```

Add `SIGNING_PASSWORD` (the PFX password) as a GitHub Actions secret.

### In `installer/PortPane.iss`

Uncomment and configure the `SignTool` line:

```ini
SignTool=standard sign /fd sha256 /tr http://timestamp.digicert.com /td sha256 $f
```

### Obfuscation (ConfuserEx)

The `build.yml` also has a placeholder for ConfuserEx obfuscation (lines ~105–115).
To enable it, create `PortPane.crproj` targeting `./publish/PortPane.exe` and update
the build step to call `crconf.exe`.

---

## 6. Cross-Org PAT — ShackDesk-Site Dispatch

The `SITE_DISPATCH_PAT` GitHub Actions secret authorises the PortPane release job to trigger
the update JSON workflow in `Computer-Consultant/ShackDesk-Site` after each release.

**Token scope:**

- Resource owner: `Computer-Consultant`
- Repository: `ShackDesk-Site` only
- Permissions: `Contents: Read and write`, `Actions: Read and write`

**Expiry:** ~1 year from creation. Renewal reminder: [issue #14](https://github.com/Computer-Tsu/ShackDesk-PortPane/issues/14)

**To renew:**

1. GitHub → Personal account → **Settings → Developer settings → Personal access tokens → Fine-grained tokens**
2. Find the token scoped to `Computer-Consultant/ShackDesk-Site` and regenerate it
3. `Computer-Tsu/ShackDesk-PortPane` → **Settings → Secrets → Actions** → update `SITE_DISPATCH_PAT`
4. Verify the next release triggers the update JSON workflow successfully

**If the token expires:** PortPane releases still build and ship normally — only the automatic
update of `stable.json` / `beta.json` / `alpha.json` in ShackDesk-Site will fail silently.
Manually update the JSON files in ShackDesk-Site to restore auto-update functionality.

---

## 7. NuGet and Actions Dependency Updates

Dependabot opens pull requests automatically every Monday for:

- NuGet packages (`nuget` ecosystem)
- GitHub Actions versions (`github-actions` ecosystem)

PRs are labelled `dependencies` and `automated`.

**Review checklist for Dependabot PRs:**

- Check the package changelog for breaking changes before merging.
- For `Velopack` updates: verify the update/packaging API hasn't changed.
- For `NAudio` updates: spot-check audio device enumeration still works.
- For Actions updates (`actions/checkout`, `actions/setup-dotnet`, etc.): verify
  the major version hasn't changed (e.g. `@v4` → `@v5` requires manual review).

**To update manually:**

```powershell
# Check for outdated packages
dotnet list src/PortPane/PortPane.csproj package --outdated

# Update a specific package
dotnet add src/PortPane/PortPane.csproj package Serilog --version 4.2.0
```

---

*For security vulnerability reporting, see [SECURITY.md](SECURITY.md).*
*For contribution guidelines, see [CONTRIBUTING.md](CONTRIBUTING.md).*
