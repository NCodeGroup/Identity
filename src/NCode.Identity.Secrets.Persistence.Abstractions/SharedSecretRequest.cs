// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the metadata of a shared secret (for example an OAuth <c>client_secret</c>) that
/// <see cref="ISecretGenerator"/> stores as a new <see cref="PersistedSecret"/>, whether the server generates the secret
/// or a caller supplies it. A shared secret is always a <c>symmetric</c> secret whose size is that of its UTF-8 octets.
/// </summary>
// See ADR-1391.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SharedSecretRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the intended use for the secret, or <c>null</c> for any compatible use.
    /// See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret expires.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <summary>
    /// Gets the random entropy, in bits, of a secret the server generates: a multiple of 8 that is at least
    /// <see cref="SharedSecretPolicy.MinimumEntropyBits"/>, which is the default. A supplied secret's size is that of
    /// its value.
    /// </summary>
    public int EntropyBits { get; init; } = SharedSecretPolicy.MinimumEntropyBits;
}
