// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedSecret"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretResource
{
    /// <inheritdoc cref="PersistedSecret.SecretId"/>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the key identifier (<c>kid</c>) the key is selected by: the <c>kid</c> it was imported with, or its
    /// <c>SecretId</c> when it has none.
    /// </summary>
    public required string KeyId { get; init; }

    /// <inheritdoc cref="PersistedSecret.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedSecret.Use"/>
    public required string? Use { get; init; }

    /// <inheritdoc cref="PersistedSecret.Algorithm"/>
    public required string? Algorithm { get; init; }

    /// <inheritdoc cref="PersistedSecret.CreatedWhen"/>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <inheritdoc cref="PersistedSecret.ActivatesWhen"/>
    public required DateTimeOffset ActivatesWhen { get; init; }

    /// <inheritdoc cref="PersistedSecret.RetiresWhen"/>
    public required DateTimeOffset RetiresWhen { get; init; }

    /// <inheritdoc cref="PersistedSecret.ExpiresWhen"/>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <inheritdoc cref="PersistedSecret.SecretType"/>
    public required string SecretType { get; init; }

    /// <inheritdoc cref="PersistedSecret.KeySizeBits"/>
    public required int KeySizeBits { get; init; }

    /// <inheritdoc cref="PersistedSecret.ManagedBy"/>
    public required string? ManagedBy { get; init; }

    /// <summary>
    /// Gets whether the server holds only a one-way verifier of the shared secret: it authenticates a
    /// client-authentication method that compares the secret, such as <c>client_secret_basic</c>, and never one that uses
    /// the secret as a key, such as <c>client_secret_jwt</c> or an <c>HS256</c> request object. A client switched to such
    /// a method needs a new secret, issued while it is registered for that method.
    /// </summary>
    public required bool IsVerifier { get; init; }

    /// <summary>
    /// Gets the name of the remote key provider whose key management service holds the key's private half, such as
    /// <c>aws-kms</c>, or <c>null</c> for a key the server holds.
    /// </summary>
    public required string? RemoteKeyProvider { get; init; }
}
