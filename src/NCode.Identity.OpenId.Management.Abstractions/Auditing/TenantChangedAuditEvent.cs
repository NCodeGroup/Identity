// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes an OpenID tenant, or
/// updates its settings. The specific transition is carried by
/// <see cref="ResourceChangeAuditEvent.ChangeType"/>.
/// </summary>
[PublicAPI]
public sealed record TenantChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.Tenant;

    /// <summary>
    /// Gets the identifier of the team the tenant belongs to.
    /// </summary>
    public required string TeamId { get; init; }

    /// <inheritdoc />
    /// <remarks>
    /// Lists the team the tenant belongs to.
    /// </remarks>
    protected override IEnumerable<AuditResource> ParentResources =>
        [new AuditResource(ManagementResourceTypes.Team, TeamId)];
}
