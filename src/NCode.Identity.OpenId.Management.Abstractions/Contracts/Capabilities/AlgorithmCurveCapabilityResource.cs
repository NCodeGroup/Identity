// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes one curve that an algorithm accepts keys of a secret type on, by its <c>JWK</c> <c>crv</c> name and the
/// size of its keys, so a form that offers the curve also knows the size of the key it generates.
/// </summary>
// See ADR-1389.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AlgorithmCurveCapabilityResource
{
    /// <summary>
    /// Gets the curve's <c>JWK</c> <c>crv</c> name (for example <c>P-384</c>, <c>secp256k1</c> or <c>X25519</c>).
    /// </summary>
    public required string CurveName { get; init; }

    /// <summary>
    /// Gets the size, in bits, of keys on the curve (for example <c>384</c>).
    /// </summary>
    public required int CurveSizeBits { get; init; }
}
