#Requires -Version 7.0
<#
.SYNOPSIS
    Generates a signed PortPane license file (.portpane).

.DESCRIPTION
    Creates a cryptographically signed license key using RSA-SHA256-PKCS1 and
    writes it to a .portpane file the licensee can activate via drag-drop or
    paste in Settings → License.

    REQUIRES: The RSA private key PEM file (never committed to the repo).
    Default location: keys/portpane-private.pem (already gitignored).
    Export the key from GitHub Secrets once and store it there, or pass a
    different path with -PrivateKeyPath.

.PARAMETER Licensee
    Full name of the person or organization being licensed.

.PARAMETER Email
    Contact email address for the licensee.

.PARAMETER Tier
    License tier. One of: personal, club, emcomm
      personal — individual use
      club     — amateur radio club (licensee = club name/callsign)
      emcomm   — emergency communications group

.PARAMETER Expires
    Expiration date in YYYY-MM-DD format, or "never".
    Default: never (permanent license).
    For beta testers use a date 3-6 months out, e.g. "2026-09-01".

.PARAMETER VersionMax
    Maximum app version this license is valid for.
    Format: "major.minor" (e.g. "0.6", "1.0").
    The license reverts to Free when the app version exceeds this ceiling.
    Omit or leave empty for no version ceiling (permanent across all versions).
    For beta testers: "0.6" means valid through all 0.6.x releases.

    Version comparison rules:
      - Pre-release suffixes are stripped before comparing
        ("0.6.0-beta" → "0.6.0", "0.5.1-Beta" → "0.5.1")
      - Precision matches version_max: "0.6" compares major.minor only
      - Ceiling is inclusive: version_max="0.6" allows 0.6.0, 0.6.5, etc.
      - 0.7.0 would be the first version that exceeds a "0.6" ceiling

.PARAMETER PrivateKeyPath
    Path to the RSA-2048 private key PEM file.
    Default: keys/portpane-private.pem (relative to this script's directory).
    Supports both PKCS#8 (-----BEGIN PRIVATE KEY-----) and
    PKCS#1 (-----BEGIN RSA PRIVATE KEY-----) formats.

.PARAMETER OutputDir
    Directory where the .portpane file will be written.
    Default: current working directory.

.EXAMPLE
    # Your own permanent personal license — no expiry, no version ceiling
    .\New-License.ps1 -Licensee "Mark McDow" -Email "mark@example.com" -Tier personal

.EXAMPLE
    # Beta tester license — expires 2026-09-01, valid through 0.6.x
    .\New-License.ps1 `
        -Licensee   "Jane Tester" `
        -Email      "jane@example.com" `
        -Tier       personal `
        -Expires    "2026-09-01" `
        -VersionMax "0.6"

.EXAMPLE
    # Club license — permanent, no version ceiling
    .\New-License.ps1 `
        -Licensee "W4XYZ Amateur Radio Club" `
        -Email    "trustee@w4xyz.org" `
        -Tier     club

.EXAMPLE
    # Using a private key stored elsewhere
    .\New-License.ps1 `
        -Licensee       "Mark McDow" `
        -Email          "mark@example.com" `
        -Tier           personal `
        -PrivateKeyPath "C:\Secure\portpane-private.pem" `
        -OutputDir      "C:\Licenses"

.NOTES
    SECURITY:
      - Never commit keys/portpane-private.pem — it is gitignored.
      - The generated .portpane file contains the licensee's name and email
        in the signed JSON. Do not post it publicly.
      - The license is tied to no hardware or machine — anyone with the file
        can activate it. Treat it like a serial number.

    SIGNING FORMAT (for reference / cross-checking):
      Payload = "portpane|{type}|{licensee}|{email}|{issued}|{expires}|{version_max}"
      All values are the exact strings written to the JSON (no reformatting).
      version_max is "" (empty string) in the payload when omitted from JSON.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Licensee,
    [Parameter(Mandatory)][string]$Email,
    [Parameter(Mandatory)][ValidateSet('personal', 'club', 'emcomm')][string]$Tier,
    [string]$Expires        = 'never',
    [string]$VersionMax     = '',
    [string]$PrivateKeyPath = '',
    [string]$OutputDir      = '.'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Resolve private key path ──────────────────────────────────────────────────
if (-not $PrivateKeyPath) {
    $PrivateKeyPath = Join-Path $PSScriptRoot '..\keys\portpane-private.pem'
}
$PrivateKeyPath = [System.IO.Path]::GetFullPath($PrivateKeyPath)

if (-not (Test-Path $PrivateKeyPath)) {
    Write-Error @"
Private key not found at: $PrivateKeyPath

To obtain the private key:
  1. Go to your GitHub repo → Settings → Secrets and variables → Actions
  2. Find RSA_LICENSE_PRIVATE_KEY
  3. Copy the value and save it to: $PrivateKeyPath
     (this path is already in .gitignore — it will not be committed)

Alternatively, pass a different path: -PrivateKeyPath "C:\Secure\portpane-private.pem"
"@
}

# ── Load and import RSA private key ──────────────────────────────────────────
$pemContent = Get-Content $PrivateKeyPath -Raw

try {
    $rsa = [System.Security.Cryptography.RSA]::Create()
    $rsa.ImportFromPem($pemContent)
} catch {
    Write-Error "Failed to import private key from ${PrivateKeyPath}: $_"
}

# ── Build issued / expires values ─────────────────────────────────────────────
$issued = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

if ($Expires -eq 'never') {
    $expiresValue = 'never'
} else {
    try {
        $expiresValue = [datetime]::Parse($Expires).ToUniversalTime().ToString('yyyy-MM-ddT00:00:00Z')
    } catch {
        Write-Error "Could not parse -Expires value '$Expires'. Use YYYY-MM-DD format or 'never'."
    }
}

# ── Build signable payload ────────────────────────────────────────────────────
# Must exactly match LicenseService.BuildSignablePayload() in the app:
#   portpane|{type}|{licensee}|{email}|{issued}|{expires}|{version_max}
$payload      = "portpane|$Tier|$Licensee|$Email|$issued|$expiresValue|$VersionMax"
$payloadBytes = [System.Text.Encoding]::UTF8.GetBytes($payload)

# ── Sign ──────────────────────────────────────────────────────────────────────
try {
    $sigBytes  = $rsa.SignData(
        $payloadBytes,
        [System.Security.Cryptography.HashAlgorithmName]::SHA256,
        [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $signature = [Convert]::ToBase64String($sigBytes)
} catch {
    Write-Error "RSA signing failed: $_"
} finally {
    $rsa.Dispose()
}

# ── Build license JSON ────────────────────────────────────────────────────────
$licenseObj = [ordered]@{
    app       = 'portpane'
    type      = $Tier
    licensee  = $Licensee
    email     = $Email
    issued    = $issued
    expires   = $expiresValue
    signature = $signature
}
# version_max is omitted entirely when not specified (cleaner than empty string in JSON)
if ($VersionMax) {
    $licenseObj['version_max'] = $VersionMax
}

$json    = $licenseObj | ConvertTo-Json -Compress -Depth 2
$keyData = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($json))

# ── Write output file ─────────────────────────────────────────────────────────
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir | Out-Null
}

$safeName = $Licensee -replace '[^a-zA-Z0-9_\-]', '_'
$outFile  = Join-Path $OutputDir "$safeName.portpane"
$keyData | Out-File -FilePath $outFile -Encoding ascii -NoNewline

# ── Report ────────────────────────────────────────────────────────────────────
Write-Host ''
Write-Host '  License generated successfully' -ForegroundColor Green
Write-Host ''
Write-Host "  File       : $outFile"
Write-Host "  Tier       : $Tier"
Write-Host "  Licensee   : $Licensee"
Write-Host "  Email      : $Email"
Write-Host "  Issued     : $issued"
Write-Host "  Expires    : $expiresValue"
if ($VersionMax) {
    Write-Host "  Version max: $VersionMax  (reverts to Free at $VersionMax.x+1)"
} else {
    Write-Host "  Version max: none (valid for all versions)"
}
Write-Host ''
Write-Host '  Activation instructions for the licensee:' -ForegroundColor Cyan
Write-Host "    1. Open PortPane → Settings → License tab"
Write-Host "    2. Drag the .portpane file onto the key box, OR paste the file contents"
Write-Host "    3. Click Activate"
Write-Host ''
