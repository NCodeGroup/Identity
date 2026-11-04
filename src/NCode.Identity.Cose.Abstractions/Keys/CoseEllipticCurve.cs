// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cose.Keys;

/// <summary>
/// Describes a COSE elliptic curve: its IANA value, the key type that uses it and the JSON Web Key curve name it maps
/// onto. Curves are registered data, so an application adds a curve by registering a descriptor.
/// </summary>
// See ADR-1409.
[PublicAPI]
public sealed class CoseEllipticCurve
{
    /// <summary>
    /// Gets the value of the curve in the IANA COSE Elliptic Curves registry.
    /// </summary>
    public required int Identifier { get; init; }

    /// <summary>
    /// Gets the value of the key type that uses the curve.
    /// </summary>
    public required int KeyType { get; init; }

    /// <summary>
    /// Gets the JSON Web Key <c>crv</c> value the curve maps onto.
    /// </summary>
    public required string JwkCurveName { get; init; }
}
