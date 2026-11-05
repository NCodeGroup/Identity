// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.EncryptionKeys;

/// <summary>
/// Represents the REST resource that lists the master keys of a tenant or of the platform, newest version first.
/// </summary>
// See ADR-1407.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EncryptionKeysResource
{
    /// <summary>
    /// Gets the keys, newest version first.
    /// </summary>
    public required List<EncryptionKeyResource> Keys { get; init; }

    /// <summary>
    /// Gets a value indicating whether new keys are also wrapped under a recovery key.
    /// </summary>
    public required bool RecoveryConfigured { get; init; }

    /// <summary>
    /// Gets the root key the tenant brings, or <c>null</c> when the deployment's root key wraps the tenant's keys.
    /// </summary>
    // See ADR-1414.
    public EncryptionRootKeyResource? RootKey { get; init; }

    /// <summary>
    /// Gets the names of the key wrappers that a tenant may choose for its root key. It is empty when the deployment
    /// lets no tenant bring a key, and for the server.
    /// </summary>
    // See ADR-1414.
    public required List<string> RootKeyProviders { get; init; }

    /// <summary>
    /// Gets the value that an identity the tenant grants can require of the server, such as the external ID of an AWS
    /// role's trust policy; <c>null</c> when the owner cannot bring a key.
    /// </summary>
    // See ADR-1429.
    public string? RootKeyAccessBinding { get; init; }
}
