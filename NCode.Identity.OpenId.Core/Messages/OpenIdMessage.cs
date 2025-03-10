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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a default implementation of <see cref="IOpenIdMessage"/> that uses strongly-typed <see cref="Parameter"/> and
/// <see cref="KnownParameter"/> objects for <c>OAuth</c> or <c>OpenID Connect</c> parameters.
/// </summary>
[PublicAPI]
public class OpenIdMessage : IOpenIdMessage, ISupportClone<IOpenIdMessage>
{
    private static Exception NotInitializedException => new InvalidOperationException("Not initialized");

    [MemberNotNullWhen(true, nameof(OpenIdEnvironmentOrNull), nameof(ParametersOrNull))]
    private bool IsInitialized { get; set; }

    private OpenIdEnvironment? OpenIdEnvironmentOrNull { get; set; }
    private ParameterCollection? ParametersOrNull { get; set; }

    /// <inheritdoc />
    public virtual string TypeDiscriminator => nameof(OpenIdMessage);

    /// <inheritdoc />
    public SerializationFormat SerializationFormat { get; set; }

    /// <inheritdoc />
    public OpenIdEnvironment OpenIdEnvironment => OpenIdEnvironmentOrNull ?? throw NotInitializedException;

    /// <inheritdoc />
    public IParameterCollection Parameters => ParametersOrNull ?? throw NotInitializedException;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdMessage"/> class.
    /// </summary>
    public OpenIdMessage()
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the current class that is a clone of another instance.
    /// </summary>
    /// <param name="other">The other instance to clone.</param>
    protected OpenIdMessage(OpenIdMessage other)
        : this(other.OpenIdEnvironment, other.Parameters, cloneParameters: true)
    {
        SerializationFormat = other.SerializationFormat;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdMessage"/> class.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    public OpenIdMessage(OpenIdEnvironment openIdEnvironment)
    {
        Initialize(openIdEnvironment);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdMessage"/> class by using a collection of <see cref="Parameter{T}"/> values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <param name="parameters">The collection of <see cref="Parameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="Parameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    public OpenIdMessage(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false)
    {
        Initialize(openIdEnvironment, parameters, cloneParameters);
    }

    /// <summary>
    /// Initializes the current instance with an <see cref="OpenIdEnvironment"/>.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is already initialized.</exception>
    public void Initialize(OpenIdEnvironment openIdEnvironment)
    {
        if (IsInitialized)
            throw new InvalidOperationException("Already initialized");

        OpenIdEnvironmentOrNull = openIdEnvironment;
        ParametersOrNull = new ParameterCollection();

        IsInitialized = true;
    }

    /// <summary>
    /// Initializes the current instance with an <see cref="OpenIdEnvironment"/> and collection of <see cref="IParameter"/> values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <param name="parameters">The collection of <see cref="IParameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="IParameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is already initialized.</exception>
    public void Initialize(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false)
    {
        if (IsInitialized)
            throw new InvalidOperationException("Already initialized");

        OpenIdEnvironmentOrNull = openIdEnvironment;
        ParametersOrNull = new ParameterCollection(parameters, cloneParameters);

        IsInitialized = true;
    }

    /// <inheritdoc />
    public virtual IOpenIdMessage CloneMessage() => new OpenIdMessage(this);

    /// <inheritdoc />
    IOpenIdMessage ISupportClone<IOpenIdMessage>.Clone() => CloneMessage();

    /// <summary>
    /// Gets the value of well known parameter.
    /// </summary>
    /// <param name="knownParameter">The descriptor of the well known parameter.</param>
    /// <typeparam name="T">The data type of the well known parameter.</typeparam>
    /// <returns>The value of the well known parameter.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    protected internal virtual T? GetKnownParameter<T>(KnownParameter<T> knownParameter) =>
        Parameters.GetValueOrDefault(knownParameter);

    /// <summary>
    /// Sets the value of the well known parameter.
    /// </summary>
    /// <param name="knownParameter">The descriptor of the well known parameter.</param>
    /// <param name="parsedValue">The value of the well known parameter.</param>
    /// <typeparam name="T">The data type of the well known parameter.</typeparam>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    protected internal virtual void SetKnownParameter<T>(KnownParameter<T> knownParameter, T? parsedValue)
    {
        if (!IsInitialized) throw NotInitializedException;

        var parameterName = knownParameter.Name;
        if (parsedValue is null)
        {
            Parameters.Remove(parameterName);
            return;
        }

        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = knownParameter.Parser.Create(OpenIdEnvironmentOrNull, descriptor, knownParameter.Parser, parsedValue);
        ParametersOrNull.Set(parameter);
    }
}

/// <summary>
/// Provides a generic implementation of <see cref="OpenIdMessage"/>.
/// </summary>
/// <typeparam name="T">The type of <c>OAuth</c> or <c>OpenID Connect</c> message.</typeparam>
[PublicAPI]
public abstract class OpenIdMessage<T> : OpenIdMessage
    where T : OpenIdMessage<T>, new()
{
    /// <summary>
    /// Create an empty <c>OAuth</c> or <c>OpenID Connect</c> message.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Create(OpenIdEnvironment openIdEnvironment)
    {
        var message = new T();
        message.Initialize(openIdEnvironment);
        return message;
    }

    /// <summary>
    /// Create and loads an <c>OAuth</c> or <c>OpenID Connect</c> message by using a collection of <see cref="IParameter{T}"/> values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <param name="parameters">The collection of <see cref="IParameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="IParameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false)
    {
        var message = new T();
        message.Initialize(openIdEnvironment, parameters, cloneParameters);
        return message;
    }

    /// <summary>
    /// Create and loads an <c>OAuth</c> or <c>OpenID Connect</c> message by parsing <see cref="StringValues"/> key-value pairs into a collection of <see cref="IParameter{T}"/> values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <param name="properties">The collection of <see cref="StringValues"/> key-value pairs to be parsed into <see cref="IParameter{T}"/> values.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    public static T Load(OpenIdEnvironment openIdEnvironment, IEnumerable<KeyValuePair<string, StringValues>> properties) =>
        Load(openIdEnvironment, properties.Select(property => Parameter.Load(openIdEnvironment, property.Key, property.Value)));

    /// <inheritdoc />
    protected OpenIdMessage()
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdMessage(T other)
        : base(other)
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdMessage(OpenIdEnvironment openIdEnvironment)
        : base(openIdEnvironment)
    {
        // nothing
    }

    /// <inheritdoc />
    protected OpenIdMessage(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false)
        : base(openIdEnvironment, parameters, cloneParameters)
    {
        // nothing
    }

    /// <inheritdoc />
    public override string TypeDiscriminator => typeof(T).Name;

    /// <inheritdoc cref="ISupportClone{T}.Clone"/>
    public abstract T Clone();

    /// <inheritdoc />
    public override IOpenIdMessage CloneMessage() => Clone();
}
