// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration;

namespace NCode.Identity.Events;

/// <summary>
/// Provides opt-in extension methods for the <see cref="EventsLibrary"/> builder, so handler and
/// background-delivery registrations cannot be added without first calling <c>AddEventServices</c>.
/// </summary>
[PublicAPI]
public static class EventLibraryRegistration
{
    /// <param name="builder">The event-library builder returned by <c>AddEventServices</c>.</param>
    extension(IServiceBuilder<EventsLibrary> builder)
    {
        /// <summary>
        /// Registers an <see cref="IEventHandler{TEvent}"/> subscriber for <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The type of event the handler observes.</typeparam>
        /// <typeparam name="THandler">The concrete handler implementation to register.</typeparam>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<EventsLibrary> AddEventHandler<TEvent, THandler>()
            where TEvent : IEvent
            where THandler : class, IEventHandler<TEvent>
        {
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped<IEventHandler<TEvent>, THandler>()
            );

            return builder;
        }

        /// <summary>
        /// Enables background delivery so handlers that implement <see cref="ISupportBackgroundDelivery"/>
        /// are invoked on a background worker instead of inline on the publishing path.
        /// </summary>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<EventsLibrary> AddBackgroundEventDelivery()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<DefaultBackgroundEventQueue>();
            serviceCollection.TryAddSingleton<IBackgroundEventQueue>(serviceProvider =>
                serviceProvider.GetRequiredService<DefaultBackgroundEventQueue>()
            );

            serviceCollection.AddHostedService<BackgroundEventDispatcher>();

            return builder;
        }
    }
}
