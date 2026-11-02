// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator blocks or unblocks an end user. Both are recorded as an update of the
/// principal, and the resource values carry whether it is blocked; the owning tenant is carried by
/// <see cref="IAuditEvent.TenantId"/>.
/// </summary>
[PublicAPI]
public sealed record PrincipalChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.Principal;
}
