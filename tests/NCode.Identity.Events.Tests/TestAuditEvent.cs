// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Events;

/// <summary>
/// A concrete audit event used by tests to exercise hierarchical dispatch and audit logging.
/// </summary>
internal sealed record TestAuditEvent : AuditEvent
{
    [SetsRequiredMembers]
    public TestAuditEvent()
    {
        EventSource = "test";
        Outcome = AuditOutcome.Success;
    }

    public override string EventName => "test.action";

    public override string EventCategory => "test";
}
