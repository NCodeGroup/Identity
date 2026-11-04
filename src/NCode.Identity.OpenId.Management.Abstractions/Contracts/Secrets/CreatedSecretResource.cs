// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the REST resource returned when a client secret is created. It carries the same metadata as
/// <see cref="SecretResource"/> plus the one-time unprotected key material. The material is revealed only in
/// this create response and is never returned by any read path.
/// </summary>
// See ADR-1050.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreatedSecretResource
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
    /// Gets the one-time unprotected key material, base64url-encoded, or <c>null</c> when the secret has no
    /// externally-held material to reveal (asymmetric secrets). This value appears only in this create response
    /// and is never persisted, logged, audited, or returned by any read path.
    /// </summary>
    public required string? SecretMaterial { get; init; }
}
