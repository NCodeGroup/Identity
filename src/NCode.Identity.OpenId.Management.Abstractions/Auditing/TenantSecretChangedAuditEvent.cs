// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes a secret belonging to an
/// OpenID tenant. The owning tenant is carried by <see cref="IAuditEvent.TenantId"/> and the secret's
/// identifier by <see cref="IResourceChangeAuditEvent.ResourceId"/>; the event never carries secret
/// material.
/// </summary>
[PublicAPI]
public sealed record TenantSecretChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.TenantSecret;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the owning tenant when <see cref="IAuditEvent.TenantId"/> is set.
    /// </remarks>
    protected override IEnumerable<AuditResource> ParentResources =>
        TenantId is null ? [] : [new AuditResource(ManagementResourceTypes.Tenant, TenantId)];
}
