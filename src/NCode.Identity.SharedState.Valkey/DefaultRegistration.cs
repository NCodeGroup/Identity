// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Provides opt-in extension methods for the <see cref="SharedStateLibrary"/> builder to register the Valkey adapter.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// The name of the health check that reports whether the Valkey server is reachable.
    /// </summary>
    internal const string HealthCheckName = "valkey";

    private const string ConnectionStringPath =
        $"ConnectionStrings:{ValkeyOptions.ConnectionStringName}";

    private static ConfigurationOptions ParseConnectionString(string connectionString)
    {
        const string name = ConnectionStringPath;

        ConfigurationOptions configurationOptions;
        try
        {
            configurationOptions = ConfigurationOptions.Parse(connectionString);
        }
        catch (ArgumentException exception)
        {
            // The message names the offending setting; the connection string itself may hold a password.
            throw new InvalidOperationException(
                $"The '{name}' connection string is malformed: {exception.Message}",
                exception
            );
        }

        if (configurationOptions.EndPoints.Count == 0)
            throw new InvalidOperationException($"The '{name}' connection string names no server.");

        return configurationOptions;
    }

    /// <param name="builder">The shared-state-library builder returned by <c>AddSharedStateServices</c>.</param>
    extension(IServiceBuilder<SharedStateLibrary> builder)
    {
        /// <summary>
        /// Registers the Valkey <see cref="IExpiringKeySet"/>, <see cref="IExpiringCounterSet"/>,
        /// <see cref="IExpiringValueSet"/>, <see cref="IExpiringIndexSet"/>, and
        /// <see cref="IReplicaBroadcaster"/> adapters, in place of any adapters registered before them, when the
        /// Valkey connection is configured, either as the <c>ConnectionStrings:valkey</c> connection string or as the
        /// parts under the <c>ValkeyConnection</c> section (<c>Host</c>, <c>Port</c>, <c>Username</c>,
        /// <c>Password</c> and <c>Ssl</c>), so every replica of the deployment shares one set of keys, one set of
        /// counters, one set of values, one set of indexes, and one broadcast channel per name. A hosted service
        /// subscribes the replica's <see cref="IReplicaBroadcastHandler"/> handlers at startup. The <c>Valkey</c>
        /// section binds <see cref="ValkeyOptions"/> strictly. A <c>valkey</c> health check, tagged <c>ready</c>,
        /// reports whether the server answers. Without the connection the call registers nothing and the adapters
        /// registered before it stay in force — the in-process adapters, correct only for a single replica.
        /// </summary>
        /// <param name="configuration">
        /// The <see cref="IConfiguration"/> holding the connection and the <c>Valkey</c> section.
        /// </param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that calls can be chained.</returns>
        /// <exception cref="InvalidOperationException">
        /// The <c>Valkey</c> section is present without a connection, both forms of the connection are configured, the
        /// connection string is malformed or names no server, or the parts have no host, a port outside 1 to 65535, or
        /// an <c>Ssl</c> that is not a boolean.
        /// </exception>
        [PublicAPI]
        public IServiceBuilder<SharedStateLibrary> AddValkey(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(configuration);

            var section = configuration.GetSection(ValkeyOptions.DefaultSectionName);
            var connectionString = configuration.GetConnectionString(
                ValkeyOptions.ConnectionStringName
            );

            var partsOptions = ValkeyConnectionParts.Resolve(configuration);
            var hasConnectionString = !string.IsNullOrEmpty(connectionString);

            if (hasConnectionString && partsOptions is not null)
                throw new InvalidOperationException(
                    $"The connection string '{ConnectionStringPath}' and the '{ValkeyConnectionParts.SectionName}' parts are both configured; configure exactly one of them."
                );

            ConfigurationOptions configurationOptions;
            if (partsOptions is not null)
            {
                configurationOptions = partsOptions;
            }
            else if (!string.IsNullOrEmpty(connectionString))
            {
                configurationOptions = ParseConnectionString(connectionString);
            }
            else
            {
                // Options without a server would otherwise be ignored without a sign that the server was never used.
                if (section.Exists())
                    throw new InvalidOperationException(
                        $"The '{section.Path}' configuration section requires the '{ConnectionStringPath}' connection string or the '{ValkeyConnectionParts.SectionName}' parts."
                    );

                return builder;
            }

            // A server that is unreachable when the process starts is retried in the background rather than failing it.
            configurationOptions.AbortOnConnectFail = false;

            var serviceCollection = builder.ServiceCollection;

            serviceCollection
                .AddOptions<ValkeyOptions>()
                .Configure(options => section.BindStrictly(options))
                .ValidateOnStart();

            serviceCollection.AddMetrics();
            serviceCollection.TryAddSingleton(TimeProvider.System);
            serviceCollection.TryAddSingleton<ValkeySharedStateMetrics>();
            serviceCollection.TryAddSingleton<IValkeyConnection>(_ => new DefaultValkeyConnection(
                configurationOptions
            ));

            serviceCollection.RemoveAll<IExpiringKeySet>();
            serviceCollection.AddSingleton<IExpiringKeySet, ValkeyExpiringKeySet>();

            serviceCollection.RemoveAll<IExpiringCounterSet>();
            serviceCollection.AddSingleton<IExpiringCounterSet, ValkeyExpiringCounterSet>();

            serviceCollection.RemoveAll<IExpiringValueSet>();
            serviceCollection.AddSingleton<IExpiringValueSet, ValkeyExpiringValueSet>();

            serviceCollection.RemoveAll<IExpiringIndexSet>();
            serviceCollection.AddSingleton<IExpiringIndexSet, ValkeyExpiringIndexSet>();

            serviceCollection.RemoveAll<IReplicaBroadcaster>();
            serviceCollection.AddSingleton<IReplicaBroadcaster, ValkeyReplicaBroadcaster>();
            serviceCollection.AddHostedService<ValkeyReplicaBroadcastSubscriber>();

            serviceCollection
                .AddHealthChecks()
                .AddCheck<ValkeyHealthCheck>(HealthCheckName, tags: [HealthCheckTags.Readiness]);

            return builder;
        }
    }
}
