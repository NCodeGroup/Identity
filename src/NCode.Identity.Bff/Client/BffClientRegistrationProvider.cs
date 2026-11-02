// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
            IdTokenSigningAlgorithms = bffOptions.IdToken.SigningAlgorithms,
            IdTokenKeyManagementAlgorithms = bffOptions.IdToken.KeyManagementAlgorithms,
            IdTokenContentEncryptionAlgorithms = bffOptions.IdToken.ContentEncryptionAlgorithms,
            UserInfoSigningAlgorithms = bffOptions.UserInfo.SigningAlgorithms,
            UserInfoKeyManagementAlgorithms = bffOptions.UserInfo.KeyManagementAlgorithms,
            UserInfoContentEncryptionAlgorithms = bffOptions.UserInfo.ContentEncryptionAlgorithms,
        };
}
