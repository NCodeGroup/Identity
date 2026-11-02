// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Metrics;
using NCode.Identity.OpenId.Metrics;
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
/// <param name="timeProvider">The <see cref="TimeProvider"/> that runs the timeouts and the retry delays.</param>
/// <param name="metrics">The <see cref="AuthenticationMetrics"/> that count the retries and the timeouts.</param>
// See ADR-1385.
internal sealed class GuardedResiliencePipelines(
    IOptions<OutboundHttpOptions> options,
    TimeProvider timeProvider,
    AuthenticationMetrics metrics
)
{
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

        return response.StatusCode
            is HttpStatusCode.RequestTimeout
                or HttpStatusCode.TooManyRequests
                or >= HttpStatusCode.InternalServerError;
    }

    /// <summary>
    /// Gets the bounded <c>error.type</c> of a failed attempt: the response's status code, <c>timeout</c> for an attempt
    /// that the per-attempt timeout cancelled, the <see cref="HttpRequestError"/> in snake case for a failed request,
    /// or <c>_OTHER</c>.
    /// </summary>
    /// <param name="outcome">The outcome of the attempt.</param>
    /// <returns>The error type.</returns>
    // The protocol client's ProviderResiliencePipelines.GetErrorType maps outcomes the same way; the client package
    // cannot reference this one (ADR-1285), so the two copies are kept in step, and both test suites pin the values.
    internal static string GetErrorType(Outcome<HttpResponseMessage> outcome) =>
        outcome switch
        {
            { Result: { } response } => ((int)response.StatusCode).ToString(
                CultureInfo.InvariantCulture
            ),
            { Exception: TimeoutRejectedException } => AuthenticationMetrics.TimeoutErrorType,
            { Exception: HttpRequestException exception } => ToSnakeCase(
                exception.HttpRequestError.ToString()
            ),
            _ => OpenIdMetricAttributes.OtherValue,
        };

    private static string ToSnakeCase(string name)
    {
        var builder = new StringBuilder(name.Length + 8);
        for (var index = 0; index < name.Length; index++)
        {
            var character = name[index];
            if (char.IsUpper(character))
            {
                if (index > 0)
                    builder.Append('_');
                builder.Append(char.ToLowerInvariant(character));
            }
            else
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static TimeoutStrategyOptions CreateTimeout(
        TimeSpan timeout,
        string scope,
        AuthenticationMetrics metrics
    ) =>
        new()
        {
            Timeout = timeout,
            OnTimeout = args =>
            {
                metrics.OutboundTimedOut(scope);
                return ValueTask.CompletedTask;
            },
        };

    private static ResiliencePipeline<HttpResponseMessage> Build(
        OutboundHttpOptions options,
        int maxRetryAttempts,
        TimeProvider timeProvider,
        AuthenticationMetrics metrics
    )
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>
        {
            TimeProvider = timeProvider,
        };
        var totalTimeout = CreateTimeout(
            options.TotalTimeout,
            AuthenticationMetrics.TotalTimeoutScope,
            metrics
        );
        builder.AddTimeout(totalTimeout);

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
                    OnRetry = args =>
                    {
                        var errorType = GetErrorType(args.Outcome);
                        metrics.OutboundRetried(errorType);
                        args.Outcome.Result?.Dispose();
                        return ValueTask.CompletedTask;
                    },
                }
            );
        }

        var attemptTimeout = CreateTimeout(
            options.AttemptTimeout,
            AuthenticationMetrics.AttemptTimeoutScope,
            metrics
        );
        builder.AddTimeout(attemptTimeout);

        return builder.Build();
    }

    private ResiliencePipeline<HttpResponseMessage> Retrying { get; } =
        Build(options.Value, options.Value.MaxRetryAttempts, timeProvider, metrics);

    private ResiliencePipeline<HttpResponseMessage> Bounded { get; } =
        Build(options.Value, maxRetryAttempts: 0, timeProvider, metrics);

    /// <summary>
    /// Selects the pipeline that bounds <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request that is about to be sent.</param>
    /// <returns>The pipeline that runs the request.</returns>
    public ResiliencePipeline<HttpResponseMessage> Select(HttpRequestMessage request) =>
        IsRetryable(request) ? Retrying : Bounded;
}
