// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes a secret belonging to an
/// OpenID client. The owning client is carried by <see cref="IAuditEvent.ClientId"/> and the secret's
/// identifier by <see cref="IResourceChangeAuditEvent.ResourceId"/>; the event never carries secret
/// material.
/// </summary>
[PublicAPI]
public sealed record ClientSecretChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.ClientSecret;

    /// <inheritdoc />
    /// <remarks>
    /// Lists the owning client when <see cref="IAuditEvent.ClientId"/> is set.
    /// </remarks>
    protected override IEnumerable<AuditResource> ParentResources =>
        ClientId is null ? [] : [new AuditResource(ManagementResourceTypes.Client, ClientId)];
}
