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
/// Provides typed <see cref="DbSet{TEntity}"/> accessors for the core OpenID schema over the framework's
/// <c>DbSet</c>-less <see cref="OpenIdDbContext"/>. The accessors live with the slice that owns the entities,
/// so the framework context stays schema-agnostic while core store code keeps its readable table access.
/// </summary>
internal static class CoreDbContextExtensions
{
    extension(OpenIdDbContext context)
    {
        public DbSet<SecretEntity> Secrets => context.Set<SecretEntity>();

        public DbSet<ServerEntity> Servers => context.Set<ServerEntity>();

        public DbSet<ServerSecretEntity> ServerSecrets => context.Set<ServerSecretEntity>();

        public DbSet<TenantEntity> Tenants => context.Set<TenantEntity>();

        public DbSet<TenantSecretEntity> TenantSecrets => context.Set<TenantSecretEntity>();

        public DbSet<ClientEntity> Clients => context.Set<ClientEntity>();

        public DbSet<ClientSecretEntity> ClientSecrets => context.Set<ClientSecretEntity>();

        public DbSet<GrantEntity> Grants => context.Set<GrantEntity>();

        public DbSet<ResourceServerEntity> ResourceServers => context.Set<ResourceServerEntity>();

        public DbSet<ScopeEntity> Scopes => context.Set<ScopeEntity>();

        public DbSet<ClientGrantEntity> ClientGrants => context.Set<ClientGrantEntity>();

        public DbSet<RoleAssignmentEntity> RoleAssignments => context.Set<RoleAssignmentEntity>();

        public DbSet<FederatedPrincipalEntity> FederatedPrincipals =>
            context.Set<FederatedPrincipalEntity>();

        public DbSet<FederatedIdentityEntity> FederatedIdentities =>
            context.Set<FederatedIdentityEntity>();
    }
}
