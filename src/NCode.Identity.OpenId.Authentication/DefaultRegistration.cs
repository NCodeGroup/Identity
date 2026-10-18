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
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Consent;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Pkce;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Mediator;
using NCode.Registration;

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
                .AddClientServices()
                .AddClientSettingServices()
                .AddConsentServices()
                .AddEndpoints()
                .AddMessageServices()
                .AddOutboundHttpServices()
                .AddPersistenceServices()
                .AddPkceServices()
                .AddResourceServerServices()
                .AddSessionServices()
                .AddSubjectServices()
                .AddTokenServices();

            return newBuilder;
        }
    }
}
