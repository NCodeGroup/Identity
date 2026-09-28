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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;

/// <summary>
/// Provides the default implementation of <see cref="IEccCurveSpecificationRegistry"/> containing the
/// built-in NIST P-curves (<c>P-256</c>, <c>P-384</c>, and <c>P-521</c>). Additional specifications
/// registered by the application are merged in and override a built-in that shares the same curve size.
/// </summary>
[PublicAPI]
internal class DefaultEccCurveSpecificationRegistry : IEccCurveSpecificationRegistry
{
    /// <summary>
    /// The <c>P-256</c> curve specification (<c>secp256r1</c> / <c>prime256v1</c>).
    /// </summary>
    public static EccCurveSpecification P256 { get; } =
        new() { CurveName = "P-256", CurveSizeBits = 256 };

    /// <summary>
    /// The <c>P-384</c> curve specification (<c>secp384r1</c>).
    /// </summary>
    public static EccCurveSpecification P384 { get; } =
        new() { CurveName = "P-384", CurveSizeBits = 384 };

    /// <summary>
    /// The <c>P-521</c> curve specification (<c>secp521r1</c>).
    /// </summary>
    public static EccCurveSpecification P521 { get; } =
        new() { CurveName = "P-521", CurveSizeBits = 521 };

    private static IEnumerable<EccCurveSpecification> BuiltInSpecifications => [P256, P384, P521];

    private Dictionary<int, EccCurveSpecification> SpecificationsByCurveSizeBits { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<EccCurveSpecification> Specifications { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEccCurveSpecificationRegistry"/> class.
    /// </summary>
    /// <param name="additionalSpecifications">Additional specifications registered by the application.
    /// These are merged with the built-in curves and override any built-in that shares the same curve size.</param>
    public DefaultEccCurveSpecificationRegistry(
        IEnumerable<EccCurveSpecification> additionalSpecifications
    )
    {
        SpecificationsByCurveSizeBits = new Dictionary<int, EccCurveSpecification>();

        foreach (var specification in BuiltInSpecifications.Concat(additionalSpecifications))
        {
            SpecificationsByCurveSizeBits[specification.CurveSizeBits] = specification;
        }

        Specifications = SpecificationsByCurveSizeBits.Values.ToArray();
    }

    /// <inheritdoc />
    public bool TryGetByCurveSizeBits(
        int curveSizeBits,
        [NotNullWhen(true)] out EccCurveSpecification? specification
    ) => SpecificationsByCurveSizeBits.TryGetValue(curveSizeBits, out specification);
}
