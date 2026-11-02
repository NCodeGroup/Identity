// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// Defines the catalogued event names (<c>event_name</c>) of the management API's grant and session audit events.
/// </summary>
/// <remarks>
/// This is the set of names the management API emits today, not a closed list: an extension names its own audit events
/// without editing this class. The names of resource-change events are built from
/// <see cref="ManagementResourceTypes"/> and the change type.
/// </remarks>
[PublicAPI]
public static class ManagementAuditEventNames
{
    /// <summary>
    /// An administrator revoked a single grant.
    /// </summary>
    public const string GrantRevoked = "grant.revoked";

    /// <summary>
    /// An administrator bulk-revoked grants by subject, client, and/or session filter.
    /// </summary>
    public const string GrantsRevokedBulk = "grant.revoked.bulk";

    /// <summary>
    /// An administrator revoked end-user sessions.
    /// </summary>
    public const string SessionsRevoked = "session.revoked.admin";
}
