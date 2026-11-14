// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.OpenId;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Server.OpenApi;
using Scalar.AspNetCore;

namespace NCode.Identity.Server;

/// <summary>
/// Provides endpoint mapping for the OpenID server's API reference: the OpenAPI document and a Scalar page wired
/// to the OpenID Provider's own security schemes. It maps only what <see cref="IdentityServerApiReferenceOptions"/>
/// enables. The client-credentials login is configured only on the page, and the bootstrap administrator's secret is
/// pre-filled only when <c>LocalDevelopment:Enabled</c> is true.
/// </summary>
[PublicAPI]
public static class IdentityServerApiReferenceEndpointRouteBuilderExtensions
{
    /// <summary>
    /// The <c>data:</c> source the page's images may load, for the icons Scalar inlines.
    /// </summary>
    private const string DataSource = "data:";

    /// <summary>
    /// Builds the page's Content-Security-Policy: scripts and styles only with the response's nonce, images from its
    /// own origin and <c>data:</c>, and connections only to its own origin.
    /// </summary>
    /// <param name="nonce">The nonce generated for the response.</param>
    /// <returns>The <see cref="HtmlPageSecurityPolicy"/> to apply to the page's response.</returns>
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

    extension(IEndpointRouteBuilder endpoints)
    {
        /// <summary>
        /// Maps the OpenAPI document when <see cref="IdentityServerApiReferenceOptions.DocumentEnabled"/> is set and a
        /// Scalar API reference page when <see cref="IdentityServerApiReferenceOptions.PageEnabled"/> is set, as the
        /// options bound from the <c>ApiReference</c> section say. The OAuth 2.0 client-credentials scheme is
        /// preferred on the page so an operator can authenticate the whole dashboard; when
        /// <c>LocalDevelopment:Enabled</c> is true and a bootstrap administrator is configured, its credentials are
        /// pre-filled for a one-click login. The login is configured only when the page is mapped.
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

            var localDevelopment = services
                .GetRequiredService<IOptions<LocalDevelopmentOptions>>()
                .Value;
            var bootstrap = services.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;

            endpoints.MapScalarApiReference(
                IdentityServerApiReferenceOptions.PagePrefix,
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

                    // Wire the non-secret parts of the client-credentials login in every host so an operator
                    // can authenticate the dashboard out of the box (HTTP Basic credentials, matching the bootstrap
                    // client's registered client_secret_basic, the token URL, and the audience-binding scope) -- this
                    // is the deployment bootstrap path too. Only the secret is pre-filled, and only when
                    // LocalDevelopment:Enabled is true, so it is never embedded in the served configuration of a
                    // deployment.
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
                                localDevelopment.Enabled
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
}
