// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// The table of the supported OKP curves — the signing curves Ed25519 and Ed448 and the key-agreement curves X25519 and
/// X448 — indexed by the JWK <c>crv</c> name (RFC 8037) and the X.509 algorithm OID (RFC 8410). Each curve carries its
/// own operations, so a curve is added by adding one <see cref="OkpCurve"/> to the table.
/// </summary>
// See ADR-1390.
internal static class OkpCurves
{
    public const string Ed25519Name = "Ed25519";
    public const string Ed448Name = "Ed448";
    public const string X25519Name = "X25519";
    public const string X448Name = "X448";

    public static EdwardsOkpCurve Ed25519 { get; } = new Ed25519Curve();
    public static EdwardsOkpCurve Ed448 { get; } = new Ed448Curve();
    public static MontgomeryOkpCurve X25519 { get; } = new X25519Curve();
    public static MontgomeryOkpCurve X448 { get; } = new X448Curve();

    private static OkpCurve[] All { get; } = [Ed25519, Ed448, X25519, X448];

    private static FrozenDictionary<string, OkpCurve> ByName { get; } =
        All.ToFrozenDictionary(curve => curve.Name, StringComparer.Ordinal);

    private static FrozenDictionary<string, OkpCurve> ByOid { get; } =
        All.ToFrozenDictionary(curve => curve.Oid, StringComparer.Ordinal);

    /// <summary>
    /// Gets the names of every supported curve, for messages that list them.
    /// </summary>
    public static string SupportedNames { get; } =
        string.Join(", ", All.Select(curve => $"'{curve.Name}'"));

    /// <summary>
    /// Resolves a curve by its JWK <c>crv</c> name.
    /// </summary>
    public static bool TryGetByName(string? name, [NotNullWhen(true)] out OkpCurve? curve)
    {
        curve = null;
        return name is not null && ByName.TryGetValue(name, out curve);
    }

    /// <summary>
    /// Resolves a curve of the specified kind by its JWK <c>crv</c> name, so a key of the other use is never resolved.
    /// </summary>
    public static bool TryGetByName<TCurve>(string? name, [NotNullWhen(true)] out TCurve? curve)
        where TCurve : OkpCurve
    {
        curve = TryGetByName(name, out var resolved) ? resolved as TCurve : null;
        return curve is not null;
    }

    /// <summary>
    /// Resolves a curve by its X.509 algorithm OID (RFC 8410 §3).
    /// </summary>
    public static bool TryGetByOid(string? oid, [NotNullWhen(true)] out OkpCurve? curve)
    {
        curve = null;
        return oid is not null && ByOid.TryGetValue(oid, out curve);
    }

    /// <summary>
    /// Resolves a signing curve by its key size; the size alone cannot tell Ed25519 from X25519, so only signing
    /// curves are considered.
    /// </summary>
    public static bool TryGetSigningCurveByKeySizeBits(
        int keySizeBits,
        [NotNullWhen(true)] out EdwardsOkpCurve? curve
    )
    {
        curve = All.OfType<EdwardsOkpCurve>()
            .FirstOrDefault(candidate => candidate.KeySizeBits == keySizeBits);
        return curve is not null;
    }
}
