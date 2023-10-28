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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NIdentity.OpenId.Playground.DataLayer.Entities;

namespace NIdentity.OpenId.Playground.DataLayer.Configuration;

internal class ClientEntityTypeConfiguration : IEntityTypeConfiguration<ClientEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.ClientId }).IsUnique();

        builder.Property(e => e.Id).UseIdGenerator();
        builder.Property(e => e.ConcurrencyToken).AsStandardConcurrencyToken();

        builder.Property(e => e.TenantId).AsStandardIndex();
        builder.Property(e => e.ClientId).AsStandardIndex();

        builder.Property(e => e.IsDisabled);
        builder.Property(e => e.AllowUnsafeTokenResponse);
        builder.Property(e => e.AllowLoopback);
        builder.Property(e => e.RequireRequestObject);
        builder.Property(e => e.RequirePkce);
        builder.Property(e => e.AllowPlainCodeChallengeMethod);

        builder
            .HasMany(e => e.ClientSecrets)
            .WithOne(e => e.Client)
            .HasForeignKey(e => e.ClientId)
            .IsRequired();

        builder
            .HasMany(e => e.Urls)
            .WithOne()
            .HasForeignKey(e => e.ClientId)
            .IsRequired();

        builder
            .HasMany(e => e.Properties)
            .WithOne(e => e.Client)
            .HasForeignKey(e => e.ClientId)
            .IsRequired();
    }
}
