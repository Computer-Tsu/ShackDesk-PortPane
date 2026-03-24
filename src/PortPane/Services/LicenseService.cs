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

public sealed record LicenseInfo(
    LicenseTier Tier,
    string?     Licensee,
    string?     Email,
    string?     LicenseType,
    DateTimeOffset? ExpiresAt,
    bool        IsValid);

public enum LicenseTier { Free, Personal, Club, EmComm }

public sealed class LicenseService : ILicenseService
{
    // ── RSA Public Key ────────────────────────────────────────────────────────
    // Replace this with the actual 2048-bit RSA public key before v1.0 release.
    // The corresponding private key must NEVER appear in source or binary.
    // Key generation: openssl genrsa -out private.pem 2048 && openssl rsa -pubout -in private.pem -out public.pem
    private const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        PLACEHOLDER_REPLACE_WITH_ACTUAL_RSA_PUBLIC_KEY_BEFORE_RELEASE
        -----END PUBLIC KEY-----
        """;

    private static readonly string LicenseFilePath = LicensePath();
    private static readonly string HashFilePath     = LicensePath() + ".sha256";

    private LicenseInfo _current = FreeTier();

    public LicenseInfo Current => _current;

    public LicenseService()
    {
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
        _current = info;
        PersistToDisk(licenseKey);
        Log.Information("License activated: {Tier} for {Licensee}", info.Tier, info.Licensee);
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

    // ── Internal ──────────────────────────────────────────────────────────────

    private static LicenseInfo FreeTier()
        => new(LicenseTier.Free, null, null, null, null, IsValid: true);

    private LicenseInfo LoadAndValidate()
    {
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

            Log.Information("License loaded: {Tier} for {Licensee}", info.Tier, info.Licensee);
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
    /// Format: base64({ "licensee":"...", "email":"...", "type":"...", "app":"portpane",
    ///                   "issued":"...", "expires":"...", "version_max":"...", "signature":"..." })
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

            string?         licensee = root.TryGetProperty("licensee",  out var l) ? l.GetString() : null;
            string?         email    = root.TryGetProperty("email",     out var e) ? e.GetString() : null;
            string?         type     = root.TryGetProperty("type",      out var t) ? t.GetString() : null;
            string?         expires  = root.TryGetProperty("expires",   out var ex) ? ex.GetString() : null;

            LicenseTier tier = type?.ToLowerInvariant() switch
            {
                "personal"   => LicenseTier.Personal,
                "club"       => LicenseTier.Club,
                "emcomm"     => LicenseTier.EmComm,
                _            => LicenseTier.Free
            };

            DateTimeOffset? expiresAt = null;
            if (!string.IsNullOrWhiteSpace(expires) && !expires!.Equals("never", StringComparison.OrdinalIgnoreCase))
                expiresAt = DateTimeOffset.Parse(expires);

            return new LicenseInfo(tier, licensee, email, type, expiresAt, IsValid: true);
        }
        catch
        {
            return null;
        }
    }

    private static bool VerifySignature(LicenseInfo info)
    {
        // TODO: Replace placeholder logic with actual RSA signature verification.
        // When PublicKeyPem contains a real key:
        //   using var rsa = RSA.Create();
        //   rsa.ImportFromPem(PublicKeyPem);
        //   byte[] data = Encoding.UTF8.GetBytes(BuildSignablePayload(info));
        //   byte[] sig  = Convert.FromBase64String(signatureFromParsedKey);
        //   return rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        //
        // Until the real key is embedded, all keys parse as Free tier (not invalid).
        return false; // Placeholder: always reject commercial keys until key is embedded
    }

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
