// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Represents a strongly typed key in a <see cref="IParameterCollection"/>.
/// </summary>
/// <typeparam name="T">The type of the parameter's value.</typeparam>
[PublicAPI]
public readonly record struct ParameterKey<T>(string ParameterName)
{
    /// <summary>
    /// Gets the type of the parameter's value.
    /// </summary>
    public Type ValueType => typeof(T);
}
