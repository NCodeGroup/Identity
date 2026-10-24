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
using Microsoft.Extensions.Options;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Provides extension methods to configure the persisted-grant services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the persisted-grant service, its schema-versioned payload serializer, and the hosted service
        /// that removes the grants that ended before their retention.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddPersistenceServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IPersistedPayloadSerializer,
                DefaultPersistedPayloadSerializer
            >();
            serviceCollection.TryAddSingleton<
                IPersistedGrantService,
                DefaultPersistedGrantService
            >();

            serviceCollection.AddOptions<GrantRetentionOptions>().ValidateOnStart();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<GrantRetentionOptions>,
                    GrantRetentionOptionsValidator
                >()
            );
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.AddHostedService<GrantPruneService>();

            return builder;
        }
    }
}
