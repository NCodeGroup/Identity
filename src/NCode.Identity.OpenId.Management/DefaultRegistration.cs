// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management.Auditing;
using NCode.Identity.OpenId.Management.Authorization;
using NCode.Identity.OpenId.Management.Endpoints;
using NCode.Identity.OpenId.Management.Endpoints.Secrets;
using NCode.Identity.OpenId.Management.OpenApi;
using NCode.Identity.OpenId.Management.ResourceServers;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using NCode.Registration;
using NCode.Registration.AspNetCore;

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

            // OwnershipHandler is an ASP.NET Core authorization handler invoked without the request's OpenIdContext in
            // hand, so it reads the ambient context from the request (the one blessed async-local usage).
            serviceCollection.AddHttpContextAccessor();

            serviceCollection.AddAuthorization();
            serviceCollection.AddAuthorizationHandler<GlobalAdminHandler>();
            serviceCollection.AddAuthorizationHandler<OwnershipHandler>();

            // Enrich the OpenAPI document with descriptions for the management API's shared parameters.
            serviceCollection.AddOpenApi(openApiOptions =>
                openApiOptions.AddDocumentTransformer<ManagementParameterDocumentTransformer>()
            );

            // Answers every bodyless error under the management prefix as a problem, in every host (ADR-1483).
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IStartupFilter,
                    ManagementProblemResponseStartupFilter
                >()
            );

            serviceCollection.TryAddSingleton<
                IResourceOwnershipService,
                DefaultResourceOwnershipService
            >();

            serviceCollection.TryAddSingleton<
                IAuthorityChainResolver,
                DefaultAuthorityChainResolver
            >();

            serviceCollection.TryAddSingleton<
                IManagementTenantEnvironmentResolver,
                DefaultManagementTenantEnvironmentResolver
            >();

            serviceCollection.TryAddSingleton<
                IManagementAuditRecorder,
                DefaultManagementAuditRecorder
            >();

            // Shared by the tenant/client secret-create endpoints (ADR-1015).
            serviceCollection.TryAddSingleton<ISecretValidator, DefaultSecretValidator>();

            // Shared by the tenant/client public-key read endpoints (ADR-1058). Delegates to each key's
            // ISecretKeyTypeDescriptor (the single extensibility seam, ADR-1104) for the JWK/PEM representation.
            serviceCollection.TryAddSingleton<
                ISecretPublicKeyConverter,
                DefaultSecretPublicKeyConverter
            >();

            // Binds a key a key management service already holds to a tenant, after proving the key signs (ADR-1475).
            serviceCollection.TryAddSingleton<IRemoteSecretImporter, DefaultRemoteSecretImporter>();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<SeedTenantCommand>,
                    ManagementResourceServerSeedHandler
                >()
            );

            // The root group declares its own endpoints and child groups (ADR-1042); registering it wires the whole
            // /api tree.
            builder.AddEndpointGroup<ManagementRootGroup>();

            return builder.NewBuilder<OpenIdManagementLibrary>();
        }
    }
}
