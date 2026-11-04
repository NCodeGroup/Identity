// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Contains constants for the known changes the server makes to a tenant's keys on its own, each the last segment of
/// the audit event that records it (<c>tenant.key.generated</c> and so on). The changes are names, compared
/// ordinally.
/// </summary>
// See ADR-1404.
[PublicAPI]
public static class TenantKeyChanges
{
    /// <summary>
    /// The server generated a key: the tenant's first key for an algorithm it uses, or the successor of a key that
    /// nears its retirement.
    /// </summary>
    public const string Generated = "generated";

    /// <summary>
    /// The server activated a published key ahead of the instant it was to activate at, because no key of its
    /// algorithm signed or encrypted.
    /// </summary>
    public const string Activated = "activated";

    /// <summary>
    /// The server retired a key: it stops signing or encrypting and expires at the end of the retirement grace.
    /// </summary>
    public const string Retired = "retired";

    /// <summary>
    /// The server moved a key's expiry later, because the retirement grace that covers the tokens it protected was
    /// raised after the key was generated.
    /// </summary>
    public const string Extended = "extended";

    /// <summary>
    /// The server removed an expired key it provisioned.
    /// </summary>
    public const string Removed = "removed";
}
