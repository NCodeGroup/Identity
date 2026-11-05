// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Represents the REST resource that describes one master key of a tenant or of the server. It carries no key
/// material, wrapped key or key check.
/// </summary>
// See ADR-1407.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EncryptionKeyResource
{
    /// <summary>
    /// Gets the identifier of the key.
    /// </summary>
    public required Guid KeyId { get; init; }

    /// <summary>
    /// Gets the version of the key within its owner, starting at one.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Gets the state of the key: one of the values in <see cref="EncryptionKeyStates"/>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Gets the instant the key was created.
    /// </summary>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <summary>
    /// Gets the instant the key was retired, or <c>null</c> while it is active.
    /// </summary>
    public DateTimeOffset? RetiredWhen { get; init; }

    /// <summary>
    /// Gets the name of the root key provider that wraps the key.
    /// </summary>
    public required string RootKeyProvider { get; init; }

    /// <summary>
    /// Gets the identifier of the root key that wraps the key.
    /// </summary>
    public required string RootKeyId { get; init; }

    /// <summary>
    /// Gets the identifier of the recovery key that also wraps the key, or <c>null</c> when no recovery copy exists.
    /// </summary>
    public string? RecoveryKeyId { get; init; }
}
