// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to bind a signing key that a key management service already holds to a tenant. The
/// server proves the key signs with the supplied public half before it stores it, and never destroys it.
/// </summary>
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
    /// Gets the secret type (crypto family) of the key's public half. Supported values are <c>rsa</c> and <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the encoding of the supplied <see cref="KeyMaterial"/>: a lowercase format name the secret type imports.
    /// The built-in RSA and ECC types import <c>jwk</c>, <c>pem</c>, and <c>certificate</c>.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Gets the caller-supplied public key material, encoded per <see cref="Format"/> (JWK JSON text, PEM text, or
    /// base64 DER for a certificate).
    /// </summary>
    public required string KeyMaterial { get; init; }

    /// <summary>
    /// Gets the JOSE signature algorithm the key is bound to, such as <c>RS256</c> or <c>ES256</c>.
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
