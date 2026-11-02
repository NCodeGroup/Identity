// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Configuration;

/// <summary>
/// Specifies that a property or field should use <see cref="IdValueGenerator"/> for generating identifier values.
/// </summary>
[PublicAPI]
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class UseIdGeneratorAttribute : Attribute
{
    // nothing
}
