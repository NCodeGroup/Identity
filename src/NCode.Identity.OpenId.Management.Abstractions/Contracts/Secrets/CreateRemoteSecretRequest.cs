// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to create a tenant's signing or encryption key in the key management service of a
/// registered remote key provider. The private key never leaves the key management service.
/// </summary>
// See ADR-1471.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateRemoteSecretRequest
{
    /// <summary>
    /// Gets the name of the registered remote key provider whose key management service holds the key, such as
    /// <c>aws-kms</c>.
    /// </summary>
    public required string RemoteKeyProvider { get; init; }

    /// <summary>
    /// Gets the type of secret to create: a type a key management service can hold, <c>rsa</c> or <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key to create. With a <see cref="Curve"/>, it is the size of the keys on that
    /// curve.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the curve to create the key on (the <c>JWK</c> <c>crv</c> name, for example <c>P-256</c>) for a secret type
    /// with curves, or <c>null</c> to leave it to the algorithm.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the JOSE signature or key-management algorithm the key is bound to, such as <c>RS256</c>, <c>ES256</c> or
    /// <c>RSA-OAEP-256</c>. The provider's row for it gives the key its use.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret starts to sign or encrypt, or <c>null</c> to activate it
    /// when it is created; a key published ahead of its use names a later instant. Never before the creation.
    /// </summary>
    public DateTimeOffset? ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret stops signing or encrypting while it still verifies or
    /// decrypts, or <c>null</c> to retire it when it expires. Never before <see cref="ActivatesWhen"/>, never after
    /// <see cref="ExpiresWhen"/>.
    /// </summary>
    public DateTimeOffset? RetiresWhen { get; init; }
}
