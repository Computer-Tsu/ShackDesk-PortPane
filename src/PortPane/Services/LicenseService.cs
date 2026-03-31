using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Serilog;

namespace PortPane.Services;

public interface ILicenseService
{
    LicenseInfo Current { get; }
    Task<bool>  ActivateAsync(string licenseKey);
    Task        DeactivateAsync();
    bool        IsFeatureAvailable(string featureKey);
}

/// <summary>
/// Parsed license details. Signature, Issued, ExpiresRaw, and VersionMax are
/// retained from the raw JSON so VerifySignature can reconstruct the exact
/// signable payload.
/// </summary>
public sealed record LicenseInfo(
    LicenseTier     Tier,
    string?         Licensee,
    string?         Email,
    string?         LicenseType,
    DateTimeOffset? ExpiresAt,
    bool            IsValid,
    string?         Issued     = null,   // raw ISO 8601 string from JSON
    string?         ExpiresRaw = null,   // raw string from JSON ("never" or ISO 8601)
    string?         VersionMax = null,   // raw string from JSON (e.g. "0.6", "1.0", or null)
    string?         Signature  = null);  // base64 RSA-SHA256-PKCS1 signature

public enum LicenseTier { Free, Personal, Club, EmComm }

public sealed class LicenseService : ILicenseService
{
    // ── RSA Public Key (XML format) ───────────────────────────────────────────
    // This is the PUBLIC half of the RSA-2048 key pair used to verify license
    // key signatures. It is safe to embed here — it cannot be used to generate
    // or forge keys. Only the private key (stored in GitHub Secrets, never in
    // source) can create valid signatures.
    // Key setup, rotation procedure, and signing format: see MAINTENANCE.md § 1-2
    private const string PublicKeyXml =
        "<RSAKeyValue>" +
        "<Modulus>vpfgPKk/y+nJhQdiIVBX5u9xZOBwcRn/GyqijBEczgoBWF0+4W4AbdBggljSWP0r7EHKZnOd3CZpOWPLMXfUhS3zYGEKzrXcdquWMFDZZ2tV09YoaEux1mTH2slxprt97WwYpN45XTRHPtOBDXaoXK/DCAtdG7gyNVC7P4MGGAd/XlxcR8PtvEV7KcFmCUZSmNYjcSkBtwN/IOQLXvFj2Ll67PK1H7NwaqHIqmYjmnq/xlTW3IT7do+6AiH4zfwEhuDd3B0wYDohHy4iErACJujdK+BSnNGkYu58+CveNQyWG7L+e0D9g3y8iOCyQ1EBerEz4JZsagELaxcNVsMPEQ==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "</RSAKeyValue>";

    // ── Signable payload format ───────────────────────────────────────────────
    // The payload signed by the private key is a pipe-delimited string built
    // from these fields in this exact order:
    //
    //   portpane|{type}|{licensee}|{email}|{issued}|{expires}|{version_max}
    //
    // Rules:
    //   - All values are the exact strings from the license JSON (no reformatting)
    //   - {type}        : "personal", "club", or "emcomm"
    //   - {licensee}    : licensee name
    //   - {email}       : licensee email
    //   - {issued}      : ISO 8601 UTC string, e.g. "2026-03-28T00:00:00Z"
    //   - {expires}     : ISO 8601 UTC string, or the literal string "never"
    //   - {version_max} : version ceiling string (e.g. "0.6"), or "" if absent
    //   - Null/missing fields use an empty string in the payload
    //
    // Signing (in your key generator / New-License.ps1):
    //   using var rsa  = RSA.Create();
    //   rsa.ImportFromPem(privateKeyPem);
    //   string payload = $"portpane|{type}|{licensee}|{email}|{issued}|{expires}|{versionMax}";
    //   byte[] data    = Encoding.UTF8.GetBytes(payload);
    //   byte[] sig     = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    //   string sigB64  = Convert.ToBase64String(sig);
    //
    // License key format (JSON before base64 encoding):
    //   {
    //     "app":         "portpane",
    //     "type":        "personal" | "club" | "emcomm",
    //     "licensee":    "Full Name",
    //     "email":       "user@example.com",
    //     "issued":      "2026-03-28T00:00:00Z",
    //     "expires":     "2027-03-28T00:00:00Z"  (or "never"),
    //     "version_max": "0.6"                   (optional; omit for no ceiling),
    //     "signature":   "<base64 RSA-SHA256-PKCS1 signature of payload above>"
    //   }
    //   Final key = Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerialize(above)))

    private static readonly string LicenseFilePath = LicensePath();
    private static readonly string HashFilePath     = LicensePath() + ".sha256";

    private readonly bool _unlockForTesting;
    private LicenseInfo _current = FreeTier();

    public LicenseInfo Current => _current;

    public LicenseService() : this(ChannelInfo.UnlockAllForTesting) { }

    internal LicenseService(bool unlockForTesting)
    {
        _unlockForTesting = unlockForTesting;
        _current = LoadAndValidate();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<bool> ActivateAsync(string licenseKey)
    {
        await Task.CompletedTask; // offline only — no network call
        var info = TryParse(licenseKey);
        if (info is null)
        {
            Log.Warning("License activation failed: could not parse key");
            return false;
        }
        if (!VerifySignature(info))
        {
            Log.Warning("License activation failed: invalid signature");
            return false;
        }
        if (info.ExpiresAt.HasValue && info.ExpiresAt.Value < DateTimeOffset.UtcNow)
        {
            Log.Warning("License activation failed: key expired on {Date}", info.ExpiresAt);
            return false;
        }
        if (!IsVersionAllowed(info.VersionMax))
        {
            Log.Warning("License activation failed: version ceiling {Max} already reached", info.VersionMax);
            return false;
        }
        _current = info;
        PersistToDisk(licenseKey);
        Log.Information("License activated: {Tier} for {Licensee} (expires: {Exp}, version_max: {Max})",
            info.Tier, info.Licensee, info.ExpiresRaw ?? "never", info.VersionMax ?? "none");
        return true;
    }

    public async Task DeactivateAsync()
    {
        _current = FreeTier();
        DeleteFromDisk();
        await Task.CompletedTask;
        Log.Information("License deactivated; reverted to Free tier");
    }

    public bool IsFeatureAvailable(string featureKey)
    {
        // All features available in GPL tier. Reserved for future commercial gating.
        return true;
    }

    // ── Internal (accessible to PortPane.Tests via InternalsVisibleTo) ────────

    /// <summary>
    /// Returns true if the running app version is within the license's version ceiling.
    /// version_max="0.6" means valid for any 0.6.x and below; invalid at 0.7.0+.
    /// Comparison strips pre-release suffixes (e.g. "0.5.1-beta" → "0.5.1").
    /// Precision is determined by version_max: "0.6" compares major.minor only.
    /// </summary>
    internal static bool IsVersionAllowed(string? versionMax)
    {
        if (string.IsNullOrWhiteSpace(versionMax)) return true;

        // Strip pre-release suffix from the running app version
        string appVer = BrandingInfo.Version;
        int dash = appVer.IndexOf('-');
        if (dash >= 0) appVer = appVer[..dash];

        if (!TryParseVersionParts(appVer,    out int[] appParts)) return true;
        if (!TryParseVersionParts(versionMax, out int[] maxParts)) return true;

        // Compare only to the precision stated in version_max.
        // If all compared parts are equal, the app is within the ceiling.
        for (int i = 0; i < maxParts.Length; i++)
        {
            int a = i < appParts.Length ? appParts[i] : 0;
            if (a < maxParts[i]) return true;  // clearly below ceiling
            if (a > maxParts[i]) return false; // clearly above ceiling
            // equal so far — continue to next component
        }
        return true; // all compared parts equal → within ceiling
    }

    internal static bool TryParseVersionParts(string version, out int[] parts)
    {
        parts = [];
        string[] segments = version.Trim().Split('.');
        var result = new List<int>();
        foreach (string seg in segments)
        {
            if (!int.TryParse(seg, out int n)) { parts = []; return false; }
            result.Add(n);
        }
        if (result.Count == 0) return false;
        parts = [.. result];
        return true;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private static LicenseInfo FreeTier()
        => new(LicenseTier.Free, null, null, null, null, IsValid: true);

    private LicenseInfo LoadAndValidate()
    {
        if (_unlockForTesting)
            return new LicenseInfo(LicenseTier.Personal, "Alpha Tester", null, "personal", null, IsValid: true);

        if (!File.Exists(LicenseFilePath)) return FreeTier();

        try
        {
            string keyData = File.ReadAllText(LicenseFilePath).Trim();

            // Tamper detection: verify stored SHA-256 matches current file
            if (File.Exists(HashFilePath))
            {
                string storedHash   = File.ReadAllText(HashFilePath).Trim();
                string computedHash = Sha256Hex(keyData);
                if (!string.Equals(storedHash, computedHash, StringComparison.OrdinalIgnoreCase))
                {
                    Log.Warning("License file tamper detected; reverting to Free tier");
                    return FreeTier();
                }
            }

            var info = TryParse(keyData);
            if (info is null) return FreeTier();

            if (!VerifySignature(info))
            {
                Log.Warning("License signature invalid; reverting to Free tier");
                return FreeTier();
            }

            if (info.ExpiresAt.HasValue && info.ExpiresAt.Value < DateTimeOffset.UtcNow)
            {
                Log.Warning("License expired on {Date}; reverting to Free tier", info.ExpiresAt);
                return FreeTier();
            }

            if (!IsVersionAllowed(info.VersionMax))
            {
                Log.Warning("License version ceiling reached ({Max}); reverting to Free tier", info.VersionMax);
                return FreeTier();
            }

            Log.Information("License loaded: {Tier} for {Licensee} (expires: {Exp}, version_max: {Max})",
                info.Tier, info.Licensee, info.ExpiresRaw ?? "never", info.VersionMax ?? "none");
            return info;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not load license file; defaulting to Free tier");
            return FreeTier();
        }
    }

    /// <summary>
    /// Parses a base64-encoded signed JSON license key.
    /// See the signable payload format comment above for the full JSON schema.
    /// </summary>
    private static LicenseInfo? TryParse(string licenseKey)
    {
        try
        {
            string json    = Encoding.UTF8.GetString(Convert.FromBase64String(licenseKey));
            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            if (!root.TryGetProperty("app", out var appProp)
                || !appProp.GetString()!.Equals("portpane", StringComparison.OrdinalIgnoreCase))
                return null;

            string? licensee   = root.TryGetProperty("licensee",    out var l)   ? l.GetString()   : null;
            string? email      = root.TryGetProperty("email",       out var e)   ? e.GetString()   : null;
            string? type       = root.TryGetProperty("type",        out var t)   ? t.GetString()   : null;
            string? expires    = root.TryGetProperty("expires",     out var ex)  ? ex.GetString()  : null;
            string? issued     = root.TryGetProperty("issued",      out var iss) ? iss.GetString() : null;
            string? versionMax = root.TryGetProperty("version_max", out var vm)  ? vm.GetString()  : null;
            string? signature  = root.TryGetProperty("signature",   out var sig) ? sig.GetString() : null;

            LicenseTier tier = type?.ToLowerInvariant() switch
            {
                "personal" => LicenseTier.Personal,
                "club"     => LicenseTier.Club,
                "emcomm"   => LicenseTier.EmComm,
                _          => LicenseTier.Free
            };

            DateTimeOffset? expiresAt = null;
            if (!string.IsNullOrWhiteSpace(expires) && !expires!.Equals("never", StringComparison.OrdinalIgnoreCase))
                expiresAt = DateTimeOffset.Parse(expires);

            return new LicenseInfo(
                Tier:        tier,
                Licensee:    licensee,
                Email:       email,
                LicenseType: type,
                ExpiresAt:   expiresAt,
                IsValid:     true,
                Issued:      issued,
                ExpiresRaw:  expires,
                VersionMax:  versionMax,
                Signature:   signature);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Verifies the RSA-SHA256-PKCS1 signature on a parsed license.
    /// Payload: portpane|{type}|{licensee}|{email}|{issued}|{expires}|{version_max}
    /// See the format comment near PublicKeyXml for full details.
    /// </summary>
    private static bool VerifySignature(LicenseInfo info)
    {
        if (string.IsNullOrWhiteSpace(info.Signature))
        {
            Log.Warning("License has no signature field");
            return false;
        }

        try
        {
            string payload = BuildSignablePayload(info);
            byte[] data    = Encoding.UTF8.GetBytes(payload);
            byte[] sig     = Convert.FromBase64String(info.Signature);

            using var rsa = RSA.Create();
            rsa.FromXmlString(PublicKeyXml);
            bool valid = rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            if (!valid)
                Log.Debug("RSA signature check failed for payload: {Payload}", payload);

            return valid;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "License signature verification threw an exception");
            return false;
        }
    }

    private static string BuildSignablePayload(LicenseInfo info) =>
        string.Join("|",
            "portpane",
            info.LicenseType ?? "",
            info.Licensee    ?? "",
            info.Email       ?? "",
            info.Issued      ?? "",
            info.ExpiresRaw  ?? "",
            info.VersionMax  ?? "");

    private void PersistToDisk(string licenseKey)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LicenseFilePath)!);
        File.WriteAllText(LicenseFilePath, licenseKey);
        File.WriteAllText(HashFilePath, Sha256Hex(licenseKey));
    }

    private static void DeleteFromDisk()
    {
        if (File.Exists(LicenseFilePath)) File.Delete(LicenseFilePath);
        if (File.Exists(HashFilePath))    File.Delete(HashFilePath);
    }

    private static string LicensePath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            BrandingInfo.SuiteName, BrandingInfo.AppName, "license.portpane");

    private static string Sha256Hex(string input)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
