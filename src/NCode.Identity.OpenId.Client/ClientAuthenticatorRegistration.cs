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
using NCode.Registration;

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Provides extension methods for <see cref="IServiceBuilder{T}"/> to register custom client authenticators.
/// </summary>
[PublicAPI]
public static class ClientAuthenticatorRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="OpenIdClientLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdClientLibrary> builder)
    {
        /// <summary>
        /// Registers a custom <see cref="IClientAuthenticator"/> so external code can contribute its own client
        /// authentication method. The <see cref="IClientAuthenticator.MethodName"/> must be unique among the
        /// registered authenticators; a duplicate throws when the request sender is first created.
        /// </summary>
        /// <typeparam name="TAuthenticator">The <see cref="IClientAuthenticator"/> implementation to register.</typeparam>
        /// <returns>The <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        public IServiceBuilder<OpenIdClientLibrary> AddClientAuthenticator<TAuthenticator>()
            where TAuthenticator : class, IClientAuthenticator
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientAuthenticator, TAuthenticator>()
            );
            return builder;
        }
    }
}
