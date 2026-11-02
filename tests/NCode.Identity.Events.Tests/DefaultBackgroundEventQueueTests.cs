// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace NCode.Identity.Events;

public class DefaultBackgroundEventQueueTests
{
    private sealed record SampleEvent : IEvent;

    #region EnqueueAsync Tests

    [Fact]
    public async Task EnqueueAsync_ThenReaderReadsTheSameEvent()
    {
        var queue = new DefaultBackgroundEventQueue(
            NullLogger<DefaultBackgroundEventQueue>.Instance
        );

        var @event = new SampleEvent();
        await queue.EnqueueAsync(@event, CancellationToken.None);

        var read = await queue.Reader.ReadAsync(CancellationToken.None);
        Assert.Same(@event, read);
    }

    #endregion
}
