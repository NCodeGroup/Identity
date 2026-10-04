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
using NCode.Identity.Events.Audit;

namespace NCode.Identity.Events;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the event publishing
/// services and event handlers.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the default <see cref="IEventPublisher"/> and the built-in logging subscriber into
        /// the provided <see cref="IServiceCollection"/> instance.
        /// </summary>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> instance so that calls can be chained.
        /// </returns>
        [PublicAPI]
        public IServiceCollection AddEventServices()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            // Register the concrete publisher so the background dispatcher can reach its internal
            // background-dispatch path, and expose it through the IEventPublisher contract.
            serviceCollection.TryAddSingleton<DefaultEventPublisher>();
            serviceCollection.TryAddSingleton<IEventPublisher>(serviceProvider =>
                serviceProvider.GetRequiredService<DefaultEventPublisher>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventHandler<IEvent>, DefaultEventLoggingHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventHandler<IEvent>, DefaultEventMetricsHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventHandler<IEvent>, DefaultEventTracingHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IEventHandler<IAuditEvent>,
                    DefaultAuditLoggingHandler
                >()
            );

            return serviceCollection;
        }

        /// <summary>
        /// Registers an <see cref="IEventHandler{TEvent}"/> subscriber for <typeparamref name="TEvent"/>.
        /// </summary>
        /// <typeparam name="TEvent">The type of event the handler observes.</typeparam>
        /// <typeparam name="THandler">The concrete handler implementation to register.</typeparam>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> instance so that calls can be chained.
        /// </returns>
        [PublicAPI]
        public IServiceCollection AddEventHandler<TEvent, THandler>()
            where TEvent : IEvent
            where THandler : class, IEventHandler<TEvent>
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Scoped<IEventHandler<TEvent>, THandler>()
            );

            return serviceCollection;
        }

        /// <summary>
        /// Enables background delivery so handlers that implement <see cref="ISupportBackgroundDelivery"/>
        /// are invoked on a background worker instead of inline on the publishing path.
        /// </summary>
        /// <returns>
        /// The same <see cref="IServiceCollection"/> instance so that calls can be chained.
        /// </returns>
        [PublicAPI]
        public IServiceCollection AddBackgroundEventDelivery()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            serviceCollection.TryAddSingleton<DefaultBackgroundEventQueue>();
            serviceCollection.TryAddSingleton<IBackgroundEventQueue>(serviceProvider =>
                serviceProvider.GetRequiredService<DefaultBackgroundEventQueue>()
            );

            serviceCollection.AddHostedService<BackgroundEventDispatcher>();

            return serviceCollection;
        }
    }
}
