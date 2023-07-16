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

internal abstract class PropertyEntityTypeConfiguration<T> : IEntityTypeConfiguration<T>
    where T : PropertyEntity
{
    protected abstract void PreConfigure(EntityTypeBuilder<T> builder);

    protected virtual void PostConfigure(EntityTypeBuilder<T> builder)
    {
        // nothing
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<T> builder)
    {
        PreConfigure(builder);

        builder.HasKey(_ => _.Id);

        builder.Property(_ => _.Id).UseIdGenerator();
        builder.Property(_ => _.CodeName).AsStandardIndex();
        builder.Property(_ => _.NormalizedCodeName).AsStandardIndex();
        builder.Property(_ => _.JsonValue).AsStandardString();

        PostConfigure(builder);
    }
}