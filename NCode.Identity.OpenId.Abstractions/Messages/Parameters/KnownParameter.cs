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

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Defines the contract for all known parameters in an <c>OAuth</c> or <c>OpenID Connect</c> message.
/// </summary>
[PublicAPI]
public abstract class KnownParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnownParameter"/> class.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="loader">The <see cref="IParameterLoader"/> that can be used to parse and load <see cref="IParameter"/> values.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <c>null</c> or empty.</exception>
    protected KnownParameter(string name, IParameterLoader loader)
    {
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
    /// Gets a value indicating whether the parameter allows missing string values when parsing.
    /// The default value is <c>false</c>.
    /// </summary>
    public required bool AllowMissingStringValues { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter should sort string values when serializing.
    /// The default value is <c>false</c>.
    /// </summary>
    public bool SortStringValues { get; init; }

    /// <summary>
    /// Gets a value indicating whether the parameter allows multiple string values when parsing.
    /// The default value is <c>false</c>.
    /// </summary>
    public bool AllowMultipleStringValues { get; init; }

    /// <summary>
    /// Gets a value indicating which serialization formats are ignored for the parameter.
    /// The default value is <see cref="SerializationFormats.None"/>.
    /// </summary>
    public SerializationFormats IgnoredSerializationFormats { get; init; }

    /// <summary>
    /// Gets the <see cref="IParameterLoader"/> that can be used to parse and load <see cref="IParameter"/> values.
    /// </summary>
    public IParameterLoader Loader { get; }
}

/// <summary>
/// Defines the contract for all known parameters in an <c>OAuth</c> or <c>OpenID Connect</c> message.
/// </summary>
/// <typeparam name="T">The type of the parameter's parsed value.</typeparam>
[PublicAPI]
public class KnownParameter<T> : KnownParameter
{
    /// <summary>
    /// Initializes a new instance of the <see cref="KnownParameter{T}"/> class.
    /// </summary>
    /// <param name="name">The name of the parameter.</param>
    /// <param name="parser">The <see cref="IParameterParser{T}"/> that can be used to parse and load <see cref="IParameter"/> values.</param>
    public KnownParameter(string name, IParameterParser<T> parser)
        : base(name, parser)
    {
        Parser = parser;
    }

    /// <inheritdoc/>
    public override Type ValueType => typeof(T);

    /// <summary>
    /// Gets the <see cref="IParameterParser{T}"/> that can be used to parse and load <see cref="IParameter"/> values.
    /// </summary>
    public IParameterParser<T> Parser { get; }
}
