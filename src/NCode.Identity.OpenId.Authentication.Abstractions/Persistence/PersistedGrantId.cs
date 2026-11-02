// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Contains the identifiers of a persisted grant.
/// </summary>
[PublicAPI]
public readonly record struct PersistedGrantId
{
    /// <summary>
    /// Gets the tenant identifier that scopes the grant.
    /// </summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Gets the type of the grant.
    /// </summary>
    public required string GrantType { get; init; }

    /// <summary>
    /// Gets the unique identifier of the grant.
    /// This value is never persisted to storage as-is, instead it's hash is used as the key.
    /// </summary>
    public required string GrantKey { get; init; }
}
