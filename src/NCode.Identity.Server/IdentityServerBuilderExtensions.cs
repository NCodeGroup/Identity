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

using System.Collections.Frozen;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.Frontend;
using NCode.Registration;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.Server;

/// <summary>
/// Provides extension methods for <see cref="IIdentityServerBuilder"/>.
/// </summary>
[PublicAPI]
public static class IdentityServerBuilderExtensions
{
    private const string TenantResolutionSubsection = "TenantResolution";
    private const string MutualTlsSubsection = "MutualTls";

    // Every subsection of the OpenID configuration section, each bound to exactly one options type below.
    private static FrozenSet<string> OpenIdSubsections { get; } =
        FrozenSet.Create(
            StringComparer.OrdinalIgnoreCase,
            nameof(OpenIdOptions.Server),
            nameof(OpenIdOptions.Tenant),
            TenantResolutionSubsection,
            MutualTlsSubsection
        );

    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to configure.</param>
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Binds the server's options from configuration, so a host does not have to repeat the binding boilerplate.
        /// The OpenID configuration section (<see cref="OpenIdOptions.DefaultSectionName"/> unless
        /// <paramref name="sectionName"/> names another) holds four subsections, each bound to one options type:
        /// <list type="bullet">
        /// <item><description><c>Server</c> — <see cref="OpenIdServerOptions"/>, including the server's
        /// <c>Settings</c>.</description></item>
        /// <item><description><c>Tenant</c> — <see cref="OpenIdTenantOptions"/>: how a resolved tenant is
        /// materialized and cached.</description></item>
        /// <item><description><c>TenantResolution</c> — <see cref="TenantResolutionOptions"/>: which tenant a request
        /// selects, and the route the tenant is served under.</description></item>
        /// <item><description><c>MutualTls</c> — <see cref="MutualTlsForwardingOptions"/>: how a forwarded mutual-TLS
        /// client certificate is read.</description></item>
        /// </list>
        /// The top-level <c>BootstrapAdmin</c> section binds <see cref="BootstrapAdminOptions"/>.
        /// </summary>
        /// <remarks>
        /// Binding is strict and checked when the host starts: a key that maps to no option — a misspelling, or a
        /// setting placed in the wrong subsection — stops the host with an error naming the key, rather than leaving
        /// the option at its default without any sign that the setting was ignored.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> to bind the options from.</param>
        /// <param name="sectionName">The configuration section name that holds the OpenID options, or <c>null</c> to use
        /// <see cref="OpenIdOptions.DefaultSectionName"/>.</param>
        /// <returns>The same <see cref="IIdentityServerBuilder"/> instance for method chaining.</returns>
        public IIdentityServerBuilder AddConfiguration(
            IConfiguration configuration,
            string? sectionName = null
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);

            var resolvedSectionName = string.IsNullOrEmpty(sectionName)
                ? OpenIdOptions.DefaultSectionName
                : sectionName;

            var openIdSection = configuration.GetSection(resolvedSectionName);
            var serverSection = openIdSection.GetSection(nameof(OpenIdOptions.Server));
            var serverSettingsSection = serverSection.GetSection(
                nameof(OpenIdServerOptions.Settings)
            );
            var tenantSection = openIdSection.GetSection(nameof(OpenIdOptions.Tenant));
            var tenantResolutionSection = openIdSection.GetSection(TenantResolutionSubsection);
            var mutualTlsSection = openIdSection.GetSection(MutualTlsSubsection);

            // The bootstrap administrator credential is sourced from its own top-level section so it can be supplied
            // out of band (for example, environment variables); absent it, the feature stays inert (ADR-0044).
            var bootstrapAdminSection = configuration.GetSection(
                BootstrapAdminOptions.DefaultSectionName
            );

            var serviceCollection = builder.ServiceCollection;

            // ValidateOnStart materializes each options instance as the host starts, so a strict-binding failure stops
            // the host before it accepts traffic rather than on the first request that reads the option (ADR-0137).
            serviceCollection
                .AddOptions<OpenIdOptions>()
                .Configure(options =>
                {
                    ThrowOnUnknownSubsection(openIdSection);
                    serverSection.BindStrictly(options.Server);
                    tenantSection.BindStrictly(options.Tenant);

                    // The binder only assigns the settings section when the server section has keys; assign it
                    // regardless so settings added by a later configuration reload are still observed.
                    options.Server.Settings ??= serverSettingsSection;
                })
                .ValidateOnStart();

            serviceCollection
                .AddOptions<TenantResolutionOptions>()
                .Configure(options => tenantResolutionSection.BindStrictly(options))
                .ValidateOnStart();

            serviceCollection
                .AddOptions<MutualTlsForwardingOptions>()
                .Configure(options => mutualTlsSection.BindStrictly(options))
                .ValidateOnStart();

            serviceCollection
                .AddOptions<BootstrapAdminOptions>()
                .Configure(options => bootstrapAdminSection.BindStrictly(options))
                .ValidateOnStart();

            return builder;
        }

        /// <summary>
        /// Binds the <see cref="FrontendBootstrapOptions"/> that back the frontend runtime bootstrap document
        /// from the specified <paramref name="configuration"/> section, optionally applies a code-first
        /// override, and registers the endpoint that publishes the document so the host's single
        /// <c>MapEndpointGroups</c> call maps it alongside every other endpoint. The tier-agnostic frontend bundles
        /// fetch this document from the serving host at boot to discover their deployment coordinates (API base,
        /// issuer, client id, scopes, feature flags, branding); when the API base and issuer are left unconfigured the
        /// published document falls back to the serving request's own origin, so a single-origin host needs no
        /// configuration.
        /// </summary>
        /// <remarks>
        /// Binding is strict and checked when the host starts: a key that maps to no option stops the host with an
        /// error naming the key.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> to bind the options from.</param>
        /// <param name="sectionName">The configuration section name that holds the options, or <c>null</c> to use
        /// <see cref="FrontendBootstrapOptions.DefaultSectionName"/>.</param>
        /// <param name="configureOptions">An optional callback to further configure the options after binding.</param>
        /// <returns>The same <see cref="IIdentityServerBuilder"/> instance for method chaining.</returns>
        public IIdentityServerBuilder AddFrontendBootstrap(
            IConfiguration configuration,
            string? sectionName = null,
            Action<FrontendBootstrapOptions>? configureOptions = null
        )
        {
            var resolvedSectionName = string.IsNullOrEmpty(sectionName)
                ? FrontendBootstrapOptions.DefaultSectionName
                : sectionName;

            var section = configuration.GetSection(resolvedSectionName);

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<FrontendBootstrapOptions>()
                .Configure(options => section.BindStrictly(options))
                .ValidateOnStart();

            if (configureOptions is not null)
            {
                serviceCollection.Configure(configureOptions);
            }

            builder.AddEndpointProvider<FrontendBootstrapEndpointProvider>();

            return builder;
        }
    }

    private static void ThrowOnUnknownSubsection(IConfigurationSection openIdSection)
    {
        var unknownPaths = openIdSection
            .GetChildren()
            .Where(child => !OpenIdSubsections.Contains(child.Key))
            .Select(child => $"'{child.Path}'")
            .ToList();

        if (unknownPaths.Count == 0)
            return;

        throw new InvalidOperationException(
            $"The configuration section '{openIdSection.Path}' contains unrecognized key(s): "
                + $"{string.Join(", ", unknownPaths)}. Its recognized subsections are "
                + $"{string.Join(", ", OpenIdSubsections.Order(StringComparer.Ordinal))}."
        );
    }
}
