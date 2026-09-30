#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Converters;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;
using NCode.Identity.OpenId.Persistence.Tenants;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Contains the entity framework <see cref="DbContext"/> for <c>OAuth</c> and <c>OpenID Connect</c> entities.
/// </summary>
[PublicAPI]
public class OpenIdDbContext(
    DbContextOptions<OpenIdDbContext> options,
    IAmbientTenantAccessor? ambientTenantAccessor = null
) : DbContext(options)
{
    private IAmbientTenantAccessor? AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <summary>
    /// Gets the normalized identifier of the ambient tenant for the current request, or <c>null</c> when tenant-bound
    /// data access is unscoped. Referenced by the tenant-scoping global query filters.
    /// </summary>
    public string? NormalizedAmbientTenantId => AmbientTenantAccessor?.TenantId?.ToLowerInvariant();

    /// <summary>
    /// Gets the <see cref="SecretEntity"/> entities.
    /// </summary>
    public DbSet<SecretEntity> Secrets => Set<SecretEntity>();

    /// <summary>
    /// Gets the <see cref="ServerEntity"/> entities.
    /// </summary>
    public DbSet<ServerEntity> Servers => Set<ServerEntity>();

    /// <summary>
    /// Gets the <see cref="ServerSecretEntity"/> entities.
    /// </summary>
    public DbSet<ServerSecretEntity> ServerSecrets => Set<ServerSecretEntity>();

    /// <summary>
    /// Gets the <see cref="TenantEntity"/> entities.
    /// </summary>
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();

    /// <summary>
    /// Gets the <see cref="TenantSecretEntity"/> entities.
    /// </summary>
    public DbSet<TenantSecretEntity> TenantSecrets => Set<TenantSecretEntity>();

    /// <summary>
    /// Gets the <see cref="ClientEntity"/> entities.
    /// </summary>
    public DbSet<ClientEntity> Clients => Set<ClientEntity>();

    /// <summary>
    /// Gets the <see cref="ClientSecretEntity"/> entities.
    /// </summary>
    public DbSet<ClientSecretEntity> ClientSecrets => Set<ClientSecretEntity>();

    /// <summary>
    /// Gets the <see cref="GrantEntity"/> entities.
    /// </summary>
    public DbSet<GrantEntity> Grants => Set<GrantEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (
            var relationship in modelBuilder
                .Model.GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys())
        )
        {
            relationship.DeleteBehavior = DeleteBehavior.Restrict;
        }

        // A tenant's domain name is optional; enforce uniqueness only over non-null values (a filtered index) so
        // that multiple tenants may omit it. The in-memory provider ignores the filter and does not enforce indexes.
        modelBuilder
            .Entity<TenantEntity>()
            .HasIndex(entity => entity.NormalizedDomainName)
            .IsUnique()
            .HasFilter("NormalizedDomainName IS NOT NULL");

        // Tenant-scoping global query filters: when an ambient tenant scope is active, tenant-child resources from
        // other tenants are never materialized (a cross-tenant row cannot be fetched, let alone returned). When no
        // scope is active (central-admin surfaces / runtime), NormalizedAmbientTenantId is null and the filter is a
        // no-op. Tenant-owned families (the tenant itself and its secrets) are intentionally not scoped.
        modelBuilder
            .Entity<ClientEntity>()
            .HasQueryFilter(entity =>
                NormalizedAmbientTenantId == null
                || entity.Tenant.NormalizedTenantId == NormalizedAmbientTenantId
            );

        modelBuilder
            .Entity<ClientSecretEntity>()
            .HasQueryFilter(entity =>
                NormalizedAmbientTenantId == null
                || entity.Tenant.NormalizedTenantId == NormalizedAmbientTenantId
            );

        // A grant's tenant is optional: a tenant-scoped surface sees only its tenant's grants, while non-tenant
        // (global) grants are visible only to central-admin (an unset ambient tenant).
        modelBuilder
            .Entity<GrantEntity>()
            .HasQueryFilter(entity =>
                NormalizedAmbientTenantId == null
                || (
                    entity.Tenant != null
                    && entity.Tenant.NormalizedTenantId == NormalizedAmbientTenantId
                )
            );
    }

    /// <inheritdoc />
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(new ConcurrencyTokenSaveChangesInterceptor());
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<DateTimeConverter>();

        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetConverter>();

        configurationBuilder.Properties<JsonElement>().HaveConversion<JsonElementConverter>();

        configurationBuilder.Conventions.Add(_ => this.GetService<UseIdGeneratorConvention>());
    }
}
