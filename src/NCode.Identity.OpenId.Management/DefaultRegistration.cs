#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Servers;
using NCode.Identity.OpenId.Management.Endpoints.Tenants;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Provides extension methods to register the OpenID management library and its services.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    extension(IServiceBuilder<IdentityLibrary> builder)
    {
        /// <summary>
        /// Registers the OpenID management library, its authorization handlers, and API endpoints.
        /// </summary>
        /// <returns>An <see cref="IServiceBuilder{TLibrary}"/> for the registered <see cref="OpenIdManagementLibrary"/>.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdManagementLibrary> AddOpenIdManagementLibrary()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddAuthorization();
            serviceCollection.AddAuthorizationHandler<GlobalAdminHandler>();
            serviceCollection.AddAuthorizationHandler<TenantAdminHandler>();

            serviceCollection.AddMediator();
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateDeleteTenantCommand>,
                    DefaultAuthorizeDeleteTenantHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateDeleteTenantCommand>,
                    DefaultTenantHasNoDependentsHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateCreateTenantCommand>,
                    DefaultAuthorizeCreateTenantHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateCreateTenantCommand>,
                    DefaultTenantIsUniqueHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateUpdateTenantCommand>,
                    DefaultAuthorizeUpdateTenantHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateUpdateTenantCommand>,
                    DefaultTenantIfMatchHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateDeleteClientCommand>,
                    DefaultAuthorizeDeleteClientHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateDeleteClientCommand>,
                    DefaultClientHasNoDependentsHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateDeleteServerCommand>,
                    DefaultAuthorizeDeleteServerHandler
                >()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<ValidateDeleteServerCommand>,
                    DefaultServerHasNoDependentsHandler
                >()
            );

            builder.AddEndpointProvider<ServerApiEndpointHandler>();
            builder.AddEndpointProvider<TenantApiEndpointHandler>();
            builder.AddEndpointProvider<ClientApiEndpointHandler>();

            return builder.NewBuilder<OpenIdManagementLibrary>();
        }
    }
}
