// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Describes one key every tenant is provisioned with, resolved from its configured algorithm against the registered
/// algorithms and key types.
/// </summary>
internal readonly record struct TenantKeySpecification
{
    /// <summary>
    /// Gets the code of the algorithm the key is generated for and bound to.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the use the algorithm implies for the key (<c>sig</c> or <c>enc</c>).
    /// </summary>
    public required string Use { get; init; }

    /// <summary>
    /// Gets the identifier of the registered secret key type the key is generated as.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key to generate.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> name of the curve to generate the key on: the first curve the algorithm accepts
    /// that has keys of <see cref="KeySizeBits"/>, or <c>null</c> when the algorithm names no curve for the key
    /// type.
    /// </summary>
    public string? Curve { get; init; }
}
