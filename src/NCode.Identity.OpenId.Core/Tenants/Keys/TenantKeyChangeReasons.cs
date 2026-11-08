// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Contains constants for the known reasons the server changes a tenant's keys on its own, recorded as the
/// <c>reason</c> of the audit event that records the change. The reasons are names, compared ordinally.
/// </summary>
// See ADR-1404.
[PublicAPI]
public static class TenantKeyChangeReasons
{
    /// <summary>
    /// No key of the algorithm signed or encrypted: the tenant's first key for it, or a replacement of one that was
    /// deleted, is generated and activates at once, or a published key activates ahead of time.
    /// </summary>
    public const string NoActiveKey = "no_active_key";

    /// <summary>
    /// The active key nears its retirement: its successor is generated and published ahead of the hand-over.
    /// </summary>
    public const string Successor = "successor";

    /// <summary>
    /// The tenant's settings do not use the key's algorithm.
    /// </summary>
    public const string UnusedAlgorithm = "unused_algorithm";

    /// <summary>
    /// The tenant's settings choose another custody for the key: its replacement, held by the new custody, is generated
    /// to activate after the activation delay, and the key retires then.
    /// </summary>
    public const string CustodyChanged = "custody_changed";

    /// <summary>
    /// The retirement grace, raised by the tenant's settings or by a longer token lifetime, reaches past the key's
    /// expiry, so the expiry moves to the end of the grace and no token the key protected outlives it.
    /// </summary>
    public const string RetirementGraceRaised = "retirement_grace_raised";

    /// <summary>
    /// The key has expired, so it neither verifies nor decrypts and is not published.
    /// </summary>
    public const string Expired = "expired";
}
