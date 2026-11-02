// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Events.Audit;
using Xunit;

namespace NCode.Identity.Events;

public class DefaultEventPublisherTests
{
    private sealed record SampleEvent(string Name) : IEvent;

    private static ServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<IEventPublisher, DefaultEventPublisher>();

        configure(serviceCollection);

        return serviceCollection.BuildServiceProvider();
    }

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_WhenHandlerRegisteredForExactType_InvokesHandler()
    {
        var order = new List<string>();
        var handler = new RecordingEventHandler<SampleEvent>("exact", order);

        using var provider = BuildProvider(serviceCollection =>
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(handler)
        );
        var publisher = provider.GetRequiredService<IEventPublisher>();

        var @event = new SampleEvent("value");
        await publisher.PublishAsync(@event, CancellationToken.None);

        var received = Assert.Single(handler.Received);
        Assert.Equal(@event, received);
    }

    [Fact]
    public async Task PublishAsync_WhenHandlerRegisteredForBaseType_InvokesViaHierarchicalDispatch()
    {
        var order = new List<string>();
        var eventHandler = new RecordingEventHandler<IEvent>("event", order);
        var auditHandler = new RecordingEventHandler<IAuditEvent>("audit", order);
        var concreteHandler = new RecordingEventHandler<TestAuditEvent>("concrete", order);

        using var provider = BuildProvider(serviceCollection =>
        {
            serviceCollection.AddSingleton<IEventHandler<IEvent>>(eventHandler);
            serviceCollection.AddSingleton<IEventHandler<IAuditEvent>>(auditHandler);
            serviceCollection.AddSingleton<IEventHandler<TestAuditEvent>>(concreteHandler);
        });
        var publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(
            new TestAuditEvent { Outcome = AuditOutcome.Success },
            CancellationToken.None
        );

        Assert.Single(eventHandler.Received);
        Assert.Single(auditHandler.Received);
        Assert.Single(concreteHandler.Received);
    }

    [Fact]
    public async Task PublishAsync_WhenMultipleHandlers_InvokesInDescendingPriorityOrder()
    {
        var order = new List<string>();
        var low = new RecordingEventHandler<SampleEvent>(
            "low",
            order,
            DefaultHandlerPriorities.Low
        );
        var high = new RecordingEventHandler<SampleEvent>(
            "high",
            order,
            DefaultHandlerPriorities.High
        );
        var middle = new RecordingEventHandler<SampleEvent>("middle", order);

        using var provider = BuildProvider(serviceCollection =>
        {
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(low);
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(high);
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(middle);
        });
        var publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new SampleEvent("value"), CancellationToken.None);

        Assert.Equal(["high", "middle", "low"], order);
    }

    [Fact]
    public async Task PublishAsync_WhenHandlerThrows_IsolatesFailureAndInvokesRemaining()
    {
        var order = new List<string>();
        var faulting = new RecordingEventHandler<SampleEvent>(
            "faulting",
            order,
            DefaultHandlerPriorities.High,
            throws: true
        );
        var healthy = new RecordingEventHandler<SampleEvent>(
            "healthy",
            order,
            DefaultHandlerPriorities.Low
        );

        using var provider = BuildProvider(serviceCollection =>
        {
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(faulting);
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(healthy);
        });
        var publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new SampleEvent("value"), CancellationToken.None);

        Assert.Equal(["faulting", "healthy"], order);
        Assert.Single(healthy.Received);
    }

    [Fact]
    public async Task PublishAsync_WhenNoSubscribers_CompletesWithoutError()
    {
        using var provider = BuildProvider(_ => { });
        var publisher = provider.GetRequiredService<IEventPublisher>();

        await publisher.PublishAsync(new SampleEvent("value"), CancellationToken.None);
    }

    #endregion

    #region HasSubscribers Tests

    [Fact]
    public void HasSubscribers_WhenHandlerRegisteredForExactType_ReturnsTrue()
    {
        var order = new List<string>();
        var handler = new RecordingEventHandler<SampleEvent>("exact", order);

        using var provider = BuildProvider(serviceCollection =>
            serviceCollection.AddSingleton<IEventHandler<SampleEvent>>(handler)
        );
        var publisher = provider.GetRequiredService<IEventPublisher>();

        Assert.True(publisher.HasSubscribers<SampleEvent>());
    }

    [Fact]
    public void HasSubscribers_WhenHandlerRegisteredForBaseType_ReturnsTrue()
    {
        var order = new List<string>();
        var auditHandler = new RecordingEventHandler<IAuditEvent>("audit", order);

        using var provider = BuildProvider(serviceCollection =>
            serviceCollection.AddSingleton<IEventHandler<IAuditEvent>>(auditHandler)
        );
        var publisher = provider.GetRequiredService<IEventPublisher>();

        Assert.True(publisher.HasSubscribers<TestAuditEvent>());
    }

    [Fact]
    public void HasSubscribers_WhenNoHandlerRegistered_ReturnsFalse()
    {
        using var provider = BuildProvider(_ => { });
        var publisher = provider.GetRequiredService<IEventPublisher>();

        Assert.False(publisher.HasSubscribers<SampleEvent>());
    }

    #endregion
}
