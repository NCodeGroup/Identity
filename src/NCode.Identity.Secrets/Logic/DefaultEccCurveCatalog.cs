#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Logic;

/// <summary>
/// Provides the default implementation of <see cref="IEccCurveCatalog"/> containing the built-in NIST P-curves
/// (<c>P-256</c>, <c>P-384</c>, <c>P-521</c>). Additional curves registered as <see cref="EccCurveSpecification"/>
/// services are merged in; a built-in remains the size-default so the <c>crv</c> selected by size stays stable.
/// </summary>
internal sealed class DefaultEccCurveCatalog : IEccCurveCatalog
{
    private static EccCurveSpecification P256 { get; } =
        new()
        {
            CurveName = "P-256",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.nistP256,
        };

    private static EccCurveSpecification P384 { get; } =
        new()
        {
            CurveName = "P-384",
            CurveSizeBits = 384,
            Curve = ECCurve.NamedCurves.nistP384,
        };

    private static EccCurveSpecification P521 { get; } =
        new()
        {
            CurveName = "P-521",
            CurveSizeBits = 521,
            Curve = ECCurve.NamedCurves.nistP521,
        };

    private static IEnumerable<EccCurveSpecification> BuiltInCurves => [P256, P384, P521];

    private FrozenDictionary<string, EccCurveSpecification> CurvesByName { get; }
    private FrozenDictionary<int, EccCurveSpecification> CurvesByCurveSizeBits { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<EccCurveSpecification> Curves { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEccCurveCatalog"/> class.
    /// </summary>
    /// <param name="additionalCurves">Additional curves registered by the application.</param>
    public DefaultEccCurveCatalog(IEnumerable<EccCurveSpecification> additionalCurves)
    {
        var byName = new Dictionary<string, EccCurveSpecification>(StringComparer.Ordinal);
        var bySize = new Dictionary<int, EccCurveSpecification>();
        foreach (var curve in BuiltInCurves.Concat(additionalCurves))
        {
            byName[curve.CurveName] = curve;
            // Built-ins come first, so a built-in stays the size-default over a same-size application curve.
            bySize.TryAdd(curve.CurveSizeBits, curve);
        }

        CurvesByName = byName.ToFrozenDictionary(StringComparer.Ordinal);
        CurvesByCurveSizeBits = bySize.ToFrozenDictionary();
        Curves = CurvesByName.Values;
    }

    /// <inheritdoc />
    public bool TryGetByName(
        string curveName,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    ) => CurvesByName.TryGetValue(curveName, out specification);

    /// <inheritdoc />
    public bool TryGetByCurveSizeBits(
        int curveSizeBits,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    ) => CurvesByCurveSizeBits.TryGetValue(curveSizeBits, out specification);

    /// <inheritdoc />
    public bool TryGetByCurve(
        ECCurve curve,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    )
    {
        foreach (var candidate in Curves)
        {
            if (CurveOidMatches(candidate.Curve, curve))
            {
                specification = candidate;
                return true;
            }
        }

        specification = null;
        return false;
    }

    // Named curves are compared by OID value (preferred) or friendly name, which survive a PKCS#8 round-trip.
    private static bool CurveOidMatches(ECCurve left, ECCurve right)
    {
        if (!left.IsNamed || !right.IsNamed)
        {
            return false;
        }

        var leftValue = left.Oid?.Value;
        var rightValue = right.Oid?.Value;
        if (!string.IsNullOrEmpty(leftValue) && !string.IsNullOrEmpty(rightValue))
        {
            return string.Equals(leftValue, rightValue, StringComparison.Ordinal);
        }

        var leftName = left.Oid?.FriendlyName;
        var rightName = right.Oid?.FriendlyName;
        return !string.IsNullOrEmpty(leftName)
            && !string.IsNullOrEmpty(rightName)
            && string.Equals(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }
}
