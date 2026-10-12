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
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Playground.Database;

/// <summary>
/// Shared Npgsql wiring for the Playground reference OP database (ADR-0073). The Playground <c>api</c> host and the
/// <c>MigrationService</c> worker both configure the dev Postgres database through these helpers so the provider,
/// migrations assembly, and migrations-history tables stay a single source of truth.
/// </summary>
[PublicAPI]
public static class PlaygroundDatabase
{
    /// <summary>
    /// The assembly that owns the EF Core migrations for both the <see cref="OpenIdDbContext"/> and the
    /// <see cref="DeveloperDataProtectionDbContext"/>. Both contexts persist into one Postgres database, so the
    /// DataProtection context keeps its own migrations-history table to avoid colliding with the OpenID history.
    /// </summary>
    public static readonly string MigrationsAssembly = typeof(PlaygroundDatabase)
        .Assembly.GetName()
        .Name!;

    private const string DataProtectionHistoryTable = "__EFMigrationsHistory_DataProtection";

    /// <summary>
    /// Configures the <see cref="OpenIdDbContext"/> options to use Npgsql against <paramref name="connectionString"/>,
    /// pointing migrations at this (the Playground database) assembly.
    /// </summary>
    /// <param name="options">The options builder to configure.</param>
    /// <param name="connectionString">The Postgres connection string (supplied by Aspire at run time).</param>
    /// <returns>The same <paramref name="options"/> for chaining.</returns>
    public static DbContextOptionsBuilder UseOpenIdNpgsql(
        this DbContextOptionsBuilder options,
        string connectionString
    ) =>
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql.MigrationsAssembly(MigrationsAssembly)
        );

    /// <summary>
    /// Configures the <see cref="DeveloperDataProtectionDbContext"/> options to use Npgsql against
    /// <paramref name="connectionString"/>, with a dedicated migrations-history table so it can share the OpenID
    /// database without colliding with the OpenID migrations history.
    /// </summary>
    /// <param name="options">The options builder to configure.</param>
    /// <param name="connectionString">The Postgres connection string (supplied by Aspire at run time).</param>
    /// <returns>The same <paramref name="options"/> for chaining.</returns>
    public static DbContextOptionsBuilder UseDataProtectionNpgsql(
        this DbContextOptionsBuilder options,
        string connectionString
    ) =>
        options.UseNpgsql(
            connectionString,
            npgsql =>
                npgsql
                    .MigrationsAssembly(MigrationsAssembly)
                    .MigrationsHistoryTable(DataProtectionHistoryTable)
        );

    /// <summary>
    /// An unscoped <see cref="IAmbientTenantAccessor"/> used when a context is constructed outside a request (EF
    /// design-time tooling and the migration worker). Schema operations never read tenant-scoped data, so an unscoped
    /// accessor is sufficient and avoids resolving the request-scoped accessor.
    /// </summary>
    public sealed class UnscopedAmbientTenantAccessor : IAmbientTenantAccessor
    {
        /// <inheritdoc />
        public bool IsScoped => false;

        /// <inheritdoc />
        public string? TenantId => null;

        /// <inheritdoc />
        public IDisposable BeginScope(string tenantId) =>
            throw new NotSupportedException("Unscoped construction cannot begin a tenant scope.");
    }
}
