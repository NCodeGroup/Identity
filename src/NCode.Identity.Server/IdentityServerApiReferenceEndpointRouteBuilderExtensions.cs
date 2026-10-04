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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId;
using NCode.Identity.Server.OpenApi;
using Scalar.AspNetCore;

namespace NCode.Identity.Server;

/// <summary>
/// Provides turnkey endpoint mapping for the OpenID server's API reference: the OpenAPI document and a Scalar UI wired
/// to the server's own security schemes. It is safe in every environment — the client-credentials login is only
/// pre-filled with the bootstrap administrator credentials during development, and never carries a secret otherwise.
/// </summary>
[PublicAPI]
public static class IdentityServerApiReferenceEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the OpenAPI document and a Scalar API reference for the OpenID server. The OAuth 2.0 client-credentials
        /// scheme is preferred so an operator can authenticate the whole dashboard; during development, when a
        /// bootstrap administrator is configured, its credentials are pre-filled for a one-click login. The OpenAPI
        /// document and the UI are independently toggleable via <paramref name="configureOptions"/>.
        /// </summary>
        /// <param name="configureOptions">An optional callback to configure which endpoints are mapped.</param>
        /// <returns>The same <see cref="IEndpointRouteBuilder"/> instance for method chaining.</returns>
        public IEndpointRouteBuilder MapIdentityServerApiReference(
            Action<IdentityServerApiReferenceOptions>? configureOptions = null
        )
        {
            var options = new IdentityServerApiReferenceOptions();
            configureOptions?.Invoke(options);

            if (options.MapOpenApiDocument)
            {
                endpoints.MapOpenApi();
            }

            if (!options.MapApiReferenceUi)
            {
                return endpoints;
            }

            var services = endpoints.ServiceProvider;
            var environment = services.GetRequiredService<IHostEnvironment>();
            var bootstrap = services.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;

            endpoints.MapScalarApiReference(scalarOptions =>
            {
                scalarOptions.AddPreferredSecuritySchemes(
                    SecuritySchemeDocumentTransformer.OAuth2SchemeName
                );

                // Wire the non-secret parts of the client-credentials login in every environment so an operator can
                // authenticate the dashboard out of the box (body credentials to match the token endpoint, correct
                // token URL and audience-binding scope) -- this is the production bootstrap path too. Only the secret
                // is pre-filled, and only during development, so it is never embedded in the served configuration.
                scalarOptions.AddClientCredentialsFlow(
                    SecuritySchemeDocumentTransformer.OAuth2SchemeName,
                    flow =>
                    {
                        flow.WithTokenUrl(OpenIdConstants.EndpointPaths.Token)
                            .WithSelectedScopes(
                                SecuritySchemeDocumentTransformer.ManagementAudienceScope
                            )
                            .WithCredentialsLocation(CredentialsLocation.Body);

                        if (
                            environment.IsDevelopment()
                            && bootstrap
                                is {
                                    ClientId: { Length: > 0 } clientId,
                                    ClientSecret: { Length: > 0 } clientSecret,
                                }
                        )
                        {
                            flow.WithClientId(clientId).WithClientSecret(clientSecret);
                        }
                    }
                );
            });

            return endpoints;
        }
    }
}
