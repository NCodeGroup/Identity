// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes an elliptic curve the server supports for generating <c>ecc</c> secrets, so management clients can
/// offer a curve picker driven by server discovery.
/// </summary>
// See ADR-1068.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EccCurveCapabilityResource
{
    /// <summary>
    /// Gets the curve's <c>JWK</c> <c>crv</c> name (for example <c>P-256</c> or <c>secp256k1</c>).
    /// </summary>
    public required string CurveName { get; init; }

    /// <summary>
    /// Gets the size of the curve, in bits (for example <c>256</c>).
    /// </summary>
    public required int CurveSizeBits { get; init; }
}
