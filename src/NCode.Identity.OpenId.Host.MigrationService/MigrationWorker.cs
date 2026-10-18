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

using NCode.Identity.OpenId.Host.Database;

namespace NCode.Identity.OpenId.Host.MigrationService;

/// <summary>
/// Applies the host database migrations once on startup, then stops the host. A non-zero exit on failure makes
/// Aspire mark this resource failed and hold back the resources that <c>WaitForCompletion</c> on it.
/// </summary>
internal sealed class MigrationWorker(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    IHostApplicationLifetime lifetime,
    ILogger<MigrationWorker> logger
) : BackgroundService
{
    // Matches the Aspire database resource name in the AppHost graph.
    private const string ConnectionStringName = "openiddb";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var connectionString =
                configuration.GetConnectionString(ConnectionStringName)
                ?? throw new InvalidOperationException(
                    $"The '{ConnectionStringName}' connection string was not supplied; run the migration worker "
                        + "through the Aspire AppHost so the Postgres resource is wired via WithReference."
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
