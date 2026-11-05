// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Provides the default implementation of <see cref="IEccCurveCatalog"/> containing the built-in NIST P-curves
/// (<c>P-256</c>, <c>P-384</c>, <c>P-521</c>). Additional curves registered as <see cref="EccCurveSpecification"/>
/// services are merged in after them, so the curves of one size are listed with the built-in first.
/// </summary>
// See ADR-1398: two curves can share a size, so a size selects a curve only when it is the one curve of that size.
internal sealed class DefaultEccCurveCatalog : IEccCurveCatalog
{
    private static IEnumerable<EccCurveSpecification> BuiltInCurves =>
        [EccCurveSpecification.P256, EccCurveSpecification.P384, EccCurveSpecification.P521];

    private FrozenDictionary<string, EccCurveSpecification> CurvesByName { get; }
    private FrozenDictionary<int, EccCurveSpecification[]> CurvesByCurveSizeBits { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<EccCurveSpecification> Curves { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEccCurveCatalog"/> class.
    /// </summary>
    /// <param name="additionalCurves">Additional curves registered by the application.</param>
    public DefaultEccCurveCatalog(IEnumerable<EccCurveSpecification> additionalCurves)
    {
        // A curve registered under a name the catalog holds replaces it in place, so the registration order is kept.
        var byName = new Dictionary<string, EccCurveSpecification>(StringComparer.Ordinal);
        foreach (var curve in BuiltInCurves.Concat(additionalCurves))
        {
            byName[curve.CurveName] = curve;
        }

        EccCurveSpecification[] curves = [.. byName.Values];
        CurvesByName = byName.ToFrozenDictionary(StringComparer.Ordinal);
        CurvesByCurveSizeBits = curves
            .GroupBy(curve => curve.CurveSizeBits)
            .ToFrozenDictionary(group => group.Key, group => group.ToArray());
        Curves = curves;
    }

    /// <inheritdoc />
    public bool TryGetByName(
        string curveName,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    ) => CurvesByName.TryGetValue(curveName, out specification);

    /// <inheritdoc />
    public IReadOnlyList<EccCurveSpecification> GetByCurveSizeBits(int curveSizeBits) =>
        CurvesByCurveSizeBits.TryGetValue(curveSizeBits, out var curves) ? curves : [];

    /// <inheritdoc />
    public bool TryGetByCurve(
        ECCurve curve,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    )
    {
        foreach (var candidate in Curves)
        {
            if (EccCurveSpecification.IsSameNamedCurve(candidate.Curve, curve))
            {
                specification = candidate;
                return true;
            }
        }

        specification = null;
        return false;
    }
}
