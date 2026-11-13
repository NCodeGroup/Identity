// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Management;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Provides the opt-in registration for the one-time instance setup: the wizard and the headless command that create
/// an instance's root administrator, guarded by a one-time setup token.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    private static string? ResolveBundleRootPath(string? bundleRootPath, string contentRootPath) =>
        string.IsNullOrWhiteSpace(bundleRootPath)
            ? bundleRootPath
            : Path.GetFullPath(bundleRootPath, contentRootPath);

    /// <param name="builder">The <see cref="OpenIdManagementLibrary"/> builder this capability extends.</param>
    extension(IServiceBuilder<OpenIdManagementLibrary> builder)
    {
        /// <summary>
        /// Registers the one-time instance setup. <see cref="SetupOptions"/> is bound from the <c>Setup</c>
        /// configuration section, and a relative bundle path is resolved against the content root.
        /// </summary>
        /// <remarks>
        /// The section is bound strictly and checked when the host starts: a key that maps to no option, a configured
        /// token shorter than <see cref="SetupDefaults.MinimumTokenLength"/>, a generated-token lifetime outside one
        /// minute to twenty-four hours, and a root administrator with a username or a password alone stop the host
        /// with an error naming the key.
        /// </remarks>
        /// <param name="configuration">The <see cref="IConfiguration"/> read for the setup settings.</param>
        /// <param name="hostEnvironment">The <see cref="IHostEnvironment"/> whose content root anchors a relative
        /// bundle path.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<OpenIdManagementLibrary> AddInstanceSetup(
            IConfiguration configuration,
            IHostEnvironment hostEnvironment
        )
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(hostEnvironment);

            var serviceCollection = builder.ServiceCollection;

            var section = configuration.GetSection(SetupOptions.SectionName);

            // ValidateOnStart materializes the options as the host starts, so a strict-binding failure stops the host
            // before it accepts traffic (ADR-1137).
            serviceCollection
                .AddOptions<SetupOptions>()
                .Configure(options => section.BindStrictly(options))
                .PostConfigure(options =>
                    options.BundleRootPath = ResolveBundleRootPath(
                        options.BundleRootPath,
                        hostEnvironment.ContentRootPath
                    )
                )
                .ValidateOnStart();

            serviceCollection.TryAddSingleton(TimeProvider.System);

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<SetupOptions>, SetupOptionsValidator>()
            );

            serviceCollection.TryAddSingleton<SetupProcessClock>();
            serviceCollection.TryAddSingleton<ISetupTokenService, DefaultSetupTokenService>();

            return builder;
        }
    }
}
