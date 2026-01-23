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
        [PublicAPI]
        public bool IsRegistered<TMarker>()
            where TMarker : IRegistrationMarker<TMarker>, new()
        {
            return serviceCollection.Any(descriptor => descriptor.ImplementationInstance is TMarker);
        }

        /// <summary>
        /// Verifies that services for <typeparamref name="TMarker"/> are registered in the <see cref="IServiceCollection"/>.
        /// </summary>
        /// <param name="message">The message to include in the exception when required services are not registered.
        /// If <c>null</c> or empty, a default message is used.</param>
        /// <typeparam name="TMarker">The type that discriminates the marker interface.</typeparam>
        /// <returns>The <see cref="IServiceCollection"/> instance for chaining additional calls.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the required services for <typeparamref name="TMarker"/> are not registered in the <see cref="IServiceCollection"/>.</exception>
        /// <remarks>
        /// This entire project uses a design where implementation libraries do not depend on (aka have a direct reference to)
        /// other implementation libraries. Implementation libraries only depend on abstraction libraries. Therefore, this method
        /// is used to verify that the composition root has registered all the required services throughout the entire stack.
        /// </remarks>
        [PublicAPI]
        public IServiceCollection VerifyIsRegistered<TMarker>(string? message = null)
            where TMarker : IRegistrationMarker<TMarker>, new()
        {
            if (!serviceCollection.IsRegistered<TMarker>())
            {
                var effectiveMessage = GetMarkerNotFoundMessage<TMarker>(message);
                throw new InvalidOperationException(effectiveMessage);
            }

            return serviceCollection;
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
                throw new InvalidOperationException($"Services for '{marker.DisplayName}' have already been registered.");
            }

            return serviceCollection.AddSingleton<IRegistrationMarker<TMarker>>(marker);
        }

        [PublicAPI]
        public IServiceBuilder<TNewMarker> NewBuilder<TNewMarker>()
            where TNewMarker : IRegistrationMarker<TNewMarker>, new()
        {
            return new ServiceBuilder<TNewMarker>(serviceCollection);
        }
    }

    /// <summary>
    /// Gets the effective error message for when a registration marker is not found.
    /// </summary>
    /// <param name="message">The custom message provided by the caller, or <c>null</c> to use the default message.</param>
    /// <typeparam name="TMarker">The type that discriminates the marker interface.</typeparam>
    /// <returns>The effective error message to use in the exception.</returns>
    private static string GetMarkerNotFoundMessage<TMarker>(string? message)
        where TMarker : IRegistrationMarker<TMarker>, new()
    {
        if (string.IsNullOrEmpty(message))
        {
            var marker = new TMarker();
            message = $"The services for '{marker.DisplayName}' have not been registered. Please call the '{marker.ConfigureMethod}' method.";
        }

        return message;
    }
}
