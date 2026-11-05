// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Events.Audit;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Creates the <see cref="RootKeyAuditEvent"/> that every writer of the audit trail builds for a change to an owner
/// root key, so the writers agree on the event's shape.
/// </summary>
// See ADR-1414.
[PublicAPI]
public static class RootKeyAuditEvents
{
    /// <summary>
    /// Creates the audit event for a change to an owner root key.
    /// </summary>
    /// <param name="rootKey">The root key after the change.</param>
    /// <param name="change">The change: one of the values in <see cref="RootKeyChanges"/>.</param>
    /// <param name="reason">Why the change is made: one of the values in <see cref="RootKeyChangeReasons"/>.</param>
    /// <param name="eventSource">The component that records the event.</param>
    /// <param name="actor">The actor that made the change.</param>
    /// <param name="now">The time of the change, which is also the time that the event identifier encodes.</param>
    /// <returns>
    /// The event. Its outcome is <see cref="AuditOutcome.Failure"/>, and it carries the failure text of the key, only
    /// when the change is <see cref="RootKeyChanges.Unavailable"/>.
    /// </returns>
    public static RootKeyAuditEvent Create(
        OwnerRootKeyInfo rootKey,
        string change,
        string reason,
        string eventSource,
        AuditActor actor,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(rootKey);
        ArgumentException.ThrowIfNullOrEmpty(change);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ArgumentException.ThrowIfNullOrEmpty(eventSource);
        ArgumentNullException.ThrowIfNull(actor);

        var failed = string.Equals(change, RootKeyChanges.Unavailable, StringComparison.Ordinal);

        return new RootKeyAuditEvent
        {
            EventId = Guid.CreateVersion7(now),
            EventTime = now,
            EventSource = eventSource,
            TenantId = rootKey.Owner.TenantId,
            Actor = actor,
            Outcome = failed ? AuditOutcome.Failure : AuditOutcome.Success,
            Change = change,
            Reason = reason,
            OwnerType = rootKey.Owner.OwnerType,
            OwnerId = rootKey.Owner.OwnerId,
            KeyWrapperName = rootKey.KeyWrapperName,
            KeyReference = rootKey.KeyReference,
            State = rootKey.State,
            Failure = failed ? rootKey.Failure : null,
        };
    }
}
