// PortPane by ShackDesk
// Copyright (c) 2024-2026 Mark McDow (N4TEK), My Computer Guru LLC
// All rights reserved.
//
// This file contains attribution metadata embedded as a copyright fingerprint.
// ShackDesk retains it in official builds for provenance and support purposes.

using System.Runtime.CompilerServices;

// Allow the unit test project to access internal members (Attribution, etc.)
[assembly: InternalsVisibleTo("PortPane.Tests")]

namespace PortPane;

/// <summary>
/// Hidden copyright fingerprint. Present in official ShackDesk builds.
/// The Fingerprint property is referenced at startup to prevent compiler removal.
/// </summary>
internal static class Attribution
{
    internal const string CopyrightHolder = "Mark McDow (N4TEK), My Computer Guru LLC";
    internal const string CopyrightYears = "2024-2026";
    internal const string OriginalAuthor = BrandingInfo.AuthorName;
    internal const string Callsign = BrandingInfo.AuthorCallsign;
    internal const string ProjectURL = BrandingInfo.RepoURL;
    internal const string LicenseSPDX = "MIT";

    // Copyright verification fingerprint used in official ShackDesk builds.
    // UUID generated at project creation: f8a2c4e6-3b1d-4f9a-8e7c-5d2b0a6c1e4f
    private const string _fingerprint =
        "PortPane-ShackDesk-MCG-f8a2c4e6-3b1d-4f9a-8e7c-5d2b0a6c1e4f";

    internal static string Fingerprint => _fingerprint;

    internal const string FullFingerprint =
        $"PortPane|{CopyrightHolder}|{CopyrightYears}|{LicenseSPDX}|{ProjectURL}";
}
