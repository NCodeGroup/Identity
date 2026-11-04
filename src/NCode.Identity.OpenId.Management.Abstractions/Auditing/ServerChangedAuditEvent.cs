// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes an OpenID server, or
/// updates its settings. The specific transition is carried by
/// <see cref="ResourceChangeAuditEvent.ChangeType"/>.
/// </summary>
/// <remarks>
/// Servers are the <c>GlobalAdmin</c>-only control plane and have no owning tenant, so
/// <see cref="IAuditEvent.TenantId"/> is not populated for this event; the affected server's identifier
/// is carried by <see cref="IResourceChangeAuditEvent.ResourceId"/>.
/// </remarks>
[PublicAPI]
public sealed record ServerChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.Platform;
}
