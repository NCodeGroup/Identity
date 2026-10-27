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

    public virtual async ValueTask<ProviderHttpResponse> SendAsync(
        HttpRequestMessage request,
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
            var client = HttpClientFactory.CreateClient(OpenIdClientDefaults.HttpClientName);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                token
            );

            await response.Content.LoadIntoBufferAsync(Options.MaxResponseSizeBytes, token);
            var bytes = await response.Content.ReadAsByteArrayAsync(token);

            if (bytes.Length == 0)
                return new ProviderHttpResponse(response.StatusCode, null);

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (
                !string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(
                    mediaType,
                    "application/jwk-set+json",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                return new ProviderHttpResponse(response.StatusCode, null);

            using var document = JsonDocument.Parse(bytes);
            return new ProviderHttpResponse(response.StatusCode, document.RootElement.Clone());
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
