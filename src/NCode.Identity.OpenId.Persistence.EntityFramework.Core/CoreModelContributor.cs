#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core;

/// <summary>
/// Contributes the core OpenID schema (clients, grants, tenants, secrets, resource servers, scopes, roles, servers, and
/// federated principals/identities) to the shared <see cref="OpenIdDbContext"/> model. Because the framework context is
/// <c>DbSet</c>-less, each core entity is added to the model here; the framework's schema-agnostic passes
/// (restrict-all-foreign-keys and the tenant-scope filter over every
/// <see cref="NCode.Identity.OpenId.Persistence.EntityFramework.Entities.ISupportTenantEntity"/>) then cover
/// them.
/// </summary>
internal sealed class CoreModelContributor : IOpenIdModelContributor
{
    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecretEntity>().ToTable("Secrets");
        modelBuilder.Entity<ServerEntity>().ToTable("Servers");
        modelBuilder.Entity<ServerSecretEntity>().ToTable("ServerSecrets");
        modelBuilder.Entity<TenantSecretEntity>().ToTable("TenantSecrets");
        modelBuilder.Entity<ClientEntity>().ToTable("Clients");
        modelBuilder.Entity<ClientSecretEntity>().ToTable("ClientSecrets");
        modelBuilder.Entity<GrantEntity>().ToTable("Grants");
        modelBuilder.Entity<ConsentEntity>().ToTable("Consents");
        modelBuilder.Entity<ResourceServerEntity>().ToTable("ResourceServers");
        modelBuilder.Entity<ResourceServerSecretEntity>().ToTable("ResourceServerSecrets");
        modelBuilder.Entity<ScopeEntity>().ToTable("Scopes");
        modelBuilder.Entity<ClientGrantEntity>().ToTable("ClientGrants");
        modelBuilder.Entity<RoleAssignmentEntity>().ToTable("RoleAssignments");
        modelBuilder.Entity<FederatedPrincipalEntity>().ToTable("FederatedPrincipals");
        modelBuilder.Entity<FederatedIdentityEntity>().ToTable("FederatedIdentities");
        modelBuilder.Entity<RetiredTenantEntity>().ToTable("RetiredTenants");
        modelBuilder.Entity<RetiredIssuerEntity>().ToTable("RetiredIssuers");
        modelBuilder.Entity<TenantUsageEntity>().ToTable("TenantUsage");

        // A tenant's issuer is optional (a tenant without one derives its origin from the request), so enforce
        // uniqueness only over non-null values (a filtered index). Two tenants sharing an issuer is a protocol-level
        // collision: the issuer IS the tenant's identity, and every token it mints asserts it. The in-memory provider
        // ignores the filter and does not enforce indexes. Quote the column so the raw filter is portable: Postgres
        // folds unquoted identifiers to lower case (the column is PascalCase), while SQLite treats the quoted name as
        // the same identifier.
        modelBuilder
            .Entity<TenantEntity>()
            .ToTable("Tenants")
            .HasIndex(entity => entity.NormalizedIssuer)
            .IsUnique()
            .HasFilter("\"NormalizedIssuer\" IS NOT NULL");

        // The routing key for the host-based strategy. NOT unique: path-based tenants legitimately share a host
        // (https://op.example/tenant-a, https://op.example/tenant-b). Host uniqueness is a requirement of the
        // host-based strategy only, so it is enforced by validation rather than by the schema.
        modelBuilder
            .Entity<TenantEntity>()
            .HasIndex(entity => entity.NormalizedIssuerHost)
            .HasFilter("\"NormalizedIssuerHost\" IS NOT NULL");

        // The second routing key for the host-based strategy: the host of the tenant's mutual-TLS endpoint-alias
        // authority (RFC 8705 §5). NOT unique, for the same reason as the issuer host.
        modelBuilder
            .Entity<TenantEntity>()
            .HasIndex(entity => entity.NormalizedMtlsEndpointAliasHost)
            .HasFilter("\"NormalizedMtlsEndpointAliasHost\" IS NOT NULL");
    }
}
