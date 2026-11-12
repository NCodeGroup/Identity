// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Server.OpenApi;
using Scalar.AspNetCore;

namespace NCode.Identity.Server;

/// <summary>
/// Provides endpoint mapping for the OpenID server's API reference: the OpenAPI document and a Scalar page wired
/// to the OpenID Provider's own security schemes. It maps only what <see cref="IdentityServerApiReferenceOptions"/>
/// enables. The client-credentials login is configured only on the page, and the bootstrap administrator's secret is
/// pre-filled only during development.
/// </summary>
[PublicAPI]
public static class IdentityServerApiReferenceEndpointRouteBuilderExtensions
{
    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the OpenAPI document when <see cref="IdentityServerApiReferenceOptions.DocumentEnabled"/> is set and a
        /// Scalar API reference page when <see cref="IdentityServerApiReferenceOptions.PageEnabled"/> is set, as the
        /// options bound from the <c>ApiReference</c> section say. The OAuth 2.0 client-credentials scheme is
        /// preferred on the page so an operator can authenticate the whole dashboard; during development, when a
        /// bootstrap administrator is configured, its credentials are pre-filled for a one-click login. The login is
        /// configured only when the page is mapped.
        /// </summary>
        /// <returns>The same <see cref="IEndpointRouteBuilder"/> instance for method chaining.</returns>
        public IEndpointRouteBuilder MapIdentityServerApiReference()
        {
            var services = endpoints.ServiceProvider;
            var options = services
                .GetRequiredService<IOptions<IdentityServerApiReferenceOptions>>()
                .Value;

            if (options.DocumentEnabled)
            {
                endpoints.MapOpenApi();
            }

            if (!options.PageEnabled)
            {
                return endpoints;
            }

            var environment = services.GetRequiredService<IHostEnvironment>();
            var bootstrap = services.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;

            endpoints.MapScalarApiReference(
                (scalarOptions, httpContext) =>
                {
                    var nonce = ContentSecurityPolicySources.CreateNonce();
                    httpContext.Response.SetHtmlPageSecurity(CreatePagePolicy(nonce));

                    // Scalar's bundled stylesheet reads its nonce from this element.
                    scalarOptions
                        .WithNonce(nonce)
                        .AddHeadContent($"<meta property=\"csp-nonce\" content=\"{nonce}\" />")
                        .DisableTelemetry()
                        .DisableAgent()
                        .DisableMcp()
                        .DisableDefaultFonts();

                    scalarOptions.AddPreferredSecuritySchemes(
                        SecuritySchemeDocumentTransformer.OAuth2SchemeName
                    );

                    // Wire the non-secret parts of the client-credentials login in every environment so an operator can
                    // authenticate the dashboard out of the box (HTTP Basic credentials, matching the bootstrap client's
                    // registered client_secret_basic, the token URL, and the audience-binding scope) -- this is the
                    // production bootstrap path too. Only the secret is pre-filled, and only during development, so it is
                    // never embedded in the served configuration.
                    scalarOptions.AddClientCredentialsFlow(
                        SecuritySchemeDocumentTransformer.OAuth2SchemeName,
                        flow =>
                        {
                            flow.WithTokenUrl(OpenIdConstants.EndpointPaths.Token)
                                .WithSelectedScopes(
                                    SecuritySchemeDocumentTransformer.ManagementAudienceScope
                                )
                                .WithCredentialsLocation(CredentialsLocation.Header);

                            if (
                                environment.IsDevelopment()
                                && bootstrap.ClientId is { Length: > 0 } clientId
                                && bootstrap.GetPresentableClientSecretOrDefault()
                                    is { Length: > 0 } clientSecret
                            )
                            {
                                flow.WithClientId(clientId).WithClientSecret(clientSecret);
                            }
                        }
                    );
                }
            );

            return endpoints;
        }
    }

    private const string DataSource = "data:";

    // See ADR-1256 and ADR-1481.
    private static HtmlPageSecurityPolicy CreatePagePolicy(string nonce) =>
        new()
        {
            Directives =
            [
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ScriptSrc,
                    ContentSecurityPolicySources.Nonce(nonce),
                    ContentSecurityPolicySources.StrictDynamic
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.StyleSrc,
                    ContentSecurityPolicySources.Nonce(nonce)
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ImgSrc,
                    ContentSecurityPolicySources.Self,
                    DataSource
                ),
                new ContentSecurityPolicyDirective(
                    ContentSecurityPolicyDirectives.ConnectSrc,
                    ContentSecurityPolicySources.Self
                ),
            ],
        };
}
