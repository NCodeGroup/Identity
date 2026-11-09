// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to bind a signing or encryption key that a key management service already holds to a
/// tenant. The request names the key, its type, size, curve and algorithm, and its lifecycle. The server reads the
/// key's public half from the service and proves the key holds its private half — the key signs a challenge, or
/// decrypts a challenge encrypted to that half — before it stores the key, and never destroys it.
/// </summary>
// See ADR-1476.
// See ADR-1477.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ImportRemoteSecretRequest
{
    /// <summary>
    /// Gets the name of the registered remote key provider whose key management service holds the key, such as
    /// <c>aws-kms</c>.
    /// </summary>
    public required string RemoteKeyProvider { get; init; }

    /// <summary>
    /// Gets the key management service's identifier for the key: an AWS KMS key ARN, an Azure Key Vault versioned key
    /// identifier, or a Google Cloud KMS crypto key version resource name.
    /// </summary>
    public required string RemoteKeyId { get; init; }

    /// <summary>
    /// Gets the type of the key the key management service holds: a type a key management service can hold,
    /// <c>rsa</c> or <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key the key management service holds. With a <see cref="Curve"/>, it is the size
    /// of the keys on that curve.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the curve the key the key management service holds is on (the <c>JWK</c> <c>crv</c> name, for example
    /// <c>P-256</c>) for a secret type with curves, or <c>null</c> to leave it to the algorithm.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the JOSE signature or key-management algorithm the key is bound to, such as <c>RS256</c>, <c>ES256</c> or
    /// <c>RSA-OAEP-256</c>. The provider's row for it gives the key its use.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret expires and is no longer valid.
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
