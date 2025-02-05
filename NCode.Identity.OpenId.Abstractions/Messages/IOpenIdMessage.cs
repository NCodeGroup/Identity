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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Represents the base interface for all <c>OAuth</c> or <c>OpenID Connect</c> messages
/// that use the standard implementation.
/// </summary>
[PublicAPI]
public interface IOpenIdMessage : IBaseOpenIdMessage
{
    /// <summary>
    /// Gets the <see cref="string"/> value that is used to discriminate this object's <see cref="Type"/>
    /// when serializing and deserializing as JSON.
    /// </summary>
    string TypeDiscriminator { get; }

    /// <summary>
    /// Gets or sets the <see cref="SerializationFormat"/> for the current instance.
    /// </summary>
    SerializationFormat SerializationFormat { get; set; }

    /// <summary>
    /// Gets the collection of strong-typed <c>OAuth</c> or <c>OpenID Connect</c> parameters.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is not initialized.</exception>
    IReadOnlyDictionary<string, IParameter> Parameters { get; }

    /// <summary>
    /// Initializes the current instance with an <see cref="OpenIdEnvironment"/>.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is already initialized.</exception>
    void Initialize(OpenIdEnvironment openIdEnvironment);

    /// <summary>
    /// Initializes the current instance with an <see cref="OpenIdEnvironment"/> and collection of <see cref="IParameter"/> values.
    /// </summary>
    /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance.</param>
    /// <param name="parameters">The collection of <see cref="IParameter"/> values.</param>
    /// <param name="cloneParameters"><c>true</c> if the <see cref="IParameter"/> instances should be deep-cloned; otherwise,
    /// <c>false</c>. The default value is <c>false</c>.</param>
    /// <exception cref="InvalidOperationException">Thrown when the current instance is already initialized.</exception>
    void Initialize(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false);
}
