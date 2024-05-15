#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

/// <summary>
/// Provides a convention to use <see cref="IdValueGenerator"/> for properties marked with <see cref="UseIdGeneratorAttribute"/>.
/// </summary>
public class UseIdGeneratorConvention(
    IdValueGenerator idValueGenerator
) : IPropertyAddedConvention
{
    private IdValueGenerator IdValueGenerator { get; } = idValueGenerator;

    /// <inheritdoc />
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context)
    {
        var metadata = propertyBuilder.Metadata;
        var memberInfo = metadata.PropertyInfo ?? (MemberInfo?)metadata.FieldInfo;
        if (memberInfo == null)
        {
            return;
        }

        if (!Attribute.IsDefined(memberInfo, typeof(UseIdGeneratorAttribute), inherit: true))
        {
            return;
        }

        propertyBuilder.HasValueGenerator((_, _) => IdValueGenerator, fromDataAnnotation: true);
    }
}
