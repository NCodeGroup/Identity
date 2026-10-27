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

using System.Net;
using System.Text.Json;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides a default implementation of <see cref="ITokenClient"/>.
/// </summary>
internal class DefaultTokenClient(
    IProviderMetadataResolver metadataResolver,
    ClientRequestSender sender
) : ITokenClient
{
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;

    private ClientRequestSender Sender { get; } = sender;

    /// <inheritdoc />
    public async ValueTask<TokenResult> RequestTokenAsync(
        ClientRegistration registration,
        TokenRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var tokenEndpoint =
            metadata.TokenEndpoint
            ?? throw new OpenIdProviderException(
                $"The provider '{registration.Authority}' has no token endpoint."
            );

        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", request.GrantType),
        };

        request.AddGrantParameters(parameters);

        if (!string.IsNullOrEmpty(request.Scope))
            parameters.Add(new KeyValuePair<string, string>("scope", request.Scope));

        foreach (var resource in request.Resources)
            parameters.Add(new KeyValuePair<string, string>("resource", resource));

        var response = await Sender.PostFormAsync(
            registration,
            metadata,
            tokenEndpoint,
            parameters,
            cancellationToken
        );

        if (
            response.StatusCode == HttpStatusCode.OK
            && response.Json is { ValueKind: JsonValueKind.Object } json
        )
        {
            TokenResponse? tokenResponse;
            try
            {
                tokenResponse = json.Deserialize<TokenResponse>();
            }
            catch (JsonException exception)
            {
                throw new OpenIdProviderException(
                    "The token endpoint answered with an invalid token response.",
                    exception
                );
            }

            if (
                tokenResponse is null
                || string.IsNullOrEmpty(tokenResponse.AccessToken)
                || string.IsNullOrEmpty(tokenResponse.TokenType)
            )
            {
                throw new OpenIdProviderException(
                    "The token endpoint answered without an access_token or token_type."
                );
            }

            return new TokenResult(tokenResponse);
        }

        if (ProtocolErrorReader.TryRead(response, out var error))
            return new TokenResult(error);

        throw new OpenIdProviderException(
            $"The token endpoint answered HTTP {(int)response.StatusCode} without an OAuth error."
        );
    }
}
