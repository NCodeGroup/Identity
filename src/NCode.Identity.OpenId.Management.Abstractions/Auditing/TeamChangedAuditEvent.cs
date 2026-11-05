// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, or deletes a team, or updates its settings. The
/// specific transition is carried by <see cref="ResourceChangeAuditEvent.ChangeType"/>.
/// </summary>
// See ADR-1411
[PublicAPI]
public sealed record TeamChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => ManagementResourceTypes.Team;
}
