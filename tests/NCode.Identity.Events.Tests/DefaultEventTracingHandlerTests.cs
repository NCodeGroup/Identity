// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Events;

public sealed class DefaultEventTracingHandlerTests
{
    private sealed record SampleEvent : IEvent;

    private static ActivityListener CreateAllDataListener(string sourceName)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllData,
            SampleUsingParentId = (ref ActivityCreationOptions<string> _) =>
                ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenCurrentActivity_AddsAuditEventWithAuditRecordTags()
    {
        using var source = new ActivitySource(nameof(DefaultEventTracingHandlerTests));
        using var listener = CreateAllDataListener(nameof(DefaultEventTracingHandlerTests));
        using var activity = source.StartActivity("operation");
        Assert.NotNull(activity);

        var handler = new DefaultEventTracingHandler();

        await handler.HandleAsync(
            new TestAuditEvent
            {
                EventSource = AuditEventSources.OpenId,
                Outcome = AuditOutcome.Failure,
                SubjectId = "subject-1",
            },
            CancellationToken.None
        );

        var activityEvent = Assert.Single(activity.Events);
        Assert.Equal("test.action", activityEvent.Name);

        var tags = activityEvent.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
        Assert.Equal("test.action", tags["audit.event_name"]);
        Assert.Equal(AuditOutcome.Failure, tags["audit.outcome"]);
        Assert.Equal("subject-1", tags["audit.subject_id"]);
        Assert.Equal("anonymous", tags["audit.actor.type"]);
        Assert.False(tags.ContainsKey("audit.action"));
    }

    [Fact]
    public async Task HandleAsync_WhenNoCurrentActivity_DoesNothing()
    {
        Assert.Null(Activity.Current);

        var handler = new DefaultEventTracingHandler();

        await handler.HandleAsync(new SampleEvent(), CancellationToken.None);
    }

    #endregion
}
