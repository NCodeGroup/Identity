#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId.Playground.DataLayer.Configuration;

internal static class ModelBuilderExtensions
{
    private const string KeyUseIdGenerator = "NIdentity:UseIdGenerator";

    public static void UseIdGenerator(this PropertyBuilder<long> builder, bool enable = true)
    {
        builder.ValueGeneratedNever().HasAnnotation(KeyUseIdGenerator, enable);
    }

    public static PropertyBuilder<string> AsStandardString(this PropertyBuilder<string> builder)
    {
        return builder.IsRequired().IsUnicode(false);
    }

    public static void AsStandardIndex(this PropertyBuilder<string> builder)
    {
        builder.AsStandardString().HasMaxLength(DataConstants.MaxIndexLength);
    }

    public static void AsStandardConcurrencyToken(this PropertyBuilder<string> builder)
    {
        builder.AsStandardString().HasMaxLength(DataConstants.MaxConcurrencyTokenLength).IsConcurrencyToken();
    }

    public static void UseIdGenerator(this ModelBuilder builder, IdValueGenerator idValueGenerator)
    {
        var properties = builder.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties()
                .Where(property => property.GetAnnotations()
                    .Any(annotation => annotation.Name == KeyUseIdGenerator &&
                                       annotation.Value as bool? == true)));

        foreach (var property in properties)
        {
            property.SetValueGeneratorFactory((_, _) => idValueGenerator);
        }
    }

    public static void UseUtcDateTime(this ModelBuilder builder)
    {
        // always persist DateTime values as UTC
        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            value => value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        var properties = builder.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties()
                .Where(property => typeof(DateTime?).IsAssignableFrom(property.ClrType) &&
                                   property.GetValueConverter() == null));

        foreach (var property in properties)
        {
            property.SetValueConverter(dateTimeConverter);
        }
    }
}