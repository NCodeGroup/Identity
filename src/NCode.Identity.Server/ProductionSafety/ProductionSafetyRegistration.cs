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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Identity.Server.ProductionSafety;

namespace NCode.Identity.Server;

/// <summary>
/// Refuses to start a Production deployment whose configuration is only safe for local development.
/// </summary>
/// <remarks>
/// Every setting checked here is a convenience that is correct in Development and dangerous on a public origin. Each
/// one is also silent: a deployment that forgets to override it starts, serves traffic, and looks healthy, which is
/// precisely why these are startup failures rather than log warnings. Failing before the server binds a port is the
/// only outcome an operator cannot miss.
/// </remarks>
[PublicAPI]
public static class ProductionSafetyRegistration
{
    private const string AllowedHostsConfigurationKey = "AllowedHosts";
    private const string AllowedOriginsConfigurationKey = "Cors:AllowedOrigins";
    private const string DeveloperKeyModeConfigurationKey = "DeveloperKeys:Mode";

    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Validates that the current configuration is safe to expose publicly, and throws when it is not. The check
        /// runs only in a Production environment; every other environment (Development, Conformance, Staging, and any
        /// custom name) is left alone so local and test hosts keep their conveniences.
        /// </summary>
        /// <param name="configuration">The <see cref="IConfiguration"/> to validate.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> that decides whether to validate at all.</param>
        /// <returns>The same <see cref="IServiceCollection"/> instance for method chaining.</returns>
        /// <exception cref="InvalidOperationException">
        /// The environment is Production and at least one development-only convenience is still enabled. The message
        /// lists every finding at once, so a misconfigured deployment is fixed in one pass rather than one restart per
        /// problem.
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

            var violations = ProductionSafetyRegistration.FindViolations(configuration);
            if (violations.Count == 0)
                return serviceCollection;

            var detail = string.Join(
                Environment.NewLine,
                violations.Select(violation => $"  - {violation}")
            );
            throw new InvalidOperationException(
                $"Refusing to start in the Production environment: {violations.Count} development-only "
                    + $"setting(s) are still enabled.{Environment.NewLine}{detail}{Environment.NewLine}"
                    + "Override them for this deployment, or run under a non-Production environment name."
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

        // Developer keys are development-only in BOTH developer modes (ADR-0002, ADR-0021): Ephemeral regenerates the
        // signing key on every restart, invalidating every token and JWKS cache the moment the process recycles, and
        // PersistentSigningKey seeds a key the server generated for itself rather than one an operator controls.
        // Absent configuration is not safe either, because the mode defaults to Ephemeral — so Production must state
        // DeveloperKeyMode.None explicitly to assert that it supplies its own key.
        var developerKeyMode = configuration[DeveloperKeyModeConfigurationKey];
        if (
            !string.Equals(
                developerKeyMode,
                nameof(DeveloperKeyMode.None),
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            violations.Add(
                new ProductionSafetyViolation(
                    DeveloperKeyModeConfigurationKey,
                    developerKeyMode is null
                        ? $"is unset, which defaults to {nameof(DeveloperKeyMode.Ephemeral)} signing keys that are "
                            + "regenerated on every restart."
                        : $"'{developerKeyMode}' is a development-only signing key source.",
                    $"Set it to '{nameof(DeveloperKeyMode.None)}' and supply a stable, operator-managed signing key."
                )
            );
        }

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

        // Loopback CORS origins only make sense when a developer's browser is the caller.
        var loopbackOrigins = configuration
            .GetSection(AllowedOriginsConfigurationKey)
            .Get<string[]>()
            ?.Where(ProductionSafetyRegistration.IsLoopbackOrigin)
            .ToArray();
        if (loopbackOrigins is { Length: > 0 })
        {
            violations.Add(
                new ProductionSafetyViolation(
                    AllowedOriginsConfigurationKey,
                    $"grants cross-origin access to local dev server(s): {string.Join(", ", loopbackOrigins)}.",
                    "Remove them, or list only this deployment's real browser origins."
                )
            );
        }

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

    private static bool IsLoopbackOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (
            uri.IsLoopback
            || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Host, "host.docker.internal", StringComparison.OrdinalIgnoreCase)
        );
}
