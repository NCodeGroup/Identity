// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the metadata used to import a caller-supplied shared secret (for example an OAuth <c>client_secret</c>)
/// as a new <see cref="PersistedSecret"/>. The secret value itself is supplied separately to
/// <see cref="ISecretGenerator.ImportSharedSecret(ISecretProtector, ImportSharedSecretRequest, ReadOnlySpan{byte})"/>
/// as the UTF-8 octets of the opaque string, and its bit length is derived from those octets, so this request carries
/// only the descriptive metadata.
/// </summary>
// See ADR-1110.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ImportSharedSecretRequest
{
    /// <summary>
    /// Gets the natural identifier to assign to the imported secret.
    /// </summary>
    public required string SecretId { get; init; }

    /// <summary>
    /// Gets the intended use for the imported secret, or <c>null</c> for any compatible use.
    /// See <c>SecretKeyUses</c> for possible values.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the imported secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public required string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret is considered created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the imported secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
