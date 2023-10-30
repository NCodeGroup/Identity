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

using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Messages.Parameters;

/// <summary>
/// Defines the contract for all known parameters in an <c>OAuth</c> or <c>OpenID Connect</c> message.
/// </summary>
public abstract class KnownParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnownParameter"/> class.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="loader">The <see cref="ParameterLoader"/> that can be used to parse and return a <see cref="Parameter"/> given <see cref="StringValues"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c> or empty.</exception>
    protected KnownParameter(string name, ParameterLoader loader)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));

        Name = name;
        Loader = loader;
    }

    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the type of the parameter's parsed value.
    /// </summary>
    public abstract Type ValueType { get; }

    /// <summary>
    /// Gets a value indicating whether the parameter is optional or required when parsing.
    /// The default value is <c>false</c>.
    /// </summary>
    public bool Optional { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter allows multiple values when parsing.
    /// The default value is <c>false</c>.
    /// </summary>
    public bool AllowMultipleValues { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter allows unrecognized values when parsing.
    /// The default value is <c>false</c>.
    /// </summary>
    public bool IgnoreUnrecognizedValues { get; init; }

    /// <summary>
    /// Gets the <see cref="ParameterLoader"/> that can be used to parse and return a <see cref="Parameter"/> given <see cref="StringValues"/>.
    /// </summary>
    public ParameterLoader Loader { get; }
}

/// <summary>
/// Defines the contract for all known parameters in an <c>OAuth</c> or <c>OpenID Connect</c> message.
/// </summary>
/// <typeparam name="T">The type of the parameter's parsed value.</typeparam>
public class KnownParameter<T> : KnownParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnownParameter{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="parser">The <see cref="ParameterParser{T}"/> that can be used to parse and return a <see cref="Parameter"/> given <see cref="StringValues"/>.</param>
    public KnownParameter(string name, ParameterParser<T> parser)
        : base(name, parser)
    {
        Parser = parser;
    }

    /// <inheritdoc/>
    public override Type ValueType => typeof(T);

    /// <summary>
    /// Gets the <see cref="ParameterParser{T}"/> that can be used to parse and return a <see cref="Parameter"/> given <see cref="StringValues"/>.
    /// </summary>
    public ParameterParser<T> Parser { get; }
}
