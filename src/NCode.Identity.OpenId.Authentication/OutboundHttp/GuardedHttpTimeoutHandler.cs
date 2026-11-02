// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Polly.Timeout;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Turns the <see cref="TimeoutRejectedException"/> that a resilience timeout throws into the
/// <see cref="TaskCanceledException"/> with a <see cref="TimeoutException"/> inner exception that
/// <see cref="HttpClient.Timeout"/> produces, so a caller that catches <see cref="HttpRequestException"/> or
/// <see cref="OperationCanceledException"/> does not let a timeout escape.
/// </summary>
// See ADR-1315.
internal sealed class GuardedHttpTimeoutHandler : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await base.SendAsync(request, cancellationToken);
        }
        catch (TimeoutRejectedException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TaskCanceledException(
                "The outbound HTTP call timed out.",
                new TimeoutException(exception.Message, exception)
            );
        }
    }
}
