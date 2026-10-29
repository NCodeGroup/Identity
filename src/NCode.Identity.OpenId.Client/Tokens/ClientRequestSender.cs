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
using NCode.Identity.OpenId.Client.ProofOfPossession;

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
        ProviderEndpoint endpoint,
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
    public virtual ValueTask<ProviderHttpResponse> PostFormAsync(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata,
        ProviderEndpoint endpoint,
        List<KeyValuePair<string, string>> parameters,
        bool includeClientId,
        CancellationToken cancellationToken
    ) =>
        PostFormAsync(
            registration,
            metadata,
            endpoint,
            parameters,
            includeClientId,
            dpopKey: null,
            cancellationToken
        );

    /// <summary>
    /// Sends a client-authenticated form post to a provider endpoint, optionally proving possession of a DPoP key.
    /// </summary>
    /// <param name="registration">The client registration.</param>
    /// <param name="metadata">The provider metadata.</param>
    /// <param name="endpoint">The endpoint to post to.</param>
    /// <param name="parameters">The form parameters; they are copied, never mutated.</param>
    /// <param name="includeClientId">Whether the form must carry <c>client_id</c>; it is appended after authentication unless the authenticator already added it (RFC 9126 §2.1).</param>
    /// <param name="dpopKey">The DPoP proof key, or <see langword="null"/> to send no DPoP proof; when the provider answers <c>use_dpop_nonce</c> the request is retried exactly once with the supplied nonce (RFC 9449 §8).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public virtual async ValueTask<ProviderHttpResponse> PostFormAsync(
        ClientRegistration registration,
        OpenIdProviderMetadata metadata,
        ProviderEndpoint endpoint,
        List<KeyValuePair<string, string>> parameters,
        bool includeClientId,
        DPoPProofKey? dpopKey,
        CancellationToken cancellationToken
    )
    {
        if (!ProviderUri.TryParseEndpoint(endpoint.Url, out var uri))
            throw new OpenIdProviderException(
                $"The endpoint '{endpoint.Url}' is not an acceptable endpoint (https, or http on loopback)."
            );

        if (!Authenticators.TryGetValue(registration.AuthenticationMethod, out var authenticator))
            throw new InvalidOperationException(
                $"No client authenticator is registered for the method '{registration.AuthenticationMethod}' of client '{registration.ClientId}'."
            );

        var response = await SendOnceAsync(
            authenticator,
            registration,
            metadata,
            endpoint.Url,
            uri,
            parameters,
            includeClientId,
            dpopKey,
            cancellationToken
        );

        if (dpopKey is null || string.IsNullOrEmpty(response.DPoPNonce))
            return response;

        dpopKey.RecordNonce(uri, response.DPoPNonce);

        if (
            !ProtocolErrorReader.TryRead(response, out var error)
            || !string.Equals(
                error.Error,
                OpenIdProtocol.ErrorCodes.UseDPoPNonce,
                StringComparison.Ordinal
            )
        )
            return response;

        // RFC 9449 §8: the request is retried exactly once with the nonce the provider supplied.
        var retry = await SendOnceAsync(
            authenticator,
            registration,
            metadata,
            endpoint.Url,
            uri,
            parameters,
            includeClientId,
            dpopKey,
            cancellationToken
        );

        if (!string.IsNullOrEmpty(retry.DPoPNonce))
            dpopKey.RecordNonce(uri, retry.DPoPNonce);

        return retry;
    }

    private async ValueTask<ProviderHttpResponse> SendOnceAsync(
        IClientAuthenticator authenticator,
        ClientRegistration registration,
        OpenIdProviderMetadata metadata,
        string target,
        Uri uri,
        List<KeyValuePair<string, string>> parameters,
        bool includeClientId,
        DPoPProofKey? dpopKey,
        CancellationToken cancellationToken
    )
    {
        var formParameters = new List<KeyValuePair<string, string>>(parameters);

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(OpenIdProtocol.MediaTypes.Json)
        );

        await authenticator.AuthenticateAsync(
            new ClientAuthenticationContext(
                registration,
                metadata,
                target,
                request,
                formParameters
            ),
            cancellationToken
        );

        if (
            includeClientId
            && !formParameters.Exists(static p => p.Key == OpenIdProtocol.Parameters.ClientId)
        )
            formParameters.Add(
                new KeyValuePair<string, string>(
                    OpenIdProtocol.Parameters.ClientId,
                    registration.ClientId
                )
            );

        if (dpopKey is not null)
            request.Headers.TryAddWithoutValidation(
                OpenIdProtocol.DPoP.HeaderName,
                dpopKey.CreateProof(HttpMethod.Post, uri, accessToken: null)
            );

        request.Content = new FormUrlEncodedContent(formParameters);

        return await ProviderHttpClient.SendAsync(request, cancellationToken);
    }
}
