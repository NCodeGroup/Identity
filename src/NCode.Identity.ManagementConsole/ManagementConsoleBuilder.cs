// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Server;
using NCode.Registration;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Provides the default implementation of <see cref="IManagementConsoleBuilder"/>: the composition root that composes
/// the BFF and adds the services that serve the management console.
/// </summary>
internal sealed class ManagementConsoleBuilder
    : ServiceBuilder<ManagementConsole>,
        IManagementConsoleBuilder
{
    /// <inheritdoc />
    public IIdentityBffBuilder IdentityBffBuilder { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagementConsoleBuilder"/> class.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configuration">
    /// The configuration whose <c>Bff</c> section configures the BFF and whose <c>ManagementConsole</c> section
    /// configures the console.
    /// </param>
    public ManagementConsoleBuilder(
        IServiceCollection serviceCollection,
        IConfiguration configuration
    )
        : base(serviceCollection)
    {
        IdentityBffBuilder = serviceCollection.AddIdentityBff(configuration);

        var section = configuration.GetSection(ManagementConsoleDefaults.ConfigurationSectionName);

        // ValidateOnStart materializes the options as the host starts, so a strict-binding failure stops the host
        // before it accepts traffic (ADR-1137).
        serviceCollection
            .AddOptions<ManagementConsoleOptions>()
            .Configure(options => section.BindStrictly(options))
            .ValidateOnStart();

        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<ManagementConsoleOptions>,
                ManagementConsoleOptionsValidator
            >()
        );
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<BffOptions>,
                ManagementConsoleFrontendValidator
            >()
        );

        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IValidateOptions<HostFilteringOptions>,
                ManagementConsoleHostFilteringValidator
            >()
        );
        serviceCollection.AddOptions<HostFilteringOptions>().ValidateOnStart();

        serviceCollection.TryAddSingleton<ManagementConsoleBundleReader>();
        serviceCollection.TryAddSingleton<ManagementConsoleBootstrapHandler>();
        serviceCollection.TryAddSingleton<ManagementConsolePageHandler>();
        serviceCollection.TryAddSingleton<ManagementConsoleAssetHandler>();
    }
}
