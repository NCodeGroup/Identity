// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Host.Database;

namespace NCode.Identity.OpenId.Host.MigrationService;

/// <summary>
/// Applies the host database migrations once on startup, then stops the host. A non-zero exit on failure makes
/// Aspire mark this resource failed and hold back the resources that <c>WaitForCompletion</c> on it, and fails the
/// Helm chart's pre-deploy Job so the release stops before its rollout.
/// </summary>
internal sealed class MigrationWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger
) : BackgroundService
{
    // Matches the Aspire database resource name in the AppHost graph and the host's connection string name.
    private const string ConnectionStringName = "openiddb";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connectionString =
                configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"The '{ConnectionStringName}' connection string was not supplied. The Aspire AppHost and the "
                        + "Helm chart's migration Job each supply it; set 'ConnectionStrings:openiddb' to run the "
                        + "worker another way."
                );

            logger.LogInformation("Applying host database migrations.");
            await MigrationRunner.MigrateAllAsync(serviceProvider, connectionString, stoppingToken);
            logger.LogInformation("Host database migrations applied successfully.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Host database migration failed.");
            Environment.ExitCode = 1;
        }
        finally
        {
            lifetime.StopApplication();
        }
    }
}
