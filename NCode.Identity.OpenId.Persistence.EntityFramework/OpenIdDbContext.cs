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

using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;
using NCode.Identity.OpenId.Persistence.EntityFramework.Converters;
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

namespace NCode.Identity.OpenId.Persistence.EntityFramework;

internal class OpenIdDbContext(
    DbContextOptions<OpenIdDbContext> options,
    IdValueGenerator idValueGenerator
) : DbContext(options)
{
    private IdValueGenerator IdValueGenerator { get; } = idValueGenerator;

    public DbSet<SecretEntity> Secrets { get; set; }

    public DbSet<TenantEntity> Tenants { get; set; }

    public DbSet<TenantSecretEntity> TenantSecrets { get; set; }

    public DbSet<ClientEntity> Clients { get; set; }

    public DbSet<ClientUrlEntity> ClientUrls { get; set; }

    public DbSet<ClientSecretEntity> ClientSecrets { get; set; }

    public DbSet<GrantEntity> Grants { get; set; }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.UseIdGenerator(IdValueGenerator);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<DateTime>()
            .HaveConversion<DateTimeConverter>();

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetConverter>();
    }
}
