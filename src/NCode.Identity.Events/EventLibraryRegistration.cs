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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCode.Registration;

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
