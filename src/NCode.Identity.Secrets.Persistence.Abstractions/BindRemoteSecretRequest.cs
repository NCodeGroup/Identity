// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the metadata used to bind a key that a key management service already holds to a
/// <see cref="PersistedSecret"/>, which stores the key's public half, read from the service, and a reference to the
/// key. The server never destroys a bound key.
/// </summary>
// See ADR-1476.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class BindRemoteSecretRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the bound secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the name of the remote key provider whose key management service holds the key. See
    /// <c>RemoteKeyProviderNames</c> for the built-in values.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the key management service's identifier for the key.
    /// </summary>
    public required string KeyId { get; init; }

    /// <summary>
    /// Gets the type of the key, such as <see cref="SecretTypes.Rsa"/> or <see cref="SecretTypes.Ecc"/>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size of the key, in bits.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the JWK <c>crv</c> value of the key's curve, or <c>null</c> for a key type with no curves.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the intended use for the bound secret. See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public required string Use { get; init; }

    /// <summary>
    /// Gets the JOSE <c>alg</c> value of the signature algorithm the bound secret signs with.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the bound secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the bound secret starts to sign, or <c>null</c> to activate it
    /// when it is created. A key published ahead of its use names a later instant. Never before
    /// <see cref="CreatedWhen"/>.
    /// </summary>
    // See ADR-1403.
    public DateTimeOffset? ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the bound secret stops signing while it still verifies, or
    /// <c>null</c> to retire it when it expires. Never before <see cref="ActivatesWhen"/>, never after
    /// <see cref="ExpiresWhen"/>.
    /// </summary>
    // See ADR-1403.
    public DateTimeOffset? RetiresWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the bound secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
