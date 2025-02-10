#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Provides the ability to convert between <see cref="StringValues"/> and <see cref="string"/>.
/// </summary>
[PublicAPI]
public interface IStringValuesConverter
{
    /// <summary>
    /// Converts the specified OpenId string value to an <see cref="StringValues"/> instance.
    /// </summary>
    /// <param name="value">The OpenId string value to convert.</param>
    /// <returns>The <see cref="StringValues"/> instance.</returns>
    StringValues ToStringValues(string? value);

    /// <summary>
    /// Converts the specified <see cref="StringValues"/> instance to a formatted OpenId string value.
    /// </summary>
    /// <param name="stringValues">The <see cref="StringValues"/> to convert.</param>
    /// <returns>The formatted OpenId string value.</returns>
    string? FromStringValues(StringValues stringValues);
}

/// <summary>
/// Provides a default implementation of the <see cref="IStringValuesConverter"/> abstraction.
/// </summary>
[PublicAPI]
public class StringValuesConverter : IStringValuesConverter
{
    /// <summary>
    /// Gets a singleton instance of the <see cref="StringValuesConverter"/>.
    /// </summary>
    public static StringValuesConverter Singleton { get; } = new();

    /// <inheritdoc />
    public StringValues ToStringValues(string? value) =>
        value switch
        {
            null => StringValues.Empty,
            _ => new StringValues(value.Split(OpenIdConstants.ParameterSeparatorChar))
        };

    /// <inheritdoc />
    public string? FromStringValues(StringValues stringValues) =>
        stringValues.Count switch
        {
            0 => null,
            1 => stringValues[0],
            _ => string.Join(OpenIdConstants.ParameterSeparatorChar, stringValues.AsEnumerable())
        };
}
