// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Polly.Timeout;

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// Bounds every request to an OpenID Provider with the resilience pipeline that
/// <see cref="ProviderResiliencePipelines"/> selects for it, and turns the <see cref="TimeoutRejectedException"/> that
/// a resilience timeout throws into the <see cref="TaskCanceledException"/> with a <see cref="TimeoutException"/> inner
/// exception that <see cref="HttpClient.Timeout"/> produces, so a caller that catches
/// <see cref="OperationCanceledException"/> does not let a timeout escape.
/// </summary>
/// <param name="pipelines">The pipelines that bound the requests.</param>
// See ADR-1336.
internal sealed class ProviderResilienceHandler(ProviderResiliencePipelines pipelines)
    : DelegatingHandler
{
    private ProviderResiliencePipelines Pipelines { get; } = pipelines;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var pipeline = Pipelines.Select(request);

        try
        {
            return await pipeline.ExecuteAsync(
                static (state, token) => state.Handler.SendAttemptAsync(state.Request, token),
                (Handler: this, Request: request),
                cancellationToken
            );
        }
        catch (TimeoutRejectedException exception) when (!cancellationToken.IsCancellationRequested)
        {
            var timeoutException = new TimeoutException(exception.Message, exception);
            throw new TaskCanceledException(
                "The request to the OpenID provider timed out.",
                timeoutException
            );
        }
    }

    private async ValueTask<HttpResponseMessage> SendAttemptAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) => await base.SendAsync(request, cancellationToken);
}
