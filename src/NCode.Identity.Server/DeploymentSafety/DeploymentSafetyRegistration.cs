// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Server.DeploymentSafety;
using NCode.Identity.Server.DevelopmentEnvironment;

namespace NCode.Identity.Server;

/// <summary>
/// Refuses to start a deployment (a host without <c>LocalDevelopment:Enabled</c>) whose configuration is only safe for
/// local development or does not protect its stored secrets.
/// </summary>
/// <remarks>
/// Most settings checked here are conveniences that are correct on a developer's machine and dangerous on a public
/// origin. The
/// rest describe a secret protection configuration that cannot keep stored secrets readable and private: no root key
/// that outlives the process, a root key held inline in configuration, or no decision about recovering a lost root
/// key. Each finding is also silent: a deployment that forgets to override or supply it starts, serves traffic, and
/// looks healthy, which is precisely why these are startup failures rather than log warnings. Failing before the
/// server binds a port is the only outcome an operator cannot miss. The gate also refuses, as the host finishes
/// starting, a server that binds a plain-HTTP endpoint or serves an unusable certificate.
/// </remarks>
[PublicAPI]
public static class DeploymentSafetyRegistration
{
    private const string AllowedHostsConfigurationKey = "AllowedHosts";

    private const string RootKeyProviderConfigurationKey = "SecretProtection:RootKeyProvider";

    private const string LocalRootKeyConfigurationKey =
        "SecretProtection:RootKeyProviders:Local:Key";

    private const string RecoveryPublicKeyPathConfigurationKey =
        "SecretProtection:Recovery:PublicKeyPath";

    private const string RecoveryDisabledConfigurationKey = "SecretProtection:Recovery:Disabled";

    internal const string KeyRingRepositoryMissingMessage =
        "Refusing to start a deployment: the Data Protection key ring has no repository, so it "
        + "falls back to a local directory or to memory and is lost when the replica is replaced. Persist it to "
        + "storage every replica shares (for example PersistKeysToDbContext). A host for local development sets "
        + "'LocalDevelopment:Enabled' to true.";

    internal const string KeyRingUnprotectedMessage =
        "Refusing to start a deployment: the Data Protection key ring has no XmlEncryptor. "
        + "Configure the 'SecretProtection' section: its root key protects the key ring and every stored secret. "
        + "A host for local development sets 'LocalDevelopment:Enabled' to true.";

    /// <summary>
    /// Returns every unsafe-for-a-deployment finding in <paramref name="configuration"/>, regardless of whether
    /// <c>LocalDevelopment:Enabled</c> is set.
    /// </summary>
    /// <param name="configuration">The <see cref="IConfiguration"/> to inspect.</param>
    /// <returns>The findings, empty when the configuration is safe to expose publicly.</returns>
    public static IReadOnlyList<DeploymentSafetyViolation> FindViolations(
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var violations = new List<DeploymentSafetyViolation>();

        // Stored secrets and the key ring are only as safe as the root key that protects them.
        AddSecretProtectionViolations(configuration, violations);

        // A wildcard host filter accepts any Host header, which is what lets a request for an unrelated origin reach
        // the application at all.
        var allowedHosts = configuration[AllowedHostsConfigurationKey];
        if (
            string.IsNullOrWhiteSpace(allowedHosts)
            || allowedHosts.Split(';').Any(host => host.Trim() == "*")
        )
        {
            violations.Add(
                new DeploymentSafetyViolation(
                    AllowedHostsConfigurationKey,
                    allowedHosts is null or ""
                        ? "is not set, so every Host header is accepted."
                        : "'*' accepts every Host header.",
                    "List this deployment's host names explicitly."
                )
            );
        }

        // Pruning discards a signing key whose master key, or the root key that wraps it, was lost or changed, and
        // provisioning replaces it (ADR-1370).
        AddSectionViolationIfPresent(
            DeveloperKeysOptions.DefaultSectionName,
            "prunes signing keys that can no longer be read and provisions new ones, which in a deployment hides a lost root key behind a silent key change."
        );

        AddSectionViolationIfPresent(
            DeveloperCorsOptions.DefaultSectionName,
            "configures the developer CORS policy, a development-only convenience that admits cross-origin calls from "
                + "the origins it lists."
        );

        // Both seeders create accounts and clients with known, published credentials.
        AddSectionViolationIfPresent(
            DeveloperInteractiveSeedOptions.DefaultSectionName,
            "seeds a development administrator and a public SPA client with well-known credentials."
        );
        AddSectionViolationIfPresent(
            ConformanceEnvironment.ConformanceSeedOptions.DefaultSectionName,
            "seeds the deterministic clients and test user that the public conformance suite drives."
        );

        AddSectionViolationIfPresent(
            BootstrapAdminOptions.DefaultSectionName,
            "configures the bootstrap client, a development and test convenience whose secret confers GlobalAdmin; "
                + "a deployment's first administrator is the root that setup creates."
        );

        return violations;

        void AddSectionViolationIfPresent(string sectionName, string problem)
        {
            if (configuration.GetSection(sectionName).Exists())
                violations.Add(
                    new DeploymentSafetyViolation(sectionName, problem, "Remove the section.")
                );
        }
    }

    private static void AddSecretProtectionViolations(
        IConfiguration configuration,
        List<DeploymentSafetyViolation> violations
    )
    {
        const string providerResolution =
            "Set it to a root key provider whose key outlives the process, such as a key management service "
            + $"('{KeyWrapperNames.AwsKms}', '{KeyWrapperNames.AzureKeyVault}' or "
            + $"'{KeyWrapperNames.GoogleCloudKms}') or '{KeyWrapperNames.Local}' with a mounted key file.";

        var rootKeyProvider = configuration[RootKeyProviderConfigurationKey];
        if (string.IsNullOrWhiteSpace(rootKeyProvider))
        {
            violations.Add(
                new DeploymentSafetyViolation(
                    RootKeyProviderConfigurationKey,
                    "is not set, so no root key protects the stored secrets.",
                    providerResolution
                )
            );
        }
        else if (
            string.Equals(rootKeyProvider, KeyWrapperNames.Ephemeral, StringComparison.Ordinal)
        )
        {
            violations.Add(
                new DeploymentSafetyViolation(
                    RootKeyProviderConfigurationKey,
                    $"'{KeyWrapperNames.Ephemeral}' keeps the root key in memory, so every restart makes every "
                        + "stored secret unreadable.",
                    providerResolution
                )
            );
        }

        // An inline root key in a deployment's configuration is a leaked key even when another provider is selected.
        if (!string.IsNullOrWhiteSpace(configuration[LocalRootKeyConfigurationKey]))
        {
            violations.Add(
                new DeploymentSafetyViolation(
                    LocalRootKeyConfigurationKey,
                    "holds a root key inline in configuration.",
                    "Mount the key as a file and set 'SecretProtection:RootKeyProviders:Local:KeyPath' instead."
                )
            );
        }

        var recoveryDisabled =
            bool.TryParse(configuration[RecoveryDisabledConfigurationKey], out var disabled)
            && disabled;
        if (
            string.IsNullOrWhiteSpace(configuration[RecoveryPublicKeyPathConfigurationKey])
            && !recoveryDisabled
        )
        {
            violations.Add(
                new DeploymentSafetyViolation(
                    RecoveryPublicKeyPathConfigurationKey,
                    "is not set, so losing the root key loses every stored secret.",
                    $"Configure a recovery public key, or set '{RecoveryDisabledConfigurationKey}' to true to "
                        + "accept that risk."
                )
            );
        }
    }

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Validates that the current configuration is safe to expose publicly, and throws when it is not. The check
        /// runs in every host environment (Development, Conformance, Staging, Production, and any custom name) unless
        /// <c>LocalDevelopment:Enabled</c> is true, which a local inner-loop host sets and which leaves the gate
        /// unregistered. It also refuses a secret
        /// protection configuration that cannot keep stored secrets readable and private, and, as the host starts, a
        /// Data Protection key ring that has no repository or is not encrypted at rest, a work factor for supplied
        /// client secrets below the recommended one, and configured platform settings whose <c>environment_type</c> is
        /// a registered non-production type. It also refuses, as the host finishes starting, a server that binds a
        /// plain-HTTP endpoint or serves an unusable certificate.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> to validate.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The host is a deployment and at least one development-only convenience is still enabled, or the secret
        /// protection configuration is unsafe. The message lists every finding at once, so a misconfigured deployment
        /// is fixed in one pass rather than one restart per problem.
        /// </exception>
        public IServiceCollection AddDeploymentSafety(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            serviceCollection.AddLocalDevelopmentOptions(configuration);

            // Kestrel serves HTTPS only, with a certificate that passes the transport checks (ADR-1504); inert when LocalDevelopment:Enabled is set, checked as the host finishes starting.
            serviceCollection.AddTransportSecurity(configuration);

            if (configuration.IsLocalDevelopment())
                return serviceCollection;
            // Data Protection is composed after this gate runs, so the key ring is checked as the host starts.
            serviceCollection
                .AddOptions<KeyManagementOptions>()
                .Validate(
                    options => options.XmlRepository is not null,
                    KeyRingRepositoryMissingMessage
                )
                .Validate(options => options.XmlEncryptor is not null, KeyRingUnprotectedMessage)
                .ValidateOnStart();

            // A supplied client secret's verifier keeps at least the recommended work factor (ADR-1391). The options
            // are configured in code, so they are checked as the host starts.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<Pbkdf2SecretVerifierOptions>,
                    DeploymentPbkdf2IterationsValidator
                >()
            );
            serviceCollection.AddOptions<Pbkdf2SecretVerifierOptions>().ValidateOnStart();

            // The platform-wide environment type needs the registered types, so it is checked as the host starts too.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    IValidateOptions<OpenIdOptions>,
                    DeploymentEnvironmentTypeValidator
                >()
            );
            serviceCollection.AddOptions<OpenIdOptions>().ValidateOnStart();

            var violations = DeploymentSafetyRegistration.FindViolations(configuration);
            if (violations.Count == 0)
                return serviceCollection;

            var detail = string.Join(
                Environment.NewLine,
                violations.Select(violation => $"  - {violation}")
            );
            throw new InvalidOperationException(
                $"Refusing to start a deployment: {violations.Count} setting(s) are unsafe "
                    + $"for a deployment.{Environment.NewLine}{detail}{Environment.NewLine}"
                    + "Fix them for this deployment. A host for local development sets 'LocalDevelopment:Enabled' to true."
            );
        }
    }
}
