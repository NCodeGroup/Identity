// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace NCode.Identity.Events;

public class BackgroundEventDeliveryTests
{
    private sealed record SampleEvent(string Name) : IEvent;

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<DefaultEventPublisher>();
        serviceCollection.AddSingleton<IEventPublisher>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultEventPublisher>()
        );

        configure(serviceCollection);

        return serviceCollection.BuildServiceProvider();
    }

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_WhenBackgroundHandlerAndQueuePresent_DefersToQueueAndRunsInlineHandlers()
    {
        var order = new List<string>();
        var inline = new RecordingEventHandler<SampleEvent>("inline", order);
        var background = new BackgroundRecordingEventHandler<SampleEvent>("background", order);
        var queue = new FakeBackgroundEventQueue();

        using var provider = BuildProvider(serviceCollection =>
        {
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(inline);
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(background);
            serviceCollection.AddSingleton<IBackgroundEventQueue>(queue);
        });
        var publisher = provider.GetRequiredService<IEventPublisher>();

        var @event = new SampleEvent("value");
        await publisher.PublishAsync(@event, CancellationToken.None);

        Assert.Equal(["inline"], order);
        Assert.Empty(background.Received);
        Assert.Equal(@event, Assert.Single(queue.Enqueued));
    }

    [Fact]
    public async Task PublishAsync_WhenBackgroundHandlerButNoQueue_RunsHandlerInline()
    {
        var order = new List<string>();
        var background = new BackgroundRecordingEventHandler<SampleEvent>("background", order);

        using var provider = BuildProvider(serviceCollection =>
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(background)
        );
        var publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new SampleEvent("value"), CancellationToken.None);

        Assert.Equal(["background"], order);
        Assert.Single(background.Received);
    }

    #endregion

    #region DispatchBackgroundAsync Tests

    [Fact]
    public async Task DispatchBackgroundAsync_InvokesOnlyBackgroundHandlers()
    {
        var order = new List<string>();
        var inline = new RecordingEventHandler<SampleEvent>("inline", order);
        var background = new BackgroundRecordingEventHandler<SampleEvent>("background", order);

        using var provider = BuildProvider(serviceCollection =>
        {
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(inline);
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(background);
        });
        var publisher = provider.GetRequiredService<DefaultEventPublisher>();

        await publisher.DispatchBackgroundAsync(new SampleEvent("value"), CancellationToken.None);

        Assert.Equal(["background"], order);
        Assert.Empty(inline.Received);
        Assert.Single(background.Received);
    }

    #endregion
}
