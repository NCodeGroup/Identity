// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Defines the one named HTTP client that serves every fetch of, and delivery to, a caller-supplied URL, and the
/// resilience pipeline that bounds it: a total timeout, then a bounded and jittered retry, then a per-attempt timeout.
/// </summary>
// See ADR-1315.
internal static class GuardedHttpClient
{
    /// <summary>
    /// The name of the guarded HTTP client.
    /// </summary>
    internal const string Name = "NCode.Identity.OpenId.GuardedHttp";

    /// <summary>
    /// Determines whether the outcome of an attempt is retried. A connection or name-resolution error is never retried,
    /// because the guard's refusal is deterministic; any other transient failure is.
    /// </summary>
    /// <param name="outcome">The outcome of the attempt.</param>
    /// <returns><c>true</c> when the attempt is retried; otherwise, <c>false</c>.</returns>
    internal static bool IsRetryable(Outcome<HttpResponseMessage> outcome)
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

        return HttpClientResiliencePredicates.IsTransient(outcome);
    }

    /// <summary>
    /// Adds the strategies that bound a call: the total timeout, the retry when it is turned on, and the per-attempt
    /// timeout.
    /// </summary>
    /// <param name="builder">The <see cref="ResiliencePipelineBuilder{TResult}"/> to add the strategies to.</param>
    /// <param name="options">The <see cref="OutboundHttpOptions"/> that hold the limits.</param>
    internal static void ConfigurePipeline(
        ResiliencePipelineBuilder<HttpResponseMessage> builder,
        OutboundHttpOptions options
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);

        builder.AddTimeout(options.TotalTimeout);

        if (options.MaxRetryAttempts > 0)
        {
            var retry = new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                Delay = options.RetryDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldRetryAfterHeader = false,
                ShouldHandle = args => ValueTask.FromResult(IsRetryable(args.Outcome)),
            };

            // set after ShouldHandle so the unsafe-method guard wraps the predicate
            retry.DisableForUnsafeHttpMethods();

            builder.AddRetry(retry);
        }

        builder.AddTimeout(options.AttemptTimeout);
    }
}
