// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Accounts.Management.Auditing;

/// <summary>
/// An audit event raised when an administrator creates, updates, disables, enables, resets the credential of, changes
/// the metadata of, or deletes a local account. The specific transition is carried by
/// <see cref="ResourceChangeAuditEvent.ChangeType"/>.
/// </summary>
[PublicAPI]
public sealed record LocalAccountChangedAuditEvent : ResourceChangeAuditEvent
{
    /// <inheritdoc />
    public override string ResourceType => AccountManagementResourceTypes.LocalAccount;
}
