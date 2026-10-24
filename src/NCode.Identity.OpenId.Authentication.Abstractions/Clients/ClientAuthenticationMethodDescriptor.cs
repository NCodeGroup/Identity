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

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Describes a client-authentication method: the name a client registers in its <c>token_endpoint_auth_method</c> and
/// the traits of the method that other components need without constructing its handler. A package contributes a
/// descriptor through <c>ICollectionDataSource&lt;ClientAuthenticationMethodDescriptor&gt;</c> beside each
/// <see cref="IClientAuthenticationHandler"/> it registers.
/// </summary>
// See ADR-0207.
[PublicAPI]
public sealed class ClientAuthenticationMethodDescriptor
{
    /// <summary>
    /// Gets the name of the method, which is the <c>token_endpoint_auth_method</c> value a client registers and the
    /// <see cref="IClientAuthenticationHandler.AuthenticationMethod"/> of the handler that authenticates it.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets a value indicating whether the method verifies a shared secret, so that a dynamically registered client of
    /// the method is issued a <c>client_secret</c>.
    /// </summary>
    public bool RequiresClientSecret { get; init; }
}
