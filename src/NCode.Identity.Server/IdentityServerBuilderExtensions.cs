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
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Dispatch.Options;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.Frontend;
using NCode.Identity.Server.SecretProtection;
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
            MutualTlsSubsection,
            ClientCertificateRevocationOptions.SectionName,
            GrantRetentionOptions.SectionName,
            TenantUsageOptions.SectionName,
            EndedSessionTerminationOptions.SectionName,
            InProcessJobDispatcherOptions.SectionName
        );

    /// <param name="builder">The <see cref="IIdentityServerBuilder"/> to configure.</param>
    extension(IIdentityServerBuilder builder)
    {
        /// <summary>
        /// Binds the server's options from configuration, so a host does not have to repeat the binding boilerplate.
        /// The OpenID configuration section (<see cref="OpenIdOptions.DefaultSectionName"/> unless
        /// <paramref name="sectionName"/> names another) holds nine subsections, each bound to one options type:
        /// <list type="bullet">
        /// <item><description><c>Server</c> — <see cref="OpenIdServerOptions"/>, including the server's
        /// <c>Settings</c>.</description></item>
        /// <item><description><c>Tenant</c> — <see cref="OpenIdTenantOptions"/>: how a resolved tenant is
        /// materialized and cached, and which keys every tenant is provisioned with.</description></item>
        /// <item><description><c>TenantResolution</c> — <see cref="TenantResolutionOptions"/>: which tenant a request
        /// selects, and the route the tenant is served under.</description></item>
        /// <item><description><c>MutualTls</c> — <see cref="MutualTlsForwardingOptions"/>: how a forwarded mutual-TLS
        /// client certificate is read.</description></item>
        /// <item><description><c>ClientCertificateRevocation</c> — <see cref="ClientCertificateRevocationOptions"/>: how
        /// the OCSP responses and revocation lists a mutual-TLS client certificate names are retrieved and
        /// cached.</description></item>
        /// <item><description><c>GrantRetention</c> — <see cref="GrantRetentionOptions"/>: how long ended grants are
        /// kept and how often they are removed.</description></item>
        /// <item><description><c>TenantUsage</c> — <see cref="TenantUsageOptions"/>: how often each replica adds its
        /// usage to the ledger and how long the ledger keeps it.</description></item>
        /// <item><description><c>EndedSessionTermination</c> — <see cref="EndedSessionTerminationOptions"/>: how often
        /// the sessions that ended while unused are terminated.</description></item>
        /// <item><description><c>InProcessJobDispatcher</c> — <see cref="InProcessJobDispatcherOptions"/>: how many
        /// jobs the in-process dispatcher queues and runs at once, and what a dispatch does when the queue is
        /// full.</description></item>
        /// </list>
        /// The top-level <c>BootstrapAdmin</c> section binds <see cref="BootstrapAdminOptions"/>, and the top-level
        /// <c>SecretProtection</c> section binds <see cref="SecretProtectionOptions"/>: it names the root key provider
        /// that protects the stored secrets and the Data Protection key ring at rest.
        /// </summary>
        /// <remarks>
        /// Binding is strict and checked when the host starts: a key that maps to no option — a misspelling, or a
        /// setting placed in the wrong subsection — stops the host with an error naming the key, rather than leaving
        /// the option at its default without any sign that the setting was ignored. The <c>SecretProtection</c>
        /// section is also checked immediately: a direct child other than <c>RootKeyProvider</c>, <c>Recovery</c> and
        /// <c>RootKeyProviders</c> stops the host during composition.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> to bind the options from.</param>
        /// <param name="sectionName">The configuration section name that holds the OpenID options, or <c>null</c> to use
        /// <see cref="OpenIdOptions.DefaultSectionName"/>.</param>
        /// <returns>The same <see cref="IIdentityServerBuilder"/> instance for method chaining.</returns>
        /// <exception cref="InvalidOperationException">The <c>SecretProtection</c> section has a direct child other
        /// than <c>RootKeyProvider</c>, <c>Recovery</c> and <c>RootKeyProviders</c>.</exception>
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
            var clientCertificateRevocationSection = openIdSection.GetSection(
                ClientCertificateRevocationOptions.SectionName
            );
            var grantRetentionSection = openIdSection.GetSection(GrantRetentionOptions.SectionName);
            var tenantUsageSection = openIdSection.GetSection(TenantUsageOptions.SectionName);
            var endedSessionTerminationSection = openIdSection.GetSection(
                EndedSessionTerminationOptions.SectionName
            );
            var inProcessJobDispatcherSection = openIdSection.GetSection(
                InProcessJobDispatcherOptions.SectionName
            );
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
                .AddOptions<ClientCertificateRevocationOptions>()
                .Configure(options => clientCertificateRevocationSection.BindStrictly(options))
                .ValidateOnStart();

            serviceCollection
                .AddOptions<GrantRetentionOptions>()
                .Configure(options => grantRetentionSection.BindStrictly(options))
                .ValidateOnStart();

            serviceCollection
                .AddOptions<TenantUsageOptions>()
                .Configure(options => tenantUsageSection.BindStrictly(options))
                .ValidateOnStart();

            serviceCollection
                .AddOptions<EndedSessionTerminationOptions>()
                .Configure(options => endedSessionTerminationSection.BindStrictly(options))
                .ValidateOnStart();

            // See ADR-0242.
            serviceCollection
                .AddOptions<InProcessJobDispatcherOptions>()
                .Configure(options => inProcessJobDispatcherSection.BindStrictly(options))
                .ValidateOnStart();

            serviceCollection
                .AddOptions<BootstrapAdminOptions>()
                .Configure(options => bootstrapAdminSection.BindStrictly(options))
                .ValidateOnStart();

            // Stored secrets and the Data Protection key ring are protected under a root key held outside the database (ADR-0235).
            SecretProtectionRegistration.AddSecretProtection(builder, configuration);

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

            serviceCollection.TryAddScoped<FrontendIssuerResolver>();

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
