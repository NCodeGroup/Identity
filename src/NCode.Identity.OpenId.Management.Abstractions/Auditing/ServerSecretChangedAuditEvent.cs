// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes a secret belonging to an
/// OpenID server. The owning server is carried by <see cref="PlatformId"/> and the secret's identifier by
/// <see cref="IResourceChangeAuditEvent.ResourceId"/>; the event never carries secret material.
/// </summary>
/// <remarks>
/// Servers are the <c>GlobalAdmin</c>-only control plane and have no owning tenant, so
/// <see cref="IAuditEvent.TenantId"/> is not populated for this event.
/// </remarks>
[PublicAPI]
public sealed record ServerSecretChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <summary>
    /// Gets the identifier of the server the secret belongs to.
    /// </summary>
    public required string PlatformId { get; init; }

    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.ServerSecret;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the owning server.
    /// </remarks>
    protected override IEnumerable<AuditResource> ParentResources =>
        [new AuditResource(ManagementResourceTypes.Platform, PlatformId)];
}
