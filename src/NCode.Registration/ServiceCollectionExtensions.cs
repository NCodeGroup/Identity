#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
