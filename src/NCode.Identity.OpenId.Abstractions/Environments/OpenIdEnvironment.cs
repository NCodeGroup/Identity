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

using System.Security.Claims;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.DataProtection;
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
    /// Gets the <see cref="IDataProtector"/> that can be used to protect and unprotect data.
    /// </summary>
    public abstract IDataProtector DataProtector { get; }

    /// <summary>
    /// Gets an ephemeral <see cref="IDataProtector"/> whose protected data is only valid for the lifetime of the current process.
    /// </summary>
    public abstract IDataProtector EphemeralDataProtector { get; }

    /// <summary>
    /// Gets the <see cref="IKnownParameterCollection"/> which contains all known parameters.
    /// </summary>
    public abstract IKnownParameterCollection KnownParameters { get; }

    /// <summary>
    /// Gets the <see cref="IOpenIdErrorFactory"/> instance that can be used to create error responses
    /// </summary>
    public abstract IOpenIdErrorFactory ErrorFactory { get; }

    /// <summary>
    /// Gets the <see cref="IPropertyBag"/> that can provide additional user-defined information about the current instance or operation.
    /// </summary>
    public abstract IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Extracts the subject id from a <see cref="ClaimsPrincipal"/> using the host-configured extraction logic. This is
    /// the single, shared accessor every HTTP surface uses to derive a caller's subject id so the behavior stays
    /// consistent across the protocol and management APIs.
    /// </summary>
    /// <param name="subject">The <see cref="ClaimsPrincipal"/> to search for the subject id.</param>
    /// <returns>The subject id if found; otherwise <c>null</c>.</returns>
    public abstract string? GetSubjectId(ClaimsPrincipal subject);

    /// <summary>
    /// Extracts the primary <see cref="ClaimsIdentity"/> from a <see cref="ClaimsPrincipal"/> using the host-configured
    /// extraction logic.
    /// </summary>
    /// <param name="subject">The <see cref="ClaimsPrincipal"/> to extract the <see cref="ClaimsIdentity"/> from.</param>
    /// <returns>The primary <see cref="ClaimsIdentity"/> from the <see cref="ClaimsPrincipal"/>.</returns>
    public abstract ClaimsIdentity GetSubjectIdentity(ClaimsPrincipal subject);

    /// <summary>
    /// Gets the <see cref="ParameterDescriptor"/> for the specified parameter name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>The <see cref="ParameterDescriptor"/> for the specified parameter name.</returns>
    public abstract ParameterDescriptor GetParameterDescriptor(string parameterName);

    /// <summary>
    /// Creates an <see cref="IOpenIdError"/> instance for an <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// </summary>
    /// <param name="errorCode">The <c>error</c> parameter for the <c>OAuth</c> or <c>OpenID Connect</c> error.</param>
    /// <returns>The newly created <see cref="IOpenIdError"/> instance.</returns>
    public abstract IOpenIdError CreateError(string errorCode);

    /// <summary>
    /// Creates a new <see cref="IOpenIdMessage"/> instance using the specified <paramref name="typeDiscriminator"/>
    /// and <paramref name="parameters"/>.
    /// </summary>
    /// <param name="typeDiscriminator">A <see cref="string"/> value that is used to discriminate the message type.</param>
    /// <param name="parameters">The collection of parameters to be used to initialize the new message.</param>
    /// <returns>The newly created <see cref="IOpenIdMessage"/> instance.</returns>
    public abstract IOpenIdMessage CreateMessage(
        string typeDiscriminator,
        IEnumerable<IParameter> parameters
    );
}
