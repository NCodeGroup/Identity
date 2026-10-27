#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.OpenId.Client;

/// <summary>
/// Authenticates the client to the provider's token and revocation endpoints with one method.
/// </summary>
/// <remarks>
/// Authenticators form an open strategy collection selected by <see cref="MethodName"/>: another package adds a method
/// by registering an implementation. The client does not check the method against
/// <c>token_endpoint_auth_methods_supported</c>, because the provider enforces the registered method.
/// </remarks>
[PublicAPI]
public interface IClientAuthenticator
{
    /// <summary>
    /// Gets the name of the authentication method, for example <see cref="ClientAuthenticationMethods.ClientSecretBasic"/>.
    /// </summary>
    string MethodName { get; }

    /// <summary>
    /// Adds the client's credentials to the request headers or form parameters.
    /// </summary>
    /// <param name="context">The request to authenticate.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
    ValueTask AuthenticateAsync(
        ClientAuthenticationContext context,
        CancellationToken cancellationToken
    );
}
