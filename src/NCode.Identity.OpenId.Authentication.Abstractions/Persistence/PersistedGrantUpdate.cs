// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Contains the change that a read-modify-write of a persisted grant applies to the grant as it is stored.
/// </summary>
/// <typeparam name="TPayload">The type of the payload for the grant.</typeparam>
[PublicAPI]
public readonly record struct PersistedGrantUpdate<TPayload>
{
    /// <summary>
    /// Gets the payload to persist for the grant.
    /// </summary>
    public required TPayload Payload { get; init; }

    /// <summary>
    /// Gets the time when the grant expires after the change, or <see langword="null"/> to keep the expiration of the
    /// grant as it is stored.
    /// </summary>
    public DateTimeOffset? ExpiresWhen { get; init; }
}
