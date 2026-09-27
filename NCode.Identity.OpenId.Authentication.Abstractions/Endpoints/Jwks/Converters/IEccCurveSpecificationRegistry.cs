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
/// Provides the set of <see cref="EccCurveSpecification"/> instances that are supported for publication
/// by the <c>JSON Web Key Set (JWKS)</c> endpoint. This is the single, discoverable source of truth for
/// which elliptic curves are published (and under which <c>crv</c> name); a curve that is not present in
/// the registry is intentionally omitted from the key set.
/// </summary>
/// <remarks>
/// Applications can extend the supported set by registering additional <see cref="EccCurveSpecification"/>
/// services; the default registry combines those with its built-in NIST P-curves.
/// </remarks>
[PublicAPI]
public interface IEccCurveSpecificationRegistry
{
    /// <summary>
    /// Gets all supported <see cref="EccCurveSpecification"/> instances.
    /// </summary>
    IReadOnlyCollection<EccCurveSpecification> Specifications { get; }

    /// <summary>
    /// Attempts to get the <see cref="EccCurveSpecification"/> for a curve of the specified size in bits.
    /// </summary>
    /// <param name="curveSizeBits">The size of the curve, in bits (for example, <c>256</c>).</param>
    /// <param name="specification">When this method returns <c>true</c>, contains the matching
    /// <see cref="EccCurveSpecification"/>; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a supported curve of the specified size was found; otherwise, <c>false</c>.</returns>
    bool TryGetByCurveSizeBits(int curveSizeBits, [NotNullWhen(true)] out EccCurveSpecification? specification);
}
