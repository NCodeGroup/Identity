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

using System.Collections.Frozen;
using System.Net.Http.Headers;
using NCode.Identity.OpenId.Client.Http;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Sends client-authenticated form posts to provider endpoints.
/// </summary>
internal class ClientRequestSender(
    ProviderHttpClient providerHttpClient,
    IEnumerable<IClientAuthenticator> authenticators
)
{
    private ProviderHttpClient ProviderHttpClient { get; } = providerHttpClient;

    private FrozenDictionary<string, IClientAuthenticator> Authenticators { get; } =
        BuildAuthenticators(authenticators);

    private static FrozenDictionary<string, IClientAuthenticator> BuildAuthenticators(
        IEnumerable<IClientAuthenticator> authenticators
    )
    {
        var dictionary = new Dictionary<string, IClientAuthenticator>(StringComparer.Ordinal);
        foreach (var authenticator in authenticators)
        {
            if (!dictionary.TryAdd(authenticator.MethodName, authenticator))
                throw new InvalidOperationException(
                    $"More than one client authenticator is registered for the method '{authenticator.MethodName}'."
                );
        }

        return dictionary.ToFrozenDictionary(StringComparer.Ordinal);
    }

    public virtual ValueTask<ProviderHttpResponse> PostFormAsync(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata,
        string endpoint,
        List<KeyValuePair<string, string>> parameters,
        CancellationToken cancellationToken
    ) =>
        PostFormAsync(
            registration,
            metadata,
            endpoint,
            parameters,
            includeClientId: false,
            cancellationToken
        );

    /// <summary>
    /// Sends a client-authenticated form post to a provider endpoint.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="metadata">The provider metadata.</param>
    /// <param name="endpoint">The endpoint to post to.</param>
    /// <param name="parameters">The form parameters.</param>
    /// <param name="includeClientId">Whether the form must carry <c>client_id</c>; it is appended after authentication unless the authenticator already added it (RFC 9126 §2.1).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public virtual async ValueTask<ProviderHttpResponse> PostFormAsync(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata,
        string endpoint,
        List<KeyValuePair<string, string>> parameters,
        bool includeClientId,
        CancellationToken cancellationToken
    )
    {
        if (!ProviderUri.TryParseEndpoint(endpoint, out var uri))
            throw new OpenIdProviderException(
                $"The endpoint '{endpoint}' is not an acceptable endpoint (https, or http on loopback)."
            );

        if (!Authenticators.TryGetValue(registration.AuthenticationMethod, out var authenticator))
            throw new InvalidOperationException(
                $"No client authenticator is registered for the method '{registration.AuthenticationMethod}' of client '{registration.ClientId}'."
            );

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(OpenIdProtocol.MediaTypes.Json)
        );

        await authenticator.AuthenticateAsync(
            new ClientAuthenticationContext(registration, metadata, endpoint, request, parameters),
            cancellationToken
        );

        if (includeClientId && !parameters.Exists(static p => p.Key == "client_id"))
            parameters.Add(new KeyValuePair<string, string>("client_id", registration.ClientId));

        request.Content = new FormUrlEncodedContent(parameters);

        return await ProviderHttpClient.SendAsync(request, cancellationToken);
    }
}
