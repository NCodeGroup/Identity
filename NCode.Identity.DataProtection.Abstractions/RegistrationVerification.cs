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

namespace NCode.Identity.DataProtection;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to verify that secure data protection services are registered.
/// </summary>
[PublicAPI]
public static class RegistrationVerification
{
    /// <summary>
    /// Verifies that the secure data protection services are registered in the <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to check whether the secure data protection services are registered.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for chaining additional calls.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the secure data protection services are not registered in the <see cref="IServiceCollection"/>.</exception>
    public static IServiceCollection VerifySecureDataProtectionServicesAreRegistered(this IServiceCollection serviceCollection)
    {
        return serviceCollection.VerifyServiceTypeIsRegistered<ISecureDataProtectionRegistrationMarker>(
            "The secure data protection services are not registered. Please call the 'AddSecureDataProtectionServices' method to register the secure data protection services.");
    }
}
