// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Represents the REST resource returned when a tenant sets or changes its root key: the key as stored, the master keys
/// of the rotation onto it, and how many master keys were rewrapped under it.
/// </summary>
// See ADR-1414.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EncryptionRootKeyChangeResource
{
    /// <summary>
    /// Gets the root key as stored.
    /// </summary>
    public required EncryptionRootKeyResource RootKey { get; init; }

    /// <summary>
    /// Gets the master key that is active after the rotation onto the root key, or <c>null</c> when the rotation is left
    /// to the background validation.
    /// </summary>
    public EncryptionKeyResource? ActiveKey { get; init; }

    /// <summary>
    /// Gets the master key that the rotation retired, or <c>null</c>.
    /// </summary>
    public EncryptionKeyResource? RetiredKey { get; init; }

    /// <summary>
    /// Gets the number of master keys that were rewrapped under the root key.
    /// </summary>
    public required int RewrappedKeys { get; init; }

    /// <summary>
    /// Gets the number of master keys that could not be rewrapped.
    /// </summary>
    public required int FailedKeys { get; init; }
}
