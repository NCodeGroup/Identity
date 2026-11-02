// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Authentication.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public class AuditRecordHandlerTests
{
    private sealed record TestAuditEvent : AuditEvent
    {
        public override string EventName => "token.issued";

        public override string EventCategory => AuditEventCategories.Token;
    }

    private static TestAuditEvent CreateEvent() =>
        new()
        {
            EventId = Guid.CreateVersion7(),
            EventTime = DateTimeOffset.UnixEpoch,
            EventSource = AuditEventSources.OpenId,
            Outcome = AuditOutcome.Success,
        };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenEnabled_QueuesTheEventsRecord()
    {
        var options = Options.Create(new AuditRecordOptions());
        var queue = new AuditRecordQueue(options);
        var handler = new AuditRecordHandler(options, queue);
        var auditEvent = CreateEvent();

        await handler.HandleAsync(auditEvent, CancellationToken.None);

        var drained =
            new List<NCode.Identity.OpenId.Persistence.DataContracts.PersistedAuditRecord>();
        queue.Drain(drained, limit: 10);
        Assert.Equal(auditEvent.EventId, Assert.Single(drained).EventId);
    }

    [Fact]
    public async Task HandleAsync_WhenDisabled_QueuesNothing()
    {
        var options = Options.Create(new AuditRecordOptions { Enabled = false });
        var queue = new AuditRecordQueue(options);
        var handler = new AuditRecordHandler(options, queue);

        await handler.HandleAsync(CreateEvent(), CancellationToken.None);

        Assert.Equal(0, queue.Count);
    }

    #endregion
}
