// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Collections.Providers;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;
using NCode.Identity.OpenId.Locales;
using NCode.Identity.OpenId.Results;
using NCode.Identity.Registration;
using NCode.Identity.Registration.AspNetCore;
using NCode.Identity.Settings;
using NCode.Mediator;

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
        /// <c>MapEndpointGroups</c> alongside the protocol endpoints, and a second group serves the bundle's
        /// content-hashed files beneath the tenant route without resolving the tenant, so no middleware is added to
        /// the pipeline. The cookie scheme is registered as a named scheme and does not itself set the host's default
        /// authentication scheme. Credential verification is deferred to a registered
        /// <see cref="IInteractiveCredentialVerifier"/>, which the host must supply separately. The locales of
        /// <see cref="InteractiveLocales"/> register as the surface's UI locales in one order whatever the order of
        /// this call and <see cref="AddInteractiveLocale"/>: the shipped locales in their shipped order, English
        /// first, each replaced by the host's locale of its tag, then the locales with other tags in the order the
        /// host registered them.
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
            // before it accepts traffic (ADR-1137).
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

            serviceCollection.TryAddSingleton(
                static serviceProvider => new SinglePageBundle<InteractiveBundleShell>(
                    serviceProvider
                        .GetRequiredService<IOptions<InteractiveAuthenticationOptions>>()
                        .Value.BundleRootPath,
                    InteractiveBundleShell.TryCreate
                )
            );
            serviceCollection.TryAddSingleton<InteractiveSurfacePaths>();

            // The locales the surface ships are the UI locales the tenant settings and discovery name, English first.
            // A UI locale is registered data: each is registered as an instance, and the registration dedupes by tag,
            // which TryAddEnumerable cannot, since it dedupes by implementation type and every shipped locale is the
            // one record type. The registered order is canonical, a function of the set of registered locales alone
            // and never of the order of the calls, so English is the first registered locale, and the surface's
            // default language, whether the host's AddInteractiveLocale calls come before or after this one. A locale
            // registered without the surface's messages stops the host (ADR-1402).
            foreach (var locale in InteractiveLocales.All)
            {
                if (FindUiLocale(serviceCollection, locale.LanguageTag) < 0)
                {
                    serviceCollection.AddSingleton<IUiLocale>(locale);
                }
            }

            OrderUiLocales(serviceCollection);

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<InteractiveAuthenticationOptions>,
                    InteractiveAuthenticationOptionsValidator
                >()
            );

            serviceCollection.TryAddSingleton<InteractiveLocaleSelector>();

            // A credential backend that holds passwords replaces this default, which reports that no change is
            // supported.
            serviceCollection.TryAddSingleton<
                IInteractivePasswordChanger,
                DefaultInteractivePasswordChanger
            >();

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

            // The bundle's content-hashed files are served beneath the tenant route by a group that resolves no tenant
            // (ADR-1193).
            builder.AddEndpointGroup<InteractiveBundleAssetEndpointGroup>();

            // Replace the core no-op consent-challenge handler with one that redirects to the interactive consent page.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<
                    ICommandResponseHandler<ConsentChallengeCommand, ReadOnlyEndpointDisposition>,
                    InteractiveConsentChallengeHandler
                >()
            );

            // Replace the core default end-session confirmation handler with one that redirects to the interactive
            // logout confirmation page.
            serviceCollection.Replace(
                ServiceDescriptor.Singleton<
                    ICommandResponseHandler<
                        EndSessionConfirmationChallengeCommand,
                        ReadOnlyEndpointDisposition
                    >,
                    InteractiveEndSessionConfirmationChallengeHandler
                >()
            );

            serviceCollection.AddAntiforgery(antiforgery =>
            {
                antiforgery.HeaderName = AntiforgeryHeaderName;
                antiforgery.Cookie.Name = InteractiveAuthenticationDefaults.AntiforgeryCookieName;
                antiforgery.Cookie.Path = "/";
                // 'Always' would make antiforgery refuse every non-HTTPS request, and the '__Host-' prefix already
                // makes a browser refuse the cookie without 'Secure' (ADR-1193).
                antiforgery.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            });

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
                        cookie.Cookie.Name = InteractiveAuthenticationDefaults.SessionCookieName;
                        cookie.Cookie.Path = "/";
                        cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                        cookie.Cookie.HttpOnly = true;
                        cookie.Cookie.SameSite = SameSiteMode.Lax;
                        cookie.SlidingExpiration = true;
                    }
                );

            // The surface is served under the tenant route (ADR-1193): the session cookie is host-only and named for
            // the tenant (ADR-1193), and a challenge redirects to the login page of the tenant that issued it.
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

        /// <summary>
        /// Registers a locale the interactive surface renders, in place of any registered UI locale of the same
        /// language tag: a host adds a language with a new tag, and changes the wording of a shipped locale by
        /// registering one built from it with its own <see cref="InteractiveMessages"/>, before or after
        /// <see cref="AddInteractiveAuthentication"/>. The registered order is the same whatever the order of the
        /// calls: the shipped locales in their shipped order, English first, each replaced by the host's locale of
        /// its tag, then the locales with other tags in the order the host registered them. So English stays the
        /// surface's default language, unless a tenant's <c>default_ui_locale</c> names another, and a host that
        /// rewords a shipped locale changes its wording and not its place.
        /// </summary>
        /// <remarks>
        /// A UI locale is registered data, so this method and <see cref="AddInteractiveAuthentication"/> register
        /// each locale as an instance, find the registered locale of a tag among the instances, and order the
        /// instances. An <see cref="IUiLocale"/> a host registers through a type or a factory is outside this rule:
        /// it keeps its own place, its tag is registered twice when a locale of the same tag is registered this way,
        /// and the catalog refuses the duplicate tag when the host starts.
        /// </remarks>
        /// <param name="locale">The locale to register.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddInteractiveLocale(
            InteractiveLocale locale
        )
        {
            ArgumentNullException.ThrowIfNull(locale);

            var serviceCollection = builder.ServiceCollection;
            var descriptor = ServiceDescriptor.Singleton<IUiLocale>(locale);
            var index = FindUiLocale(serviceCollection, locale.LanguageTag);
            if (index < 0)
            {
                serviceCollection.Add(descriptor);
            }
            else
            {
                serviceCollection[index] = descriptor;
            }

            OrderUiLocales(serviceCollection);

            return builder;
        }
    }

    // The index of the registered IUiLocale instance whose tag is languageTag, compared without regard to case as
    // BCP 47 tags are, or -1 when none is registered.
    private static int FindUiLocale(IServiceCollection serviceCollection, string languageTag)
    {
        for (var index = 0; index < serviceCollection.Count; index++)
        {
            if (
                TryGetUiLocale(serviceCollection[index], out var registered)
                && string.Equals(
                    registered.LanguageTag,
                    languageTag,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return index;
            }
        }

        return -1;
    }

    // Registers the IUiLocale instances again in the canonical order, a function of the set of registered locales
    // alone: the shipped tags in the shipped order, each the registered locale of its tag, then the other tags in
    // the order they were registered. Both registrations end with it, so the order is the same whichever of them
    // comes first and however they interleave. The instances take the place of the first of them, so a locale
    // registered through a type or a factory keeps its place among them.
    private static void OrderUiLocales(IServiceCollection serviceCollection)
    {
        var registered = new List<(ServiceDescriptor Descriptor, IUiLocale Locale)>();
        var insertIndex = serviceCollection.Count;
        for (var index = serviceCollection.Count - 1; index >= 0; index--)
        {
            var descriptor = serviceCollection[index];
            if (TryGetUiLocale(descriptor, out var locale))
            {
                registered.Add((descriptor, locale));
                serviceCollection.RemoveAt(index);
                insertIndex = index;
            }
        }

        registered.Reverse();

        var ordered = new List<ServiceDescriptor>(registered.Count);
        foreach (var shipped in InteractiveLocales.All)
        {
            var index = FindUiLocale(registered, shipped.LanguageTag);
            if (index >= 0)
            {
                ordered.Add(registered[index].Descriptor);
                registered.RemoveAt(index);
            }
        }

        foreach (var (descriptor, _) in registered)
        {
            ordered.Add(descriptor);
        }

        foreach (var descriptor in ordered)
        {
            serviceCollection.Insert(insertIndex, descriptor);
            insertIndex++;
        }
    }

    // The index of the registered locale whose tag is languageTag, or -1 when none is registered.
    private static int FindUiLocale(
        List<(ServiceDescriptor Descriptor, IUiLocale Locale)> registered,
        string languageTag
    )
    {
        for (var index = 0; index < registered.Count; index++)
        {
            if (
                string.Equals(
                    registered[index].Locale.LanguageTag,
                    languageTag,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryGetUiLocale(
        ServiceDescriptor descriptor,
        [MaybeNullWhen(false)] out IUiLocale locale
    )
    {
        if (
            descriptor.ServiceType == typeof(IUiLocale)
            && !descriptor.IsKeyedService
            && descriptor.ImplementationInstance is IUiLocale registered
        )
        {
            locale = registered;
            return true;
        }

        locale = null;
        return false;
    }

    // Mirrors the cookie handler's own login redirect with the login page of the request's tenant in place of a fixed
    // login path, as a root-relative URL so the request's Host contributes nothing; its return URL is the challenge's
    // redirect URI, or the challenged request itself. The challenge's ui_locales parameter, which the authorization
    // endpoint sets from the request, reaches the page so it renders in the end-user's preferred language.
    // See ADR-1365 and ADR-1402.
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
        var uiLocales = context.Properties.GetParameter<string>(
            OpenIdProtocol.Parameters.UiLocales
        );
        if (!string.IsNullOrEmpty(uiLocales))
        {
            query = query.Add(OpenIdProtocol.Parameters.UiLocales, uiLocales);
        }

        return $"{loginPath}{query}";
    }

    private static string? ResolveBundleRootPath(string? bundleRootPath, string contentRootPath) =>
        string.IsNullOrWhiteSpace(bundleRootPath)
            ? bundleRootPath
            : Path.GetFullPath(bundleRootPath, contentRootPath);
}
