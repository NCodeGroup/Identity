// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Blazor.Client.Options;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Sends the BFF's anti-forgery header on every request to the application's own origin and re-probes the session
/// when such a request is answered with <c>401</c>. Requests to any other origin pass untouched.
/// </summary>
internal sealed class BffAntiforgeryHeaderHandler(
    NavigationManager navigation,
    BffClientSession session,
    IOptions<BffBlazorClientOptions> options
) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        var sameOrigin = IsSameOrigin(request.RequestUri);
        if (sameOrigin)
        {
            var current = options.Value;
            request.Headers.Remove(current.AntiforgeryHeaderName);
            request.Headers.TryAddWithoutValidation(
                current.AntiforgeryHeaderName,
                current.AntiforgeryHeaderValue
            );
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (sameOrigin && response.StatusCode == HttpStatusCode.Unauthorized)
            session.Invalidate();

        return response;
    }

    private bool IsSameOrigin(Uri? requestUri) =>
        requestUri is { IsAbsoluteUri: true }
        && Uri.Compare(
            requestUri,
            new Uri(navigation.BaseUri, UriKind.Absolute),
            UriComponents.SchemeAndServer,
            UriFormat.UriEscaped,
            StringComparison.OrdinalIgnoreCase
        ) == 0;
}
