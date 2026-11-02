// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Polly.Timeout;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Bounds every call of the guarded outbound client with the resilience pipeline that
/// <see cref="GuardedResiliencePipelines"/> selects for it, and turns the <see cref="TimeoutRejectedException"/> that
/// a resilience timeout throws into the <see cref="TaskCanceledException"/> with a <see cref="TimeoutException"/> inner
/// exception that <see cref="HttpClient.Timeout"/> produces, so a caller that catches
/// <see cref="OperationCanceledException"/> does not let a timeout escape.
/// </summary>
/// <param name="pipelines">The pipelines that bound the calls.</param>
// See ADR-1385.
internal sealed class GuardedResilienceHandler(GuardedResiliencePipelines pipelines)
    : DelegatingHandler
{
    private GuardedResiliencePipelines Pipelines { get; } = pipelines;

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
            throw new TaskCanceledException("The outbound HTTP call timed out.", timeoutException);
        }
    }

    private async ValueTask<HttpResponseMessage> SendAttemptAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    ) => await base.SendAsync(request, cancellationToken);
}
