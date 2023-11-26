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

using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdMessage"/> that uses strongly-typed <see cref="Parameter"/> and
/// <see cref="KnownParameter"/> objects for <c>OAuth</c> or <c>OpenID Connect</c> parameters.
/// </summary>
public class OpenIdMessage : IOpenIdMessage
{
    private static Exception NotInitializedException => new InvalidOperationException("Not initialized");

    [MemberNotNullWhen(true, nameof(ContextOrNull), nameof(ParameterStoreOrNull))]
    private bool IsInitialized { get; set; }

    private OpenIdContext? ContextOrNull { get; set; }
    private Dictionary<string, Parameter>? ParameterStoreOrNull { get; set; }
    private Dictionary<string, Parameter> ParameterStore => ParameterStoreOrNull ??= new Dictionary<string, Parameter>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the collection of strong-typed <c>OAuth</c> or <c>OpenID Connect</c> parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    public IReadOnlyDictionary<string, Parameter> Parameters => ParameterStore;

    /// <summary>
    /// Gets the <see cref="OpenIdContext"/> for the current instance.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    public OpenIdContext OpenIdContext => ContextOrNull ?? throw NotInitializedException;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdMessage"/> class.
    /// </summary>
    public OpenIdMessage()
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdMessage"/> class.
    /// </summary>
    /// <param name="context"><see cref="OpenIdContext"/></param>
    public OpenIdMessage(OpenIdContext context)
    {
        Initialize(context);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdMessage"/> class by using a collection of <see cref="Parameter{T}"/> values.
    /// </summary>
    /// <param name="context"><see cref="OpenIdContext"/></param>
    /// <param name="parameters">The collection of <see cref="Parameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="Parameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    public OpenIdMessage(OpenIdContext context, IEnumerable<Parameter> parameters, bool cloneParameters = false)
    {
        Initialize(context, parameters, cloneParameters);
    }

    /// <summary>
    /// Initializes the current instance with an <see cref="OpenIdContext"/>.
    /// </summary>
    /// <param name="context"><see cref="OpenIdContext"/></param>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is already initialized.</exception>
    public void Initialize(OpenIdContext context)
    {
        if (IsInitialized)
            throw new InvalidOperationException("Already initialized");

        ContextOrNull = context;

        IsInitialized = true;
    }

    /// <summary>
    /// Initializes the current instance with an <see cref="OpenIdContext"/> and collection of <see cref="Parameter"/> values.
    /// </summary>
    /// <param name="context"><see cref="OpenIdContext"/></param>
    /// <param name="parameters">The collection of <see cref="Parameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="Parameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is already initialized.</exception>
    public void Initialize(OpenIdContext context, IEnumerable<Parameter> parameters, bool cloneParameters = false)
    {
        if (IsInitialized)
            throw new InvalidOperationException("Already initialized");

        ContextOrNull = context;
        ParameterStoreOrNull = parameters.ToDictionary(
            parameter => parameter.Descriptor.ParameterName,
            parameter => cloneParameters ? parameter.Clone() : parameter,
            StringComparer.OrdinalIgnoreCase);

        IsInitialized = true;
    }

    /// <inheritdoc />
    public int Count => ParameterStore.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => ParameterStore.Keys;

    /// <inheritdoc />
    public IEnumerable<StringValues> Values => ParameterStore.Values.Select(parameter => parameter.StringValues);

    /// <inheritdoc />
    public StringValues this[string key] => ParameterStore[key].StringValues;

    /// <inheritdoc />
    public bool ContainsKey(string key) => ParameterStore.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value)
    {
        if (!ParameterStore.TryGetValue(key, out var parameter))
        {
            value = default;
            return false;
        }

        value = parameter.StringValues;
        return true;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => ParameterStore
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
        ParameterStore.TryGetValue(knownParameter.Name, out var parameter) &&
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
        if (!IsInitialized) throw NotInitializedException;

        var parameterName = knownParameter.Name;
        if (parsedValue is null)
        {
            ParameterStore.Remove(parameterName);
            return;
        }

        var stringValues = knownParameter.Parser.Serialize(ContextOrNull, parsedValue);
        if (StringValues.IsNullOrEmpty(stringValues))
        {
            ParameterStore.Remove(parameterName);
            return;
        }

        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = descriptor.Loader.Load(ContextOrNull, descriptor, stringValues, parsedValue);
        ParameterStore[knownParameter.Name] = parameter;
    }
}

/// <summary>
/// Provides a generic implementation of <see cref="OpenIdMessage"/>.
/// </summary>
/// <typeparam name="T">The type of <c>OAuth</c> or <c>OpenID Connect</c> message.</typeparam>
public abstract class OpenIdMessage<T> : OpenIdMessage
    where T : OpenIdMessage<T>, new()
{
    /// <inheritdoc />
    protected OpenIdMessage()
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdMessage(OpenIdContext context)
        : base(context)
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdMessage(OpenIdContext context, IEnumerable<Parameter> parameters, bool cloneParameters = false)
        : base(context, parameters, cloneParameters)
    {
        // nothing
    }

    /// <summary>
    /// Create an empty <c>OAuth</c> or <c>OpenID Connect</c> message.
    /// </summary>
    /// <param name="context"><see cref="OpenIdContext"/></param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Create(OpenIdContext context)
    {
        var message = new T();
        message.Initialize(context);
        return message;
    }

    /// <summary>
    /// Create and loads an <c>OAuth</c> or <c>OpenID Connect</c> message by using a collection of <see cref="Parameter{T}"/> values.
    /// </summary>
    /// <param name="context"><see cref="OpenIdContext"/></param>
    /// <param name="parameters">The collection of <see cref="Parameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="Parameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(OpenIdContext context, IEnumerable<Parameter> parameters, bool cloneParameters = false)
    {
        var message = new T();
        message.Initialize(context, parameters, cloneParameters);
        return message;
    }

    /// <summary>
    /// Create and loads an <c>OAuth</c> or <c>OpenID Connect</c> message by parsing <see cref="StringValues"/> key-value pairs into a collection of <see cref="Parameter{T}"/> values.
    /// </summary>
    /// <param name="context"><see cref="OpenIdContext"/></param>
    /// <param name="properties">The collection of <see cref="StringValues"/> key-value pairs to be parsed into <see cref="Parameter{T}"/> values.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(OpenIdContext context, IEnumerable<KeyValuePair<string, StringValues>> properties) =>
        Load(context, properties.Select(property => Parameter.Load(context, property.Key, property.Value)));

    /// <summary>
    /// Clones an existing <c>OAuth</c> or <c>OpenID Connect</c> message by parsing it's parameters using their original string-values.
    /// </summary>
    /// <param name="other">The <see cref="IOpenIdMessage"/> to clone.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(IOpenIdMessage other) => Load(other.OpenIdContext, other);
}

/// <summary>
/// Provides a generic implementation of <see cref="OpenIdMessage"/> that has additional properties to be included in JSON serialization.
/// </summary>
/// <typeparam name="T">The type of <c>OAuth</c> or <c>OpenID Connect</c> message.</typeparam>
/// <typeparam name="TProperties">The type that contains the additional properties for JSON serialization.</typeparam>
public abstract class OpenIdMessage<T, TProperties> : OpenIdMessage<T>, ISupportProperties
    where T : OpenIdMessage<T, TProperties>, new()
    where TProperties : class, new()
{
    /// <summary>
    /// Provides storage for any properties that are to be included in JSON serialization.
    /// </summary>
    protected TProperties Properties { get; private set; } = new();

    /// <inheritdoc />
    protected OpenIdMessage()
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdMessage(OpenIdContext context)
        : base(context)
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdMessage(OpenIdContext context, IEnumerable<Parameter> parameters, bool cloneParameters = false)
        : base(context, parameters, cloneParameters)
    {
        // nothing
    }

    void ISupportProperties.SerializeProperties(Utf8JsonWriter writer, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, Properties, options);
    }

    void ISupportProperties.DeserializeProperties(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        Properties = JsonSerializer.Deserialize<TProperties>(ref reader, options) ?? new TProperties();
    }
}
