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
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Results;
using NCode.Identity.Settings;
using NCode.Mediator;
using NCode.Registration;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Provides the opt-in registration for the interactive login/consent surface: a cookie authentication scheme that
/// satisfies the OpenID server's existing challenge/authenticate seam, plus the single-page-friendly antiforgery
/// configuration the login and consent pages rely on. The surface is backend-agnostic: it defers credential
/// verification to a registered <see cref="IInteractiveCredentialVerifier"/>, so the host must also register a verifier
/// implementation (for example, the local-account capability's <c>AddLocalAccountInteractiveAuthentication()</c>). It
/// is intentionally not part of the default registrations.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    private const string ConfigurationSectionName = "InteractiveAuthentication";
    private const string AntiforgeryHeaderName = "X-XSRF-TOKEN";
    private const string CookieName = "NCode.Identity.Interactive";

    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Registers the interactive login/consent surface: a cookie scheme named
        /// <see cref="InteractiveAuthenticationDefaults.CookieSchemeName"/> (with its <c>LoginPath</c> /
        /// <c>LogoutPath</c> pointed at the surface) that the authorize endpoint's challenge redirects into, and the
        /// antiforgery configuration the single-page surface uses. <see cref="InteractiveAuthenticationOptions"/> is
        /// bound from the <c>InteractiveAuthentication</c> configuration section and may be further configured through
        /// <paramref name="configureOptions"/>; a relative bundle path is resolved against the content root. Its
        /// endpoints register as an <see cref="InteractiveAuthenticationEndpointGroup"/> discovered by
        /// <c>MapEndpointGroups</c> alongside the protocol endpoints, and those endpoints also serve the bundle's files
        /// beneath the tenant route, so no middleware is added to the pipeline. The cookie scheme is registered as a
        /// named scheme and does not
        /// itself set the host's default authentication scheme. Credential verification is deferred to a registered
        /// <see cref="IInteractiveCredentialVerifier"/>, which the host must supply separately.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The section is bound strictly and checked when the host starts: a key that maps to no option stops the host
        /// with an error naming the key, rather than leaving the surface without its bundle and no sign of why.
        /// </para>
        /// <para>
        /// The host MUST order <c>UseAntiforgery</c> AFTER <c>UseAuthentication</c>/<c>UseAuthorization</c>. The login
        /// and consent forms submit claims-bound antiforgery tokens, and when this cookie is the only registered
        /// authentication scheme ASP.NET Core promotes it to the default — so once a session cookie exists the login
        /// page authenticates the user and mints the token bound to that user. Validating the POST before
        /// authentication runs would compare an authenticated token against an anonymous user and throw
        /// "The provided antiforgery token was meant for a different claims-based user than the current user".
        /// </para>
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the surface's settings.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> whose content root anchors a relative
        /// bundle path.</param>
        /// <param name="configureOptions">An optional callback to further configure the surface.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddInteractiveAuthentication(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment,
            Action<InteractiveAuthenticationOptions>? configureOptions = null
        )
        {
            var serviceCollection = builder.ServiceCollection;

            var section = configuration.GetSection(ConfigurationSectionName);

            // ValidateOnStart materializes the options as the host starts, so a strict-binding failure stops the host
            // before it accepts traffic (ADR-0137).
            var optionsBuilder = serviceCollection
                .AddOptions<InteractiveAuthenticationOptions>()
                .Configure(options => section.BindStrictly(options));

            if (configureOptions is not null)
            {
                optionsBuilder.Configure(configureOptions);
            }

            optionsBuilder
                .PostConfigure(options =>
                    options.BundleRootPath = ResolveBundleRootPath(
                        options.BundleRootPath,
                        hostEnvironment.ContentRootPath
                    )
                )
                .ValidateOnStart();

            serviceCollection.TryAddSingleton<InteractiveBundleReader>();
            serviceCollection.TryAddSingleton<InteractiveSurfacePaths>();

            // The branding settings join the descriptor catalog only where the surface that shows them is registered.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingDescriptor>,
                    InteractiveSettingDescriptorDataSource
                >()
            );

            // The content-negotiation seam in OpenIdResult selects this formatter when an interactive browser
            // explicitly requests text/html, rendering a non-redirectable authorization error as a styled HTML page.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IOpenIdResponseFormatter,
                    InteractiveOpenIdErrorResponseFormatter
                >()
            );

            // The surface's endpoints flow through the same MapEndpointGroups discovery as every other endpoint.
            builder.AddEndpointGroup<InteractiveAuthenticationEndpointGroup>();

            // Replace the core no-op consent-challenge handler with one that redirects to the interactive consent page.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<
                    ICommandResponseHandler<ConsentChallengeCommand, ReadOnlyEndpointDisposition>,
                    InteractiveConsentChallengeHandler
                >()
            );

            serviceCollection.AddAntiforgery(antiforgery =>
                antiforgery.HeaderName = AntiforgeryHeaderName
            );

            // Register the cookie as a NAMED scheme only; it must not become the host's default authenticate scheme,
            // which the bearer-oriented subject-authentication seam (userinfo/introspection) resolves by default.
            serviceCollection
                .AddAuthentication()
                .AddCookie(
                    InteractiveAuthenticationDefaults.CookieSchemeName,
                    cookie =>
                    {
                        cookie.LoginPath = InteractiveAuthenticationDefaults.LoginPath;
                        cookie.LogoutPath = InteractiveAuthenticationDefaults.LogoutPath;
                        cookie.Cookie.Name = CookieName;
                        cookie.SlidingExpiration = true;
                    }
                );

            // The surface is served under the tenant route (ADR-0193): the session cookie is scoped to the tenant's
            // path, and a challenge redirects to the login page of the tenant that issued it.
            serviceCollection
                .AddOptions<CookieAuthenticationOptions>(
                    InteractiveAuthenticationDefaults.CookieSchemeName
                )
                .Configure<InteractiveSurfacePaths>(
                    (cookie, surfacePaths) =>
                    {
                        cookie.CookieManager = new InteractiveSessionCookieManager(surfacePaths);
                        var redirectToLogin = cookie.Events.OnRedirectToLogin;
                        cookie.Events.OnRedirectToLogin = context =>
                        {
                            context.RedirectUri = BuildLoginRedirectUri(context, surfacePaths);
                            return redirectToLogin(context);
                        };
                    }
                );

            return builder;
        }
    }

    // Mirrors the cookie handler's own login redirect — an absolute URI whose return URL is the challenge's redirect URI,
    // or the challenged request itself — with the login page of the request's tenant in place of a fixed login path.
    private static string BuildLoginRedirectUri(
        RedirectContext<CookieAuthenticationOptions> context,
        InteractiveSurfacePaths surfacePaths
    )
    {
        var request = context.Request;
        var returnUrl = context.Properties.RedirectUri;
        if (string.IsNullOrEmpty(returnUrl))
        {
            returnUrl = request.PathBase + request.Path + request.QueryString;
        }

        var loginPath = surfacePaths.GetSurfacePath(
            context.HttpContext,
            InteractiveAuthenticationDefaults.LoginPath
        );
        var query = QueryString.Create(context.Options.ReturnUrlParameter, returnUrl);
        return $"{request.Scheme}://{request.Host}{loginPath}{query}";
    }

    private static string? ResolveBundleRootPath(string? bundleRootPath, string contentRootPath) =>
        string.IsNullOrWhiteSpace(bundleRootPath) || Path.IsPathRooted(bundleRootPath)
            ? bundleRootPath
            : Path.Combine(contentRootPath, bundleRootPath);
}
