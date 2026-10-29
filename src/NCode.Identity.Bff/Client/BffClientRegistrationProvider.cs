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

using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Client;

/// <summary>
/// Provides the <see cref="ClientRegistration"/> that describes the BFF as a client of the OpenID Provider.
/// </summary>
internal class BffClientRegistrationProvider(IOptions<BffOptions> options)
{
    private readonly Lazy<ClientRegistration> _registration = new(() => Create(options.Value));

    /// <summary>
    /// Gets the client registration, built once from the options.
    /// </summary>
    public ClientRegistration Registration => _registration.Value;

    private static ClientRegistration Create(BffOptions bffOptions) =>
        new(bffOptions.Authority, bffOptions.ClientId, bffOptions.ClientAuthentication.Method)
        {
            KeyRing = bffOptions.ClientAuthentication.KeyRing,
            DPoPKeyRing = bffOptions.DPoP.KeyRing,
            DPoPBoundAccessTokens = bffOptions.DPoP.Mode == BffDPoPModes.Required,
            BackchannelLogoutSessionRequired = bffOptions.BackchannelLogoutSessionRequired,
        };
}
