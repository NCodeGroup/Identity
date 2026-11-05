// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator updates the platform's settings. The specific transition is carried by
/// <see cref="ResourceChangeAuditEvent.ChangeType"/>.
/// </summary>
/// <remarks>
/// The platform is the one deployment root, so <see cref="IResourceChangeAuditEvent.ResourceId"/> is always
/// <c>platform</c>; <see cref="IAuditEvent.TenantId"/> is the tenant the request runs
/// under.
/// </remarks>
[PublicAPI]
public sealed record PlatformChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.Platform;
}
