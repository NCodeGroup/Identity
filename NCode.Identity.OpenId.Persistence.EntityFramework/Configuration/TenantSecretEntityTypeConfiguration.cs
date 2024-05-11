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

internal class TenantSecretEntityTypeConfiguration : IEntityTypeConfiguration<TenantSecretEntity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TenantSecretEntity> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasIndex(e => new { e.TenantId, e.SecretId }).IsUnique();

        builder.Property(e => e.Id).UseIdGenerator();
        builder.Property(e => e.TenantId);
        builder.Property(e => e.SecretId);

        builder
            .HasOne(e => e.Tenant)
            .WithMany(e => e.Secrets)
            .HasForeignKey(e => e.TenantId)
            .IsRequired();

        builder
            .HasOne(e => e.Secret)
            .WithMany()
            .HasForeignKey(e => e.SecretId)
            .IsRequired();
    }
}
