// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Specifies the status of a persisted grant.
/// </summary>
/// <remarks>
/// The status is never stored. <see cref="IPersistedGrantService"/> derives it on every read from the grant's own
/// revocation and expiry instants, so the members are the outcome set of that library-owned operation, and the set is
/// closed. A grant type that has its own lifecycle, such as a device code or a CIBA request that is
/// pending, approved, or denied, keeps that state in its payload, not here. A new member would need a new instant on
/// the stored grant and a change to the service that derives the status.
/// </remarks>
// See ADR-1150.
[PublicAPI]
public enum PersistedGrantStatus
{
    /// <summary>
    /// The grant is active.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The grant has expired.
    /// </summary>
    Expired = 1,

    /// <summary>
    /// The grant has been revoked.
    /// </summary>
    Revoked = 2,
}
