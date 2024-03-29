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

using NIdentity.OpenId.Clients;

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides additional contextual information when handling an authorization request.
/// </summary>
public abstract class AuthorizationRequestContext
{
    /// <summary>
    /// Gets the <see cref="OpenIdContext"/> instance associated with the current request.
    /// </summary>
    public abstract OpenIdContext OpenIdContext { get; }

    /// <summary>
    /// Gets the <see cref="OpenIdClient"/> instance associated with the current request.
    /// </summary>
    public abstract OpenIdClient OpenIdClient { get; }

    /// <summary>
    /// Gets the <see cref="IAuthorizationRequest"/> that contains the <c>OAuth</c> or <c>OpenID Connect</c> authorization parameters.
    /// </summary>
    public abstract IAuthorizationRequest AuthorizationRequest { get; }

    /// <summary>
    /// Gets a boolean indicating whether the current authorization request has been continued from user interaction.
    /// </summary>
    public abstract bool IsContinuation { get; }
}
