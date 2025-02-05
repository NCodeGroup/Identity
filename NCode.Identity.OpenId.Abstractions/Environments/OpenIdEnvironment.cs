#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.DataProtection;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides contextual information about the OpenID environment.
/// </summary>
[PublicAPI]
public abstract class OpenIdEnvironment
{
    /// <summary>
    /// Gets the <see cref="JsonSerializerOptions"/> to be used for any JSON serialization.
    /// </summary>
    public abstract JsonSerializerOptions JsonSerializerOptions { get; }

    /// <summary>
    /// Gets the <see cref="ISecureDataProtector"/> that can be used to protect and unprotect data.
    /// </summary>
    public abstract ISecureDataProtector SecureDataProtector { get; }

    /// <summary>
    /// Gets the <see cref="IKnownParameterCollection"/> which contains all known parameters.
    /// </summary>
    public abstract IKnownParameterCollection KnownParameters { get; }

    /// <summary>
    /// Gets the <see cref="IOpenIdErrorFactory"/> that can be used to create error responses
    /// </summary>
    public abstract IOpenIdErrorFactory ErrorFactory { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Gets the <see cref="ParameterDescriptor"/> for the specified parameter name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>The <see cref="ParameterDescriptor"/> for the specified parameter name.</returns>
    public abstract ParameterDescriptor GetParameterDescriptor(string parameterName);

    /// <summary>
    /// Creates a new <see cref="IOpenIdMessage"/> instance using the specified <paramref name="typeDiscriminator"/>
    /// and <paramref name="parameters"/>.
    /// </summary>
    /// <param name="typeDiscriminator">A <see cref="string"/> value that is used to discriminate the message type.</param>
    /// <param name="parameters">The collection of parameters to be used to initialize the new message.</param>
    /// <returns>The newly created <see cref="IOpenIdMessage"/> instance.</returns>
    public abstract IOpenIdMessage CreateMessage(string typeDiscriminator, IEnumerable<IParameter> parameters);
}
