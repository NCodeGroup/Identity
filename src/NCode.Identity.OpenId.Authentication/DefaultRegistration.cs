// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Pkce;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.Registration;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Authentication.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IdentityLibrary"/> builder this library extends.</param>
    extension(IServiceBuilder<IdentityLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Authentication.
        /// </summary>
        /// <param name="joseBuilder">The <see cref="JoseLibrary"/> builder — the compile-time proof that the JOSE
        /// prerequisite the token handlers depend on is registered.</param>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddOpenIdAuthenticationLibrary(
            IServiceBuilder<JoseLibrary> joseBuilder
        )
        {
            ArgumentNullException.ThrowIfNull(joseBuilder);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddMediator();
            // Subject and authorization handlers call HttpContext.AuthenticateAsync, which requires the ASP.NET Core
            // authentication services (IAuthenticationService / IAuthenticationSchemeProvider).
            serviceCollection.AddAuthentication();

            // Wires up the OpenAPI pipeline so the XML-comment source generator documents this plane's endpoints.
            serviceCollection.AddOpenApi();

            var newBuilder = builder.NewBuilder<OpenIdAuthenticationLibrary>();

            newBuilder
                .AddAuditingServices()
                .AddClientCertificateServices()
                .AddClientServices()
                .AddClientSettingServices()
                .AddConsentServices()
                .AddEndpoints()
                .AddMessageServices()
                .AddOutboundHttpServices()
                .AddPasswordServices()
                .AddPersistenceServices()
                .AddPkceServices()
                .AddRateLimitingServices()
                .AddResourceServerServices()
                .AddSessionServices()
                .AddSubjectServices()
                .AddTenantUsageServices()
                .AddTokenServices();

            return newBuilder;
        }
    }
}
