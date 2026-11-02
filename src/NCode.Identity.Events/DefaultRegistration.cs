// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Events.Audit;
using NCode.Registration;

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
        /// Registers the default <see cref="IEventPublisher"/> and the built-in subscribers, minting the
        /// <see cref="EventsLibrary"/> marker that the event opt-ins require.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> for the event library so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<EventsLibrary> AddEventServices()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            var newBuilder = serviceCollection.NewBuilder<EventsLibrary>();

            // The metrics subscriber creates its meter through the IMeterFactory (ADR-1197).
            serviceCollection.AddMetrics();

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

            return newBuilder;
        }
    }
}
