// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes one signing algorithm a remote key provider holds keys for, with the type, size and curve of the key,
/// which a request that creates or binds a key in the provider names.
/// </summary>
// See ADR-1476.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RemoteKeyAlgorithmCapabilityResource
{
    /// <summary>
    /// Gets the JOSE <c>alg</c> value of the signing algorithm, such as <c>ES256</c>.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the secret type of the key, either <c>rsa</c> or <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size of the key in bits.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the JWK <c>crv</c> name of the key's curve, or <c>null</c> for a key type with no curve.
    /// </summary>
    public string? Curve { get; init; }
}
