// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Registration;

/// <summary>
/// Provides various extension methods for <see cref="IServiceCollection"/>.
/// </summary>
[PublicAPI]
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Provides extension methods for <see cref="IServiceCollection"/> to manage registration markers.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to extend.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Determines whether services for <typeparamref name="TMarker"/> are already registered in the
        /// <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TMarker">The type that discriminates the marker interface.</typeparam>
        /// <returns><c>true</c> if the marker is registered; otherwise, <c>false</c>.</returns>
        [PublicAPI]
        public bool IsRegistered<TMarker>()
            where TMarker : IRegistrationMarker<TMarker>, new()
        {
            return serviceCollection.Any(descriptor =>
                descriptor.ImplementationInstance is TMarker
            );
        }

        /// <summary>
        /// Registers a new registration marker with the specified type discriminator in the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <typeparam name="TMarker">The type that discriminates the marker interface.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/> instance for chaining additional calls.</returns>
        /// <remarks>
        /// Registration markers are used to track which service groups have been registered,
        /// enabling verification of required dependencies at application startup.
        /// </remarks>
        [PublicAPI]
        public IServiceCollection AddRegistrationMarker<TMarker>()
            where TMarker : IRegistrationMarker<TMarker>, new()
        {
            var marker = new TMarker();

            if (serviceCollection.IsRegistered<TMarker>())
            {
                throw new InvalidOperationException(
                    $"Services for '{typeof(TMarker).FullName}' have already been registered."
                );
            }

            return serviceCollection.AddSingleton<IRegistrationMarker<TMarker>>(marker);
        }

        /// <summary>
        /// Registers <paramref name="instance"/> as a singleton <typeparamref name="TService"/>, one of several of that
        /// service, unless the same instance is already registered for it. <c>TryAddEnumerable</c> keeps one
        /// registration per implementation type, which would drop every other instance of the same type, such as the
        /// other curves or algorithm specifications an application registers; this keeps them all and still makes a
        /// repeated registration of one instance safe.
        /// </summary>
        /// <typeparam name="TService">The service type the instance is registered as.</typeparam>
        /// <param name="instance">The instance to register.</param>
        /// <returns>The <see cref="IServiceCollection"/> instance for chaining additional calls.</returns>
        [PublicAPI]
        public IServiceCollection TryAddEnumerableInstance<TService>(TService instance)
            where TService : class
        {
            ArgumentNullException.ThrowIfNull(instance);

            if (
                !serviceCollection.Any(descriptor =>
                    descriptor.ServiceType == typeof(TService)
                    && !descriptor.IsKeyedService
                    && ReferenceEquals(descriptor.ImplementationInstance, instance)
                )
            )
            {
                serviceCollection.AddSingleton(instance);
            }

            return serviceCollection;
        }

        /// <summary>
        /// Creates a new <see cref="IServiceBuilder{TNewMarker}"/> over this <see cref="IServiceCollection"/>,
        /// discriminated by the specified marker type.
        /// </summary>
        /// <typeparam name="TNewMarker">The type that discriminates the new service builder.</typeparam>
        /// <returns>The newly created <see cref="IServiceBuilder{TNewMarker}"/> instance.</returns>
        [PublicAPI]
        public IServiceBuilder<TNewMarker> NewBuilder<TNewMarker>()
            where TNewMarker : IRegistrationMarker<TNewMarker>, new()
        {
            return new ServiceBuilder<TNewMarker>(serviceCollection);
        }
    }
}
