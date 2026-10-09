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
        modelBuilder.Entity<ResourceServerEntity>().ToTable("ResourceServers");
        modelBuilder.Entity<ScopeEntity>().ToTable("Scopes");
        modelBuilder.Entity<ClientGrantEntity>().ToTable("ClientGrants");
        modelBuilder.Entity<RoleAssignmentEntity>().ToTable("RoleAssignments");
        modelBuilder.Entity<FederatedPrincipalEntity>().ToTable("FederatedPrincipals");
        modelBuilder.Entity<FederatedIdentityEntity>().ToTable("FederatedIdentities");

        // A tenant's domain name is optional; enforce uniqueness only over non-null values (a filtered index) so
        // that multiple tenants may omit it. The in-memory provider ignores the filter and does not enforce indexes.
        modelBuilder
            .Entity<TenantEntity>()
            .ToTable("Tenants")
            .HasIndex(entity => entity.NormalizedDomainName)
            .IsUnique()
            .HasFilter("NormalizedDomainName IS NOT NULL");
    }
}
