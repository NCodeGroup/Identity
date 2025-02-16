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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Contains the parsed value and string values from which a parameter was loaded from.
/// </summary>
[PublicAPI]
public interface IParameter
{
    /// <summary>
    /// Gets the <see cref="ParameterDescriptor"/> that describes this parameter.
    /// </summary>
    public ParameterDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the formatted (aka openid) representation of this parameter.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> to use while formatting the parameter.</param>
    public StringValues GetStringValues(OpenIdEnvironment openIdEnvironment);

    /// <summary>
    /// Clones this <see cref="IParameter"/> instance.
    /// </summary>
    /// <returns>The newly cloned <see cref="IParameter"/> instance.</returns>
    public IParameter Clone();
}

/// <summary>
/// Contains the parsed value and string values from which a parameter was loaded from.
/// </summary>
/// <typeparam name="T">The type of the parameter's parsed value.</typeparam>
[PublicAPI]
public interface IParameter<T> : IParameter
{
    /// <summary>
    /// Gets the <see cref="IParameterParser{T}"/> that is used to parse and load this parameter.
    /// </summary>
    IParameterParser<T> Parser { get; }

    /// <summary>
    /// Gets the value that this parameter was parsed with.
    /// </summary>
    T? ParsedValue { get; }
}
