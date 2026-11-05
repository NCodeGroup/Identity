// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core.Entities;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Core;

/// <summary>
/// Contributes the core OpenID schema (clients, grants, tenants, secrets, master keys, resource servers, scopes, roles,
/// the platform, teams, and federated principals/identities) to the shared <see cref="OpenIdDbContext"/> model. Because
/// the framework context is <c>DbSet</c>-less, each core entity is added to the model here; the framework's
/// schema-agnostic passes (restrict-all-foreign-keys and the tenant-scope filter over every <see
/// cref="NCode.Identity.OpenId.Persistence.EntityFramework.Entities.ISupportTenantEntity"/>) then cover them.
/// </summary>
internal sealed class CoreModelContributor : IOpenIdModelContributor
{
    /// <inheritdoc />
    public void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SecretEntity>().ToTable("Secrets");
        modelBuilder.Entity<MasterKeyEntity>().ToTable("MasterKeys");
        modelBuilder.Entity<OwnerRootKeyEntity>().ToTable("OwnerRootKeys");
        modelBuilder.Entity<PlatformEntity>().ToTable("Platform");
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
        modelBuilder.Entity<TeamEntity>().ToTable("Teams");
        modelBuilder.Entity<RetiredTeamEntity>().ToTable("RetiredTeams");
        modelBuilder.Entity<RetiredTenantEntity>().ToTable("RetiredTenants");
        modelBuilder.Entity<RetiredIssuerEntity>().ToTable("RetiredIssuers");
        modelBuilder.Entity<TenantUsageEntity>().ToTable("TenantUsage");
        modelBuilder.Entity<TenantUsageCheckpointEntity>().ToTable("TenantUsageCheckpoints");
        modelBuilder.Entity<AuditRecordEntity>().ToTable("AuditRecords");
        modelBuilder.Entity<AuditRecordResourceEntity>().ToTable("AuditRecordResources");

        // An owner has at most one active master key (the one with no retirement time), so enforce uniqueness only
        // over the active rows (a filtered index): two replicas that race to create an owner's first key cannot both
        // succeed, and the loser reloads the winner's key. Retired keys are unconstrained here (their versions are
        // unique through the owner-and-version index). The in-memory provider ignores the filter and does not enforce
        // indexes. Quote the column so the raw filter is portable: Postgres folds unquoted identifiers to lower case
        // (the column is PascalCase), while SQLite treats the quoted name as the same identifier.
        modelBuilder
            .Entity<MasterKeyEntity>()
            .HasIndex(entity => new { entity.OwnerType, entity.NormalizedOwnerId })
            .IsUnique()
            .HasFilter("\"RetiredWhen\" IS NULL")
            .HasDatabaseName("IX_MasterKeys_OwnerType_NormalizedOwnerId_Active");

        // The background re-validation scan pages the owner root keys that are due by state and by the time of the last
        // successful or failed probe, so index each of those two reads on the state first.
        modelBuilder
            .Entity<OwnerRootKeyEntity>()
            .HasIndex(entity => new { entity.State, entity.ValidatedWhen });
        modelBuilder
            .Entity<OwnerRootKeyEntity>()
            .HasIndex(entity => new { entity.State, entity.FailedWhen });

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
