// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the metadata used to generate a new <see cref="PersistedSecret"/> whose private key is created in a key
/// management service and never leaves it, so the secret stores only the key's public half and a reference to it.
/// </summary>
// See ADR-1471.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class GenerateRemoteSecretRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the generated secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the owner of the generated secret, whose successive keys for one algorithm are the versions of one key in
    /// the key management service.
    /// </summary>
    public required SecretOwner Owner { get; init; }

    /// <summary>
    /// Gets the name of the remote key provider that creates the key. See <c>RemoteKeyProviderNames</c> for the
    /// built-in values.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the type of secret to generate, such as <see cref="SecretTypes.Rsa"/> or <see cref="SecretTypes.Ecc"/>.
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
    /// Gets the intended use for the generated secret. See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public required string Use { get; init; }

    /// <summary>
    /// Gets the JOSE <c>alg</c> value of the signature algorithm the generated secret signs with.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret starts to sign, or <c>null</c> to activate it
    /// when it is created. A key published ahead of its use names a later instant. Never before
    /// <see cref="CreatedWhen"/>.
    /// </summary>
    // See ADR-1403.
    public DateTimeOffset? ActivatesWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret stops signing while it still verifies, or
    /// <c>null</c> to retire it when it expires. Never before <see cref="ActivatesWhen"/>, never after
    /// <see cref="ExpiresWhen"/>.
    /// </summary>
    // See ADR-1403.
    public DateTimeOffset? RetiresWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the generated secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets the name of the component that manages the generated secret's lifecycle on its own, or <c>null</c> when
    /// the secret keeps the lifecycle this request gives it. See <see cref="PersistedSecret.ManagedBy"/>.
    /// </summary>
    // See ADR-1404.
    public string? ManagedBy { get; init; }
}
