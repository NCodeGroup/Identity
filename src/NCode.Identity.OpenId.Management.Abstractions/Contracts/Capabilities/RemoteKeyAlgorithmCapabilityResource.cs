// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes one signature or key-management algorithm a remote key provider holds keys for, with the use and the
/// type, size and curve of the key, which a request that creates or binds a key in the provider names.
/// </summary>
// See ADR-1476.
// See ADR-1471.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RemoteKeyAlgorithmCapabilityResource
{
    /// <summary>
    /// Gets the JOSE <c>alg</c> value of the signature or key-management algorithm, such as <c>ES256</c> or
    /// <c>RSA-OAEP-256</c>.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the use of a key the provider holds for the algorithm: <c>sig</c> for a signature algorithm, <c>enc</c> for
    /// a key-management algorithm.
    /// </summary>
    public required string Use { get; init; }

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
