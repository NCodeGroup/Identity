// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    /// Gets the formatted (<c>openid</c>) representation of this parameter.
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
