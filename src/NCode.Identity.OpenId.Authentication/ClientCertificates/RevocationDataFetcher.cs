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

using System.Buffers;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Fetches revocation information, a certificate revocation list or an OCSP response, through the guarded named
/// client: the connection is pinned away from private, loopback, and link-local addresses, redirects are not followed,
/// and both <see cref="ClientCertificateRevocationOptions.FetchTimeout"/> and
/// <see cref="ClientCertificateRevocationOptions.MaxResponseSizeBytes"/> bound the request. A fetch that yields
/// nothing usable returns <see langword="null"/> after it logs why.
/// </summary>
// See ADR-0252.
internal sealed class RevocationDataFetcher(
    IHttpClientFactory httpClientFactory,
    IOutboundHttpContentReader outboundHttpContentReader,
    IOptions<ClientCertificateRevocationOptions> options,
    ILogger<RevocationDataFetcher> logger
)
{
    internal const string HttpClientName = "NCode.Identity.OpenId.ClientCertificateRevocation";

    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IOutboundHttpContentReader OutboundHttpContentReader { get; } =
        outboundHttpContentReader;
    private ClientCertificateRevocationOptions Options { get; } = options.Value;
    private ILogger<RevocationDataFetcher> Logger { get; } = logger;

    /// <summary>
    /// Fetches the body of <paramref name="requestUri"/> with a <c>GET</c> request.
    /// </summary>
    /// <param name="requestUri">The absolute URI to request.</param>
    /// <param name="location">The location that the log entries name.</param>
    /// <param name="subject">The subject of the certificate that the log entries name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The response body, or <see langword="null"/> when nothing usable arrived.</returns>
    public ValueTask<byte[]?> GetAsync(
        string requestUri,
        string location,
        string subject,
        CancellationToken cancellationToken
    ) => SendAsync(HttpMethod.Get, requestUri, location, subject, null, cancellationToken);

    /// <summary>
    /// Fetches the response of a <c>POST</c> request to <paramref name="location"/> that sends
    /// <paramref name="content"/>.
    /// </summary>
    /// <param name="location">The absolute URI to request, which the log entries also name.</param>
    /// <param name="subject">The subject of the certificate that the log entries name.</param>
    /// <param name="content">The body to send.</param>
    /// <param name="contentType">The media type of <paramref name="content"/>.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The response body, or <see langword="null"/> when nothing usable arrived.</returns>
    public ValueTask<byte[]?> PostAsync(
        string location,
        string subject,
        byte[] content,
        string contentType,
        CancellationToken cancellationToken
    ) =>
        SendAsync(
            HttpMethod.Post,
            location,
            location,
            subject,
            new ByteArrayContent(content)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(contentType) },
            },
            cancellationToken
        );

    private async ValueTask<byte[]?> SendAsync(
        HttpMethod method,
        string requestUri,
        string location,
        string subject,
        HttpContent? content,
        CancellationToken cancellationToken
    )
    {
        if (!AbsoluteUri.TryParse(requestUri, out var parsedUri))
        {
            content?.Dispose();
            Logger.RevocationInformationUnavailable(
                location,
                subject,
                "The location is not an absolute URI."
            );
            return null;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(Options.FetchTimeout);

        try
        {
            using var httpClient = HttpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(method, parsedUri) { Content = content };

            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token
            );

            if (!response.IsSuccessStatusCode)
            {
                Logger.RevocationInformationUnavailable(
                    location,
                    subject,
                    $"The response could not be fetched (HTTP {(int)response.StatusCode})."
                );
                return null;
            }

            using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);
            var readResult = await OutboundHttpContentReader.ReadCappedAsync(
                response,
                buffer,
                Options.MaxResponseSizeBytes,
                timeoutCts.Token
            );
            if (readResult != OutboundHttpReadResult.Completed)
            {
                Logger.RevocationInformationUnavailable(
                    location,
                    subject,
                    "The response exceeds the maximum allowed size."
                );
                return null;
            }

            return buffer.AsReadOnlySequence.ToArray();
        }
        catch (HttpRequestException exception)
        {
            Logger.RevocationInformationUnavailable(
                location,
                subject,
                $"The response could not be fetched: {exception.Message}"
            );
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.RevocationInformationUnavailable(location, subject, "The fetch timed out.");
            return null;
        }
    }
}
