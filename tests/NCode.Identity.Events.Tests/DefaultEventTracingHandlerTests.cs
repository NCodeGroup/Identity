#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
    public async Task HandleAsync_WhenCurrentActivity_AddsAuditEventWithEnvelopeTags()
    {
        using var source = new ActivitySource(nameof(DefaultEventTracingHandlerTests));
        using var listener = CreateAllDataListener(nameof(DefaultEventTracingHandlerTests));
        using var activity = source.StartActivity("operation");
        Assert.NotNull(activity);

        var handler = new DefaultEventTracingHandler();

        await handler.HandleAsync(
            new TestAuditEvent { Outcome = AuditOutcome.Failure, SubjectId = "subject-1" },
            CancellationToken.None
        );

        var activityEvent = Assert.Single(activity.Events);
        Assert.Equal("test.action", activityEvent.Name);

        var tags = activityEvent.Tags.ToDictionary(tag => tag.Key, tag => tag.Value);
        Assert.Equal("test.action", tags["audit.action"]);
        Assert.Equal(AuditOutcome.Failure, tags["audit.outcome"]);
        Assert.Equal("subject-1", tags["audit.subject_id"]);
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
