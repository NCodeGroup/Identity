#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdMessage"/> that uses strongly-typed <see cref="Parameter"/> and
/// <see cref="KnownParameter"/> objects for <c>OAuth</c> or <c>OpenId Connect</c> parameters.
/// </summary>
public abstract class OpenIdMessage : IOpenIdMessage
{
    [MemberNotNullWhen(true, nameof(ContextOrNull))]
    [MemberNotNullWhen(true, nameof(ParametersOrNull))]
    private bool IsInitialized { get; set; }

    private IOpenIdContext? ContextOrNull { get; set; }

    private Dictionary<string, Parameter>? ParametersOrNull { get; set; }

    private static Exception GetNotInitializedException() => new InvalidOperationException("Not initialized");

    /// <inheritdoc />
    public IOpenIdContext OpenIdContext => ContextOrNull ?? throw GetNotInitializedException();

    /// <summary>
    /// Gets the collection of strong-typed <c>OAuth</c> or <c>OpenId Connect</c> parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    public IReadOnlyDictionary<string, Parameter> Parameters => ParametersOrNull ?? throw GetNotInitializedException();

    /// <summary>
    /// Initializes the current instance with an <see cref="IOpenIdContext"/> and collection of <see cref="Parameter"/> values.
    /// </summary>
    /// <param name="context"><see cref="IOpenIdContext"/></param>
    /// <param name="parameters">The collection of <see cref="Parameter"/> values.</param>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is already initialized.</exception>
    protected internal void Initialize(IOpenIdContext context, IEnumerable<Parameter> parameters)
    {
        if (IsInitialized) throw new InvalidOperationException("Already initialized");

        ContextOrNull = context;
        ParametersOrNull = parameters.ToDictionary(
            parameter => parameter.Descriptor.ParameterName,
            StringComparer.OrdinalIgnoreCase);

        IsInitialized = true;
    }

    /// <inheritdoc />
    public int Count => Parameters.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => Parameters.Keys;

    /// <inheritdoc />
    public IEnumerable<StringValues> Values => Parameters.Values.Select(_ => _.StringValues);

    /// <inheritdoc />
    public StringValues this[string key] => Parameters[key].StringValues;

    /// <inheritdoc />
    public bool ContainsKey(string key) => Parameters.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value)
    {
        if (!Parameters.TryGetValue(key, out var parameter))
        {
            value = default;
            return false;
        }

        value = parameter.StringValues;
        return true;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => Parameters
        .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.StringValues))
        .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Gets the value of well known parameter.
    /// </summary>
    /// <param name="knownParameter">The descriptor of the well known parameter.</param>
    /// <typeparam name="T">The data type of the well known parameter.</typeparam>
    /// <returns>The value of the well known parameter.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    protected internal T? GetKnownParameter<T>(KnownParameter<T> knownParameter) =>
        Parameters.TryGetValue(knownParameter.Name, out var parameter) &&
        parameter is Parameter<T> typedParameter ?
            typedParameter.ParsedValue :
            default;

    /// <summary>
    /// Sets the value of the well known parameter.
    /// </summary>
    /// <param name="knownParameter">The descriptor of the well known parameter.</param>
    /// <param name="parsedValue">The value of the well known parameter.</param>
    /// <typeparam name="T">The data type of the well known parameter.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    protected internal void SetKnownParameter<T>(KnownParameter<T> knownParameter, T? parsedValue)
    {
        if (!IsInitialized) throw GetNotInitializedException();

        var parameterName = knownParameter.Name;
        if (parsedValue is null)
        {
            ParametersOrNull.Remove(parameterName);
            return;
        }

        var stringValues = knownParameter.Parser.Serialize(ContextOrNull, parsedValue);
        if (StringValues.IsNullOrEmpty(stringValues))
        {
            ParametersOrNull.Remove(parameterName);
            return;
        }

        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = descriptor.Loader.Load(ContextOrNull, descriptor, stringValues, parsedValue);
        ParametersOrNull[knownParameter.Name] = parameter;
    }
}

/// <summary>
/// Provides a generic implementation of <see cref="OpenIdMessage"/>.
/// </summary>
/// <typeparam name="T">The type of <c>OAuth</c> or <c>OpenId Connect</c> message.</typeparam>
public abstract class OpenIdMessage<T> : OpenIdMessage
    where T : OpenIdMessage, new()
{
    /// <summary>
    /// Create and initializes an <c>OAuth</c> or <c>OpenId Connect</c> message by using a collection of <see cref="Parameter{T}"/> values.
    /// </summary>
    /// <param name="context"><see cref="IOpenIdContext"/></param>
    /// <param name="parameters">The collection of <see cref="Parameter"/> values.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(IOpenIdContext context, IEnumerable<Parameter> parameters)
    {
        var message = new T();

        message.Initialize(context, parameters);
        return message;
    }

    /// <summary>
    /// Create and loads an <c>OAuth</c> or <c>OpenId Connect</c> message by parsing <see cref="StringValues"/> key-value pairs into a collection of <see cref="Parameter{T}"/> values.
    /// </summary>
    /// <param name="context"><see cref="IOpenIdContext"/></param>
    /// <param name="properties">The collection of <see cref="StringValues"/> key-value pairs to be parsed into <see cref="Parameter{T}"/> values.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(IOpenIdContext context, IEnumerable<KeyValuePair<string, StringValues>> properties)
    {
        var parameters = properties
            .GroupBy(
                kvp => kvp.Key,
                kvp => kvp.Value.AsEnumerable(),
                StringComparer.OrdinalIgnoreCase)
            .Select(grouping => Parameter.Load(
                context,
                grouping.Key,
                grouping.SelectMany(stringValues => stringValues)));

        return Load(context, parameters);
    }

    /// <summary>
    /// Create and loads an <c>OAuth</c> or <c>OpenId Connect</c> message by cloning an existing message.
    /// </summary>
    /// <param name="other">The <see cref="IOpenIdMessage"/> to clone.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(IOpenIdMessage other)
    {
        return Load(other.OpenIdContext, other);
    }
}
