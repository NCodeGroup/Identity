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

namespace NCode.Identity;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to verify that required services are registered.
/// </summary>
[PublicAPI]
public static class RegistrationVerificationExtensions
{
    /// <summary>
    /// Verifies that the specified service type is registered in the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to check whether the service type is registered.</param>
    /// <param name="message">The message to include in the exception when the service type is not registered.
    /// Optional, a default message is used if not specified.</param>
    /// <typeparam name="TServiceType">The service type to check whether it is registered.</typeparam>
    /// <returns>The <see cref="IServiceCollection"/> instance for chaining additional calls.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the specified service type is not registered in the <see cref="IServiceCollection"/>.</exception>
    public static IServiceCollection VerifyServiceTypeIsRegistered<TServiceType>(
        this IServiceCollection serviceCollection,
        string? message = null)
    {
        if (serviceCollection.All(descriptor => descriptor.ServiceType != typeof(TServiceType)))
        {
            throw new InvalidOperationException(message ?? $"The required service type '{typeof(TServiceType).FullName}' has not been registered.");
        }

        return serviceCollection;
    }
}
