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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Jose;
using NCode.Identity.JsonWebTokens;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Server.OpenApi;
using NCode.Mediator;
using NCode.PropertyBag;
using NCode.Registration;

namespace NCode.Identity.Server;

/// <summary>
/// Provides the default implementation of <see cref="IIdentityServerBuilder"/>.
/// </summary>
internal sealed class IdentityServerBuilder : ServiceBuilder<IdentityServer>, IIdentityServerBuilder
{
    /// <inheritdoc />
    public IDataProtectionBuilder DataProtectionBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<SecretsLibrary> SecretsLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<SecretPersistenceLibrary> SecretPersistenceLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<JoseLibrary> JoseLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<JsonWebTokensLibrary> JsonWebTokensLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<IdentityLibrary> IdentityLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdCoreLibrary> OpenIdCoreLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdAuthenticationLibrary> OpenIdAuthenticationLibraryBuilder { get; }

    /// <inheritdoc />
    public IServiceBuilder<OpenIdManagementLibrary> OpenIdManagementLibraryBuilder { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IdentityServerBuilder"/> class.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    public IdentityServerBuilder(IServiceCollection serviceCollection)
        : base(serviceCollection)
    {
        serviceCollection.AddPropertyBag();

        DataProtectionBuilder = serviceCollection.AddDataProtection();
        SecretsLibraryBuilder = serviceCollection.AddSecretsLibrary();
        SecretPersistenceLibraryBuilder = SecretsLibraryBuilder.AddPersistenceLibrary();

        JoseLibraryBuilder = serviceCollection.AddJoseLibrary();
        JsonWebTokensLibraryBuilder = serviceCollection.AddJsonWebTokensLibrary();

        IdentityLibraryBuilder = serviceCollection.AddIdentityLibrary();
        OpenIdCoreLibraryBuilder = IdentityLibraryBuilder.AddOpenIdCoreLibrary();
        OpenIdAuthenticationLibraryBuilder =
            IdentityLibraryBuilder.AddOpenIdAuthenticationLibrary();
        OpenIdManagementLibraryBuilder = IdentityLibraryBuilder.AddOpenIdManagementLibrary();

        // The bootstrap administrator client's management tokens are stamped with the GlobalAdmin role at issuance
        // (ADR-0044); the handler is a no-op unless a bootstrap client id is configured.
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ICommandHandler<GetAccessTokenPayloadClaimsCommand>,
                BootstrapAdminAccessTokenClaimsHandler
            >()
        );

        // The bootstrap administrator client is seeded into the workload tenant on the same tenant seed fan-out that
        // seeds the system resource servers (ADR-0045); inert unless a bootstrap client is configured.
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                ICommandHandler<SeedTenantCommand>,
                BootstrapAdminSeedHandler
            >()
        );

        // The OpenAPI document describes this server's own endpoints, so the composition root owns its
        // registration and grouping rather than leaving each host to re-wire it.
        serviceCollection.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<TagGroupsDocumentTransformer>();
            options.AddDocumentTransformer<SecuritySchemeDocumentTransformer>();
            options.AddDocumentTransformer<TokenEndpointExamplesDocumentTransformer>();
        });
    }
}
