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

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
[PublicAPI]
public interface IParameter
{
    /// <summary>
    /// Gets the <see cref="ParameterDescriptor"/> that describes this parameter.
    /// </summary>
    public ParameterDescriptor Descriptor { get; init; }

    /// <summary>
    /// Gets the <see cref="StringValues"/> that this parameter was loaded with.
    /// </summary>
    public StringValues StringValues { get; init; }

    /// <summary>
    /// Gets the value that this parameter was parsed with.
    /// </summary>
    public object? GetParsedValue();

    /// <summary>
    /// Clones this <see cref="IParameter"/> instance.
    /// </summary>
    /// <returns>The newly cloned <see cref="IParameter"/> instance.</returns>
    public IParameter Clone();
}

/// <summary>
/// Contains the parsed value and string values from which a parameter was parsed from.
/// </summary>
/// <typeparam name="T">The type of the parameter's parsed value.</typeparam>
[PublicAPI]
public interface IParameter<T> : IParameter
{
    /// <summary>
    /// Gets the value that this parameter was parsed with.
    /// </summary>
    T? ParsedValue { get; init; }
}
