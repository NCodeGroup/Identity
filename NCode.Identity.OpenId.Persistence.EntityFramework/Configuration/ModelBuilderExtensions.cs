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

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

internal static class ModelBuilderExtensions
{
    private const string UseIdGeneratorKey = "NCode.Identity.OpenId:UseIdGenerator";

    public static void UseIdGenerator(this PropertyBuilder<long> builder, bool enable = true)
    {
        builder.ValueGeneratedNever().HasAnnotation(UseIdGeneratorKey, enable);
    }

    public static void UseIdGenerator(this ModelBuilder builder, IdValueGenerator idValueGenerator)
    {
        var properties = builder.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetProperties()
                .Where(property => property.GetAnnotations()
                    .Any(annotation => annotation.Name == UseIdGeneratorKey &&
                                       annotation.Value as bool? == true)));

        foreach (var property in properties)
        {
            property.SetValueGeneratorFactory((_, _) => idValueGenerator);
        }
    }
}
