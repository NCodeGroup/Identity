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

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Converters;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

/// <summary>
/// Contains the entity framework <see cref="DbContext"/> for <c>OAuth</c> and <c>OpenID Connect</c> entities.
/// </summary>
[PublicAPI]
public class OpenIdDbContext(
    DbContextOptions<OpenIdDbContext> options
) : DbContext(options)
{
    /// <summary>
    /// Gets or sets the <see cref="SecretEntity"/> entities.
    /// </summary>
    public DbSet<SecretEntity> Secrets { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="TenantEntity"/> entities.
    /// </summary>
    public DbSet<TenantEntity> Tenants { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="TenantSecretEntity"/> entities.
    /// </summary>
    public DbSet<TenantSecretEntity> TenantSecrets { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="ClientEntity"/> entities.
    /// </summary>
    public DbSet<ClientEntity> Clients { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="ClientUrlEntity"/> entities.
    /// </summary>
    public DbSet<ClientUrlEntity> ClientUrls { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="ClientSecretEntity"/> entities.
    /// </summary>
    public DbSet<ClientSecretEntity> ClientSecrets { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="GrantEntity"/> entities.
    /// </summary>
    public DbSet<GrantEntity> Grants { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // nothing
    }

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTime>()
            .HaveConversion<DateTimeConverter>();

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();

        configurationBuilder.Conventions.Add(serviceProvider =>
            serviceProvider.GetRequiredService<UseIdGeneratorConvention>());
    }
}
