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

using System.Text.Json;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.OpenId.Client.Options;

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// Sends requests to an OpenID provider with a timeout and a response size cap.
/// </summary>
internal class ProviderHttpClient(
    IHttpClientFactory httpClientFactory,
    IOptions<OpenIdClientOptions> options
)
{
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;

    private OpenIdClientOptions Options { get; } = options.Value;

    public virtual ValueTask<ProviderHttpResponse> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) => SendAsync(request, OpenIdClientDefaults.HttpClientName, cancellationToken);

    public virtual async ValueTask<ProviderHttpResponse> SendAsync(
        HttpRequestMessage request,
        string httpClientName,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken
        );
        timeoutSource.CancelAfter(Options.RequestTimeout);
        var token = timeoutSource.Token;

        try
        {
            var client = HttpClientFactory.CreateClient(httpClientName);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token
            );

            await response.Content.LoadIntoBufferAsync(Options.MaxResponseSizeBytes, token);
            var bytes = await response.Content.ReadAsByteArrayAsync(token);

            var dpopNonce = response.Headers.TryGetValues(
                OpenIdProtocol.DPoP.NonceHeaderName,
                out var values
            )
                ? values.FirstOrDefault()
                : null;

            var wwwAuthenticate =
                response.Headers.WwwAuthenticate.Count > 0
                    ? response.Headers.WwwAuthenticate.ToString()
                    : null;
            var mediaType = response.Content.Headers.ContentType?.MediaType;

            if (bytes.Length == 0)
                return new ProviderHttpResponse((int)response.StatusCode, null)
                {
                    DPoPNonce = dpopNonce,
                    MediaType = mediaType,
                    WwwAuthenticate = wwwAuthenticate,
                };

            if (
                string.Equals(
                    mediaType,
                    OpenIdProtocol.MediaTypes.Jwt,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return new ProviderHttpResponse((int)response.StatusCode, null)
                {
                    DPoPNonce = dpopNonce,
                    MediaType = mediaType,
                    WwwAuthenticate = wwwAuthenticate,
                    Jwt = SecureEncoding.UTF8.GetString(bytes).Trim(),
                };

            if (
                !string.Equals(
                    mediaType,
                    OpenIdProtocol.MediaTypes.Json,
                    StringComparison.OrdinalIgnoreCase
                )
                && !string.Equals(
                    mediaType,
                    OpenIdProtocol.MediaTypes.JwkSet,
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return new ProviderHttpResponse((int)response.StatusCode, null)
                {
                    DPoPNonce = dpopNonce,
                    MediaType = mediaType,
                    WwwAuthenticate = wwwAuthenticate,
                };

            var jsonElement = JsonElement.Parse(bytes);
            return new ProviderHttpResponse((int)response.StatusCode, jsonElement)
            {
                DPoPNonce = dpopNonce,
                MediaType = mediaType,
                WwwAuthenticate = wwwAuthenticate,
            };
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new OpenIdProviderException(
                $"The request to '{request.RequestUri}' timed out.",
                exception
            );
        }
        catch (HttpRequestException exception)
        {
            throw new OpenIdProviderException(
                $"The OpenID provider at '{request.RequestUri}' could not be reached or returned a response that is too large.",
                exception
            );
        }
        catch (JsonException exception)
        {
            throw new OpenIdProviderException(
                $"The response from '{request.RequestUri}' is not valid JSON.",
                exception
            );
        }
    }
}
