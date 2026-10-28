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

using NCode.Identity.OpenId.Client.Http;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides a default implementation of <see cref="ITokenRevocationClient"/>.
/// </summary>
internal class DefaultTokenRevocationClient(
    IProviderMetadataResolver metadataResolver,
    ClientRequestSender sender
) : ITokenRevocationClient
{
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;

    private ClientRequestSender Sender { get; } = sender;

    /// <inheritdoc />
    public async ValueTask<ProtocolError?> RevokeTokenAsync(
        ClientRegistration registration,
        TokenRevocationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var revocationEndpoint = metadata.RevocationEndpoint;
        if (string.IsNullOrEmpty(revocationEndpoint))
        {
            throw new InvalidOperationException(
                $"The OpenID Provider '{registration.Authority}' does not advertise a revocation endpoint."
            );
        }

        var parameters = new List<KeyValuePair<string, string>>
        {
            new(OpenIdProtocol.Parameters.Token, request.Token),
        };

        if (!string.IsNullOrEmpty(request.TokenTypeHint))
            parameters.Add(
                new KeyValuePair<string, string>(
                    OpenIdProtocol.Parameters.TokenTypeHint,
                    request.TokenTypeHint
                )
            );

        var response = await Sender.PostFormAsync(
            registration,
            metadata,
            revocationEndpoint,
            parameters,
            cancellationToken
        );

        if (response.StatusCode == ProviderStatusCodes.Status200OK)
            return null;

        if (ProtocolErrorReader.TryRead(response, out var error))
            return error;

        throw new OpenIdProviderException(
            $"The revocation endpoint answered HTTP {response.StatusCode} without an OAuth error."
        );
    }
}
