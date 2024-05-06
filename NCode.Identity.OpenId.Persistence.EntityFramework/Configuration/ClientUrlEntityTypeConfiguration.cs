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
using NCode.Identity.OpenId.Persistence.EntityFramework.Entities;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

internal class ClientUrlEntityTypeConfiguration : IEntityTypeConfiguration<ClientUrlEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ClientUrlEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.ClientId, e.UrlType, e.UrlValue}).IsUnique();

        builder.Property(e => e.Id).UseIdGenerator();
        builder.Property(e => e.TenantId);
        builder.Property(e => e.ClientId);

        builder.Property(e => e.UrlType).AsStandardString();
        builder.Property(e => e.UrlValue).AsStandardString();

        builder
            .HasOne(e => e.Tenant)
            .WithMany()
            .HasForeignKey(e => e.TenantId)
            .IsRequired();

        builder
            .HasOne(e => e.Client)
            .WithMany(e => e.Urls)
            .HasForeignKey(e => e.ClientId)
            .IsRequired();
    }
}
