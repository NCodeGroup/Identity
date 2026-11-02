// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Provides the single, discoverable source of truth for the elliptic curves the server supports — used by key
/// generation (to resolve a requested curve) and by <c>JWK</c> publication (to name a key's <c>crv</c>). A curve
/// absent from the catalog is not generatable and is intentionally omitted from the <c>JWKS</c>.
/// Applications extend the set by registering additional <see cref="EccCurveSpecification"/> services.
/// </summary>
// See ADR-1068.
[PublicAPI]
public interface IEccCurveCatalog
{
    /// <summary>
    /// Gets all supported elliptic curves.
    /// </summary>
    IReadOnlyCollection<EccCurveSpecification> Curves { get; }

    /// <summary>
    /// Looks up a curve by its <c>JWK</c> <c>crv</c> name.
    /// </summary>
    bool TryGetByName(
        string curveName,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    );

    /// <summary>
    /// Looks up the default curve for the specified size in bits (used when no curve is explicitly requested).
    /// </summary>
    bool TryGetByCurveSizeBits(
        int curveSizeBits,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    );

    /// <summary>
    /// Looks up the curve that matches the specified <see cref="ECCurve"/> (by named-curve OID), used to name a
    /// deserialized key's <c>crv</c> unambiguously even when two curves share a size.
    /// </summary>
    bool TryGetByCurve(ECCurve curve, [NotNullWhen(true)] out EccCurveSpecification? specification);
}
