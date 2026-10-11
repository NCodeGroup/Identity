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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using NCode.Registration;

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
    private const string BundleRootPathConfigurationKey = "BundleRootPath";
    private const string AntiforgeryHeaderName = "X-XSRF-TOKEN";
    private const string CookieName = "NCode.Identity.Interactive";

    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Registers the interactive login/consent surface: a cookie scheme named
        /// <see cref="InteractiveAuthenticationDefaults.CookieSchemeName"/> (with its <c>LoginPath</c> /
        /// <c>LogoutPath</c> pointed at the surface) that the authorize endpoint's challenge redirects into, and the
        /// antiforgery configuration the single-page surface uses. The bundle location is read from the
        /// <c>InteractiveAuthentication:BundleRootPath</c> configuration value (resolved relative to the content root
        /// when not rooted) and may be overridden through <paramref name="configureOptions"/>. Apply it to the
        /// pipeline with <c>UseInteractiveAuthentication</c> and map it with <c>MapInteractiveAuthentication</c>. The
        /// cookie scheme is registered as a named scheme and does not itself set the host's default authentication
        /// scheme. Credential verification is deferred to a registered <see cref="IInteractiveCredentialVerifier"/>,
        /// which the host must supply separately.
        /// </summary>
        /// <remarks>
        /// The host MUST order <c>UseAntiforgery</c> AFTER <c>UseAuthentication</c>/<c>UseAuthorization</c>. The login
        /// and consent forms submit claims-bound antiforgery tokens, and when this cookie is the only registered
        /// authentication scheme ASP.NET Core promotes it to the default — so once a session cookie exists the login
        /// page authenticates the user and mints the token bound to that user. Validating the POST before
        /// authentication runs would compare an authenticated token against an anonymous user and throw
        /// "The provided antiforgery token was meant for a different claims-based user than the current user".
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

            var options = new InteractiveAuthenticationOptions();

            var configuredBundlePath = section[BundleRootPathConfigurationKey];
            if (!string.IsNullOrWhiteSpace(configuredBundlePath))
            {
                options.BundleRootPath = Path.IsPathRooted(configuredBundlePath)
                    ? configuredBundlePath
                    : Path.Combine(hostEnvironment.ContentRootPath, configuredBundlePath);
            }

            configureOptions?.Invoke(options);

            serviceCollection.AddSingleton(options);
            serviceCollection.TryAddSingleton<InteractiveAuthenticationEndpointHandler>();

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

            return builder;
        }
    }
}
