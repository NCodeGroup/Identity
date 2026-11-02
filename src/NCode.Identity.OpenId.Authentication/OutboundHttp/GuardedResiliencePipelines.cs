// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Holds the resilience pipelines that bound a call of the guarded outbound client and selects the one that fits a
/// request. A GET or HEAD request without a body, while retries are turned on, runs through the total timeout, a
/// bounded and jittered retry, and a per-attempt timeout; any other request runs through the total timeout and the
/// per-attempt timeout only, so it is never sent twice.
/// </summary>
/// <param name="options">The <see cref="OutboundHttpOptions"/> that hold the limits.</param>
// See ADR-1385.
internal sealed class GuardedResiliencePipelines(IOptions<OutboundHttpOptions> options)
{
    private ResiliencePipeline<HttpResponseMessage> Retrying { get; } =
        Build(options.Value, options.Value.MaxRetryAttempts);

    private ResiliencePipeline<HttpResponseMessage> Bounded { get; } =
        Build(options.Value, maxRetryAttempts: 0);

    /// <summary>
    /// Selects the pipeline that bounds <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request that is about to be sent.</param>
    /// <returns>The pipeline that runs the request.</returns>
    public ResiliencePipeline<HttpResponseMessage> Select(HttpRequestMessage request) =>
        IsRetryable(request) ? Retrying : Bounded;

    /// <summary>
    /// Determines whether <paramref name="request"/> may be sent again after a failed attempt. A request with a body is
    /// never resent, because a body's content may be a stream that one attempt consumes.
    /// </summary>
    /// <param name="request">The request that is about to be sent.</param>
    /// <returns><c>true</c> when the request is a GET or HEAD without a body; otherwise, <c>false</c>.</returns>
    internal static bool IsRetryable(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isSafeMethod = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
        return isSafeMethod && request.Content is null;
    }

    /// <summary>
    /// Determines whether the outcome of an attempt is transient and may be retried: a failed connection, a request
    /// that the per-attempt timeout cancelled, or a 408, 429 or 5xx response. A connection or name-resolution error is
    /// never retried, because the guard's refusal is deterministic.
    /// </summary>
    /// <param name="outcome">The outcome of the attempt.</param>
    /// <returns><c>true</c> when the outcome is transient; otherwise, <c>false</c>.</returns>
    internal static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (
            outcome.Exception is HttpRequestException
            {
                HttpRequestError: HttpRequestError.ConnectionError
                    or HttpRequestError.NameResolutionError,
            }
        )
        {
            return false;
        }

        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
            return true;

        if (outcome.Result is not { } response)
            return false;

        var statusCode = (int)response.StatusCode;
        return statusCode is 408 or 429 or >= 500;
    }

    private static ResiliencePipeline<HttpResponseMessage> Build(
        OutboundHttpOptions options,
        int maxRetryAttempts
    )
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        builder.AddTimeout(options.TotalTimeout);

        if (maxRetryAttempts > 0)
        {
            builder.AddRetry(
                new RetryStrategyOptions<HttpResponseMessage>
                {
                    MaxRetryAttempts = maxRetryAttempts,
                    Delay = options.RetryDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = static args =>
                    {
                        var isTransient = IsTransient(args.Outcome);
                        return ValueTask.FromResult(isTransient);
                    },
                    OnRetry = static args =>
                    {
                        args.Outcome.Result?.Dispose();
                        return ValueTask.CompletedTask;
                    },
                }
            );
        }

        builder.AddTimeout(options.AttemptTimeout);

        return builder.Build();
    }
}
