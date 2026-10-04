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

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Logging;

namespace NCode.Identity.Events;

/// <summary>
/// Provides the default implementation of <see cref="IEventPublisher"/> that dispatches an event to
/// every handler registered for its runtime type and for any event type it derives from or implements,
/// ordered by priority and invoked with fault isolation.
/// </summary>
/// <remarks>
/// Hierarchical dispatch resolves handlers via reflection (<see cref="Type.MakeGenericType"/>), which
/// is not compatible with ahead-of-time compilation or trimming; see ADR-0047.
/// </remarks>
internal class DefaultEventPublisher(
    IServiceScopeFactory serviceScopeFactory,
    IServiceProviderIsService serviceProviderIsService,
    IEnumerable<IBackgroundEventQueue> backgroundEventQueues,
    ILogger<DefaultEventPublisher> logger
) : IEventPublisher
{
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private IServiceProviderIsService ServiceProviderIsService { get; } = serviceProviderIsService;
    private ILogger<DefaultEventPublisher> Logger { get; } = logger;

    // Optional: present only when background delivery has been enabled. When absent, handlers marked
    // for background delivery run inline (graceful degradation).
    private IBackgroundEventQueue? BackgroundEventQueue { get; } =
        backgroundEventQueues.FirstOrDefault();

    // Cached per runtime event type: the closed IEventHandler<> service types to resolve plus the
    // delegate that invokes each. Only the type-plan is cached; handler instances are resolved per
    // publish from a fresh scope so scoped dependencies bind correctly.
    private ConcurrentDictionary<Type, EventHandlerBinding[]> PlanCache { get; } = new();

    private static ConcurrentDictionary<Type, EventHandlerInvoker> InvokerCache { get; } = new();

    /// <inheritdoc />
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var plan = GetPlan(@event.GetType());
        if (plan.Length == 0)
            return ValueTask.CompletedTask;

        return PublishCoreAsync(@event, plan, cancellationToken);
    }

    private async ValueTask PublishCoreAsync(
        IEvent @event,
        EventHandlerBinding[] plan,
        CancellationToken cancellationToken
    )
    {
        // Background and singleton callers alike get a correctly-scoped dependency graph.
        using var scope = ServiceScopeFactory.CreateScope();
        var subscribers = ResolveSubscribers(scope.ServiceProvider, plan);

        var queue = BackgroundEventQueue;
        var hasBackground = false;

        foreach (var (handler, invoker, _) in subscribers)
        {
            if (queue is not null && handler is ISupportBackgroundDelivery)
            {
                hasBackground = true;
                continue;
            }

            await InvokeSubscriberAsync(handler, invoker, @event, cancellationToken);
        }

        if (hasBackground && queue is not null)
        {
            await queue.EnqueueAsync(@event, cancellationToken);
        }
    }

    // Invokes the handlers marked for background delivery, in a fresh scope, outside the request.
    internal async ValueTask DispatchBackgroundAsync(
        IEvent @event,
        CancellationToken cancellationToken
    )
    {
        var plan = GetPlan(@event.GetType());
        if (plan.Length == 0)
            return;

        using var scope = ServiceScopeFactory.CreateScope();
        var subscribers = ResolveSubscribers(scope.ServiceProvider, plan);

        foreach (var (handler, invoker, _) in subscribers)
        {
            if (handler is ISupportBackgroundDelivery)
            {
                await InvokeSubscriberAsync(handler, invoker, @event, cancellationToken);
            }
        }
    }

    // Resolves the handler instances for the plan from the given scope, ordered by descending priority.
    private static List<(
        object Handler,
        EventHandlerInvoker Invoker,
        int Priority
    )> ResolveSubscribers(IServiceProvider provider, EventHandlerBinding[] plan)
    {
        var subscribers = new List<(object Handler, EventHandlerInvoker Invoker, int Priority)>();
        foreach (var (handlerServiceType, invoker) in plan)
        {
            foreach (var handler in provider.GetServices(handlerServiceType))
            {
                if (handler is null)
                    continue;

                var priority = handler is ISupportHandlerPriority support
                    ? support.HandlerPriority
                    : 0;
                subscribers.Add((handler, invoker, priority));
            }
        }

        return [.. subscribers.OrderByDescending(item => item.Priority)];
    }

    private async ValueTask InvokeSubscriberAsync(
        object handler,
        EventHandlerInvoker invoker,
        IEvent @event,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await invoker(handler, @event, cancellationToken);
        }
        catch (Exception exception)
        {
            Logger.EventHandlerFailed(
                handler.GetType().FullName ?? handler.GetType().Name,
                @event.GetType().FullName ?? @event.GetType().Name,
                exception
            );
        }
    }

    /// <inheritdoc />
    public bool HasSubscribers<TEvent>()
        where TEvent : IEvent
    {
        var plan = GetPlan(typeof(TEvent));
        foreach (var (handlerServiceType, _) in plan)
        {
            if (ServiceProviderIsService.IsService(handlerServiceType))
                return true;
        }

        return false;
    }

    [RequiresDynamicCode(
        "Hierarchical event dispatch closes IEventHandler<> over the event type hierarchy via reflection."
    )]
    [RequiresUnreferencedCode(
        "Hierarchical event dispatch resolves handler types that trimming cannot statically discover."
    )]
    private EventHandlerBinding[] GetPlan(Type eventType) =>
        PlanCache.GetOrAdd(
            eventType,
            static type =>
                EventTypeHierarchy(type)
                    .Select(static eventContract => new EventHandlerBinding(
                        typeof(IEventHandler<>).MakeGenericType(eventContract),
                        GetInvoker(eventContract)
                    ))
                    .ToArray()
        );

    // Every type in the event's hierarchy that is itself an event contract: the runtime type, its base
    // types, and its implemented interfaces, filtered to those assignable to IEvent.
    private static HashSet<Type> EventTypeHierarchy(Type eventType)
    {
        var contracts = new HashSet<Type>();

        for (var current = eventType; current is not null; current = current.BaseType)
        {
            if (typeof(IEvent).IsAssignableFrom(current))
                contracts.Add(current);
        }

        foreach (var contract in eventType.GetInterfaces())
        {
            if (typeof(IEvent).IsAssignableFrom(contract))
                contracts.Add(contract);
        }

        return contracts;
    }

    [RequiresDynamicCode(
        "Building the handler invoker closes a generic method over the event type via reflection."
    )]
    private static EventHandlerInvoker GetInvoker(Type eventContract) =>
        InvokerCache.GetOrAdd(
            eventContract,
            static type =>
            {
                var openMethod =
                    typeof(DefaultEventPublisher).GetMethod(
                        nameof(InvokeHandlerAsync),
                        BindingFlags.NonPublic | BindingFlags.Static
                    )
                    ?? throw new InvalidOperationException(
                        "Unable to locate the event handler invoker method."
                    );

                return openMethod.MakeGenericMethod(type).CreateDelegate<EventHandlerInvoker>();
            }
        );

    private static ValueTask InvokeHandlerAsync<TEvent>(
        object handler,
        IEvent @event,
        CancellationToken cancellationToken
    )
        where TEvent : IEvent =>
        ((IEventHandler<TEvent>)handler).HandleAsync((TEvent)@event, cancellationToken);
}
