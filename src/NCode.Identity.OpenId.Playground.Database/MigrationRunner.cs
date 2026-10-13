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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.EntityFramework;

namespace NCode.Identity.OpenId.Playground.Database;

/// <summary>
/// Applies the EF Core migrations for every Playground context against the Postgres database. Invoked by the
/// <c>MigrationService</c> worker so schema application is a dedicated Aspire resource: a migration failure surfaces as
/// a failed dashboard resource instead of crashing the <c>api</c> host.
/// </summary>
// See ADR-0073.
[PublicAPI]
public static class MigrationRunner
{
    /// <summary>
    /// Applies pending migrations for the <see cref="OpenIdDbContext"/> and the
    /// <see cref="DeveloperDataProtectionDbContext"/>, which share one Postgres database.
    /// </summary>
    /// <param name="serviceProvider">A provider that has the OpenID persistence slice registered (id generator and
    /// model contributors), used to construct the contexts outside a request scope.</param>
    /// <param name="connectionString">The Postgres connection string supplied by Aspire.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    public static async Task MigrateAllAsync(
        IServiceProvider serviceProvider,
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var openIdOptionsBuilder = new DbContextOptionsBuilder<OpenIdDbContext>();
        openIdOptionsBuilder.UseOpenIdNpgsql(connectionString);
        openIdOptionsBuilder.UseApplicationServiceProvider(serviceProvider);

        await using (
            var openIdContext = new OpenIdDbContext(
                openIdOptionsBuilder.Options,
                new PlaygroundDatabase.UnscopedAmbientTenantAccessor(),
                serviceProvider.GetServices<IOpenIdModelContributor>()
            )
        )
        {
            await openIdContext.Database.MigrateAsync(cancellationToken);
        }

        var dataProtectionOptionsBuilder =
            new DbContextOptionsBuilder<DeveloperDataProtectionDbContext>();
        dataProtectionOptionsBuilder.UseDataProtectionNpgsql(connectionString);

        await using (
            var dataProtectionContext = new DeveloperDataProtectionDbContext(
                dataProtectionOptionsBuilder.Options
            )
        )
        {
            await dataProtectionContext.Database.MigrateAsync(cancellationToken);
        }
    }
}
