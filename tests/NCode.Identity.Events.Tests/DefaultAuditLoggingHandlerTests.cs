// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Audit;
using NCode.Identity.Events.Logging;
using Xunit;

namespace NCode.Identity.Events;

public class DefaultAuditLoggingHandlerTests
{
    private sealed record CriticalAuditEvent : AuditEvent
    {
        public override string EventName => "test.action";

        public override string EventCategory => "test";

        public override string Severity => AuditSeverities.Critical;
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenOutcomeSuccess_LogsAtInformationWithTheAuditRecordedEventId()
    {
        var factory = new ListLoggerFactory();
        var handler = new DefaultAuditLoggingHandler(factory);

        await handler.HandleAsync(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Success,
            },
            CancellationToken.None
        );

        var entry = Assert.Single(factory.Logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(EventIds.AuditRecorded, entry.EventId);
    }

    [Fact]
    public void Constructor_WhenCreated_UsesTheAuditCategory()
    {
        var factory = new ListLoggerFactory();

        _ = new DefaultAuditLoggingHandler(factory);

        Assert.Equal(["NCode.Identity.Audit"], factory.CategoryNames);
    }

    [Fact]
    public async Task HandleAsync_WhenOutcomeSuccess_FormatsTheMessageFromTheEventNameAndOutcome()
    {
        var factory = new ListLoggerFactory();
        var handler = new DefaultAuditLoggingHandler(factory);

        await handler.HandleAsync(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Success,
            },
            CancellationToken.None
        );

        var entry = Assert.Single(factory.Logger.Entries);
        Assert.Equal("Audit 'test.action' success.", entry.Message);
    }

    [Fact]
    public async Task HandleAsync_WhenOutcomeSuccess_CarriesTheAuditRecordAndTheOriginalFormatAsState()
    {
        var factory = new ListLoggerFactory();
        var handler = new DefaultAuditLoggingHandler(factory);

        await handler.HandleAsync(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Success,
            },
            CancellationToken.None
        );

        var entry = Assert.Single(factory.Logger.Entries);
        Assert.NotNull(entry.State);
        var eventName = Assert.Single(entry.State, x => x.Key == "audit.event_name");
        Assert.Equal("test.action", eventName.Value);
        var originalFormat = Assert.Single(entry.State, x => x.Key == "{OriginalFormat}");
        Assert.Equal(DefaultAuditLoggingHandler.MessageTemplate, originalFormat.Value);
    }

    [Fact]
    public async Task HandleAsync_WhenOutcomeFailure_LogsAtWarning()
    {
        var factory = new ListLoggerFactory();
        var handler = new DefaultAuditLoggingHandler(factory);

        await handler.HandleAsync(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Failure,
            },
            CancellationToken.None
        );

        var entry = Assert.Single(factory.Logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal(EventIds.AuditRecorded, entry.EventId);
    }

    [Fact]
    public async Task HandleAsync_WhenOutcomeDenied_LogsAtInformation()
    {
        var factory = new ListLoggerFactory();
        var handler = new DefaultAuditLoggingHandler(factory);

        await handler.HandleAsync(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Denied,
            },
            CancellationToken.None
        );

        var entry = Assert.Single(factory.Logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
    }

    [Fact]
    public async Task HandleAsync_WhenSeverityIsCritical_LogsAtCritical()
    {
        var factory = new ListLoggerFactory();
        var handler = new DefaultAuditLoggingHandler(factory);

        await handler.HandleAsync(
            new CriticalAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Success,
            },
            CancellationToken.None
        );

        var entry = Assert.Single(factory.Logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
    }

    #endregion
}
