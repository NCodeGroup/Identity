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
using NCode.Identity.OpenId;

namespace NCode.Identity.Bff.Options;

/// <summary>
/// Configures how the BFF authenticates to the token endpoint of the OpenID Provider.
/// </summary>
[PublicAPI]
public sealed class BffClientAuthenticationOptions
{
    /// <summary>
    /// Gets or sets the client authentication method, as registered by an <c>IClientAuthenticator</c>. The default is
    /// <c>private_key_jwt</c>. The configuration key is <c>Bff:ClientAuthentication:Method</c>.
    /// </summary>
    public string Method { get; set; } = OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt;

    /// <summary>
    /// Gets or sets the name of the key ring that holds the client's authentication keys. The configuration key is
    /// <c>Bff:ClientAuthentication:KeyRing</c>.
    /// </summary>
    public string? KeyRing { get; set; }
}
