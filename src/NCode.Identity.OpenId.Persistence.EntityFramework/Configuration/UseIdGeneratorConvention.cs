// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

/// <summary>
/// Provides a convention to use <see cref="IdValueGenerator"/> for properties marked with <see cref="UseIdGeneratorAttribute"/>.
/// </summary>
internal class UseIdGeneratorConvention(IdValueGenerator idValueGenerator)
    : IPropertyAddedConvention
{
    private IdValueGenerator IdValueGenerator { get; } = idValueGenerator;

    /// <inheritdoc />
    public void ProcessPropertyAdded(
        IConventionPropertyBuilder propertyBuilder,
        IConventionContext<IConventionPropertyBuilder> context
    )
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

        propertyBuilder.ValueGenerated(ValueGenerated.Never, fromDataAnnotation: true);
        propertyBuilder.HasValueGenerator((_, _) => IdValueGenerator, fromDataAnnotation: true);
    }
}
