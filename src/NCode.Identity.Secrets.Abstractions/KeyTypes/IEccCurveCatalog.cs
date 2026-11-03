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
    /// Gets the curves whose keys are the specified size, in bits, in the order they are registered with the built-in
    /// curves first, or an empty list when no curve has the size. Two curves can share a size, so a size selects a
    /// curve only when it is the one curve of that size.
    /// </summary>
    /// <param name="curveSizeBits">The size of the curves' keys, in bits.</param>
    /// <returns>The curves of that size.</returns>
    IReadOnlyList<EccCurveSpecification> GetByCurveSizeBits(int curveSizeBits);

    /// <summary>
    /// Looks up the curve that matches the specified <see cref="ECCurve"/> (by named-curve OID), used to name a
    /// deserialized key's <c>crv</c> unambiguously even when two curves share a size.
    /// </summary>
    bool TryGetByCurve(ECCurve curve, [NotNullWhen(true)] out EccCurveSpecification? specification);
}
