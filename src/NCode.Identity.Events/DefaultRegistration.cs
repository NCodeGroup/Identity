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

            serviceCollection.TryAddSingleton<IEventPublisher, DefaultEventPublisher>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEventHandler<IEvent>, DefaultEventLoggingHandler>()
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
    }
}
