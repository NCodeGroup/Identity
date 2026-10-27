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
/// Describes how this application is registered as a client of an OpenID provider.
/// </summary>
/// <param name="authority">The issuer URL of the OpenID provider.</param>
/// <param name="clientId">The client identifier issued by the OpenID provider.</param>
/// <param name="authenticationMethod">The client authentication method used at the token endpoint.
/// See <see cref="ClientAuthenticationMethods"/> for the known values.</param>
[PublicAPI]
public sealed class ClientRegistration(
    string authority,
    string clientId,
    string authenticationMethod
)
{
    /// <summary>
    /// Gets the issuer URL of the OpenID provider.
    /// </summary>
    public string Authority { get; } = ValidateNotEmpty(authority);

    /// <summary>
    /// Gets the client identifier issued by the OpenID provider.
    /// </summary>
    public string ClientId { get; } = ValidateNotEmpty(clientId);

    /// <summary>
    /// Gets the client authentication method used at the token endpoint.
    /// </summary>
    public string AuthenticationMethod { get; } = ValidateNotEmpty(authenticationMethod);

    /// <summary>
    /// Gets the name of the keyed <see cref="NCode.Identity.Secrets.Collections.ISecretKeyCollectionProvider"/> holding the client's credentials.
    /// </summary>
    public string? KeyRing { get; init; }

    private static string ValidateNotEmpty(
        string value,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(value))]
            string? paramName = null
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(value, paramName);
        return value;
    }
}
