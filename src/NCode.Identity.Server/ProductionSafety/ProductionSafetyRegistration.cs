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
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Server.ProductionSafety;

namespace NCode.Identity.Server;

/// <summary>
/// Refuses to start a Production deployment whose configuration is only safe for local development or does not
/// protect its stored secrets.
/// </summary>
/// <remarks>
/// Most settings checked here are conveniences that are correct in Development and dangerous on a public origin. The
/// rest describe a secret protection configuration that cannot keep stored secrets readable and private: no root key
/// that outlives the process, a root key held inline in configuration, or no decision about recovering a lost root
/// key. Each finding is also silent: a deployment that forgets to override or supply it starts, serves traffic, and
/// looks healthy, which is precisely why these are startup failures rather than log warnings. Failing before the
/// server binds a port is the only outcome an operator cannot miss.
/// </remarks>
[PublicAPI]
public static class ProductionSafetyRegistration
{
    private const string AllowedHostsConfigurationKey = "AllowedHosts";

    private const string RootKeyProviderConfigurationKey = "SecretProtection:RootKeyProvider";

    private const string LocalRootKeyConfigurationKey =
        "SecretProtection:RootKeyProviders:Local:Key";

    private const string RecoveryPublicKeyPathConfigurationKey =
        "SecretProtection:Recovery:PublicKeyPath";

    private const string RecoveryDisabledConfigurationKey = "SecretProtection:Recovery:Disabled";

    private const string DeveloperKeyModeConfigurationKey =
        $"{DeveloperKeysOptions.DefaultSectionName}:{nameof(DeveloperKeysOptions.Mode)}";

    internal const string KeyRingRepositoryMissingMessage =
        "Refusing to start in the Production environment: the Data Protection key ring has no repository, so it "
        + "falls back to a local directory or to memory and is lost when the replica is replaced. Persist it to "
        + "storage every replica shares (for example PersistKeysToDbContext).";

    internal const string KeyRingUnprotectedMessage =
        "Refusing to start in the Production environment: the Data Protection key ring has no XmlEncryptor. "
        + "Configure the 'SecretProtection' section: its root key protects the key ring and every stored secret.";

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Validates that the current configuration is safe to expose publicly, and throws when it is not. The check
        /// runs only in a Production environment; every other environment (Development, Conformance, Staging, and any
        /// custom name) is left alone so local and test hosts keep their conveniences. It also refuses a secret
        /// protection configuration that cannot keep stored secrets readable and private, and, as the host starts, a
        /// Data Protection key ring that has no repository or is not encrypted at rest.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> to validate.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> that decides whether to validate at all.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The environment is Production and at least one development-only convenience is still enabled, or the secret
        /// protection configuration is unsafe. The message lists every finding at once, so a misconfigured deployment
        /// is fixed in one pass rather than one restart per problem.
        /// </exception>
        public IServiceCollection AddProductionSafety(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment
        )
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            if (!hostEnvironment.IsProduction())
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

            var violations = ProductionSafetyRegistration.FindViolations(configuration);
            if (violations.Count == 0)
                return serviceCollection;

            var detail = string.Join(
                Environment.NewLine,
                violations.Select(violation => $"  - {violation}")
            );
            throw new InvalidOperationException(
                $"Refusing to start in the Production environment: {violations.Count} setting(s) are unsafe "
                    + $"for a production deployment.{Environment.NewLine}{detail}{Environment.NewLine}"
                    + "Fix them for this deployment, or run under a non-Production environment name."
            );
        }
    }

    /// <summary>
    /// Returns every unsafe-for-production finding in <paramref name="configuration"/>, regardless of environment.
    /// </summary>
    /// <param name="configuration">The <see cref="IConfiguration"/> to inspect.</param>
    /// <returns>The findings, empty when the configuration is safe to expose publicly.</returns>
    public static IReadOnlyList<ProductionSafetyViolation> FindViolations(
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var violations = new List<ProductionSafetyViolation>();

        AddDeveloperKeysViolations(configuration, violations);

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
                new ProductionSafetyViolation(
                    AllowedHostsConfigurationKey,
                    allowedHosts is null or ""
                        ? "is not set, so every Host header is accepted."
                        : "'*' accepts every Host header.",
                    "List this deployment's host names explicitly."
                )
            );
        }

        // The developer CORS policy is never registered in Production, so a Cors section there is configuration the
        // server accepts without honoring — and, when it names loopback origins, one that would be unsafe if it were.
        AddSectionViolationIfPresent(
            DeveloperCorsOptions.DefaultSectionName,
            "configures the developer CORS policy, which is development-only and not registered in Production, so "
                + "it has no effect."
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

        return violations;

        void AddSectionViolationIfPresent(string sectionName, string problem)
        {
            if (configuration.GetSection(sectionName).Exists())
                violations.Add(
                    new ProductionSafetyViolation(sectionName, problem, "Remove the section.")
                );
        }
    }

    private static void AddSecretProtectionViolations(
        IConfiguration configuration,
        List<ProductionSafetyViolation> violations
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
                new ProductionSafetyViolation(
                    RootKeyProviderConfigurationKey,
                    "is not set, so the ephemeral root key is used and every restart makes every stored secret "
                        + "unreadable.",
                    providerResolution
                )
            );
        }
        else if (
            string.Equals(rootKeyProvider, KeyWrapperNames.Ephemeral, StringComparison.Ordinal)
        )
        {
            violations.Add(
                new ProductionSafetyViolation(
                    RootKeyProviderConfigurationKey,
                    $"'{KeyWrapperNames.Ephemeral}' keeps the root key in memory, so every restart makes every "
                        + "stored secret unreadable.",
                    providerResolution
                )
            );
        }

        // An inline root key in Production configuration is a leaked key even when another provider is selected.
        if (!string.IsNullOrWhiteSpace(configuration[LocalRootKeyConfigurationKey]))
        {
            violations.Add(
                new ProductionSafetyViolation(
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
                new ProductionSafetyViolation(
                    RecoveryPublicKeyPathConfigurationKey,
                    "is not set, so losing the root key loses every stored secret.",
                    $"Configure a recovery public key, or set '{RecoveryDisabledConfigurationKey}' to true to "
                        + "accept that risk."
                )
            );
        }
    }

    private static void AddDeveloperKeysViolations(
        IConfiguration configuration,
        List<ProductionSafetyViolation> violations
    )
    {
        const string remedy =
            $"Set it to '{nameof(DeveloperKeyMode.None)}'; every tenant is then provisioned with persisted signing "
            + "keys, or holds the keys its operator imports.";

        DeveloperKeysOptions developerKeys;
        try
        {
            developerKeys = configuration.GetDeveloperKeysOptions();
        }
        catch (InvalidOperationException exception)
        {
            violations.Add(
                new ProductionSafetyViolation(
                    DeveloperKeysOptions.DefaultSectionName,
                    exception.Message,
                    remedy
                )
            );
            return;
        }

        // Developer keys are development-only in BOTH developer modes (ADR-0002, ADR-0021, ADR-0169): Ephemeral regenerates
        // the signing key on every restart, invalidating every token and JWKS cache the moment the process recycles, and
        // PersistentSigningKey discards a key whose master key, or the root key that wraps it, was lost or changed and
        // replaces it, which in production would hide a lost root key behind a silent key change. Absent configuration is
        // not safe either, because the mode defaults to Ephemeral — so Production must state DeveloperKeyMode.None
        // explicitly, under which tenants hold the persisted keys they are provisioned with.
        if (developerKeys.Mode == DeveloperKeyMode.None)
            return;

        violations.Add(
            new ProductionSafetyViolation(
                DeveloperKeyModeConfigurationKey,
                developerKeys.Mode is { } mode
                    ? $"'{mode}' is a development-only signing key source."
                    : $"is unset, which defaults to {nameof(DeveloperKeyMode.Ephemeral)} signing keys that are "
                        + "regenerated on every restart.",
                remedy
            )
        );
    }
}
