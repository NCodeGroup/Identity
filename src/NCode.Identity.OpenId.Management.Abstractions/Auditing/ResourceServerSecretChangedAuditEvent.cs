// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes a secret belonging to a
/// resource server. The owning resource server is carried by <see cref="ResourceServerId"/>, the owning
/// tenant by <see cref="IAuditEvent.TenantId"/>, and the secret's identifier by
/// <see cref="IResourceChangeAuditEvent.ResourceId"/>; the event never carries secret material.
/// </summary>
[PublicAPI]
public sealed record ResourceServerSecretChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <summary>
    /// Gets the identifier of the resource server the secret belongs to.
    /// </summary>
    public required string ResourceServerId { get; init; }

    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.ResourceServerSecret;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the owning resource server.
    /// </remarks>
    protected override IEnumerable<AuditResource> ParentResources =>
        [new AuditResource(ManagementResourceTypes.ResourceServer, ResourceServerId)];
}
