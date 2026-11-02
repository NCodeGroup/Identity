// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Client.Metrics;
using NCode.Identity.OpenId.Client.Options;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// Holds the resilience pipelines that bound a request to an OpenID Provider and selects the one that fits a request. A
/// request that is safe to resend — a GET or HEAD without a body or a DPoP proof, while retries are turned on — runs
/// through the total timeout, a bounded and jittered retry, and a per-attempt timeout; any other request runs through
/// the total timeout only, so it is never sent twice.
/// </summary>
/// <param name="options">The <see cref="OpenIdClientOptions"/> that hold the limits.</param>
/// <param name="metrics">The <see cref="ProviderMetrics"/> that count the retries and the timeouts.</param>
// See ADR-1336.
internal sealed class ProviderResiliencePipelines(
    IOptions<OpenIdClientOptions> options,
    ProviderMetrics metrics
)
{
    private ResiliencePipeline<HttpResponseMessage> Retrying { get; } =
        Build(options.Value, options.Value.MaxRetryAttempts, metrics);

    private ResiliencePipeline<HttpResponseMessage> Bounded { get; } =
        Build(options.Value, maxRetryAttempts: 0, metrics);

    /// <summary>
    /// Selects the pipeline that bounds <paramref name="request"/>.
    /// </summary>
    /// <param name="request">The request that is about to be sent.</param>
    /// <returns>The pipeline that runs the request.</returns>
    public ResiliencePipeline<HttpResponseMessage> Select(HttpRequestMessage request) =>
        IsRetryable(request) ? Retrying : Bounded;

    /// <summary>
    /// Determines whether <paramref name="request"/> may be sent again after a failed attempt. A request with a body is
    /// never resent, because a body's content may be a stream that one attempt consumes. A DPoP proof carries a
    /// <c>jti</c> that is single-use (RFC 9449 §11.1), so a request that carries one is never resent either.
    /// </summary>
    /// <param name="request">The request that is about to be sent.</param>
    /// <returns>
    /// <c>true</c> when the request is a GET or HEAD without a body and without a DPoP proof; otherwise, <c>false</c>.
    /// </returns>
    internal static bool IsRetryable(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isSafeMethod = request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;
        return isSafeMethod
            && request.Content is null
            && !request.Headers.Contains(OpenIdProtocol.DPoP.HeaderName);
    }

    /// <summary>
    /// Determines whether the outcome of an attempt is transient and may be retried: a failed connection, a request
    /// that the per-attempt timeout cancelled, or a 408, 429 or 5xx response.
    /// </summary>
    /// <param name="outcome">The outcome of the attempt.</param>
    /// <returns><c>true</c> when the outcome is transient; otherwise, <c>false</c>.</returns>
    internal static bool IsTransient(Outcome<HttpResponseMessage> outcome)
    {
        if (outcome.Exception is HttpRequestException or TimeoutRejectedException)
            return true;

        if (outcome.Result is not { } response)
            return false;

        var statusCode = (int)response.StatusCode;
        return statusCode is 408 or 429 or >= 500;
    }

    /// <summary>
    /// Gets the bounded <c>error.type</c> of a failed attempt: the response's status code, <c>timeout</c> for an attempt
    /// that the per-attempt timeout cancelled, the <see cref="HttpRequestError"/> in snake case for a failed request,
    /// or <c>_OTHER</c>.
    /// </summary>
    /// <param name="outcome">The outcome of the attempt.</param>
    /// <returns>The error type.</returns>
    // NCode.Identity.OpenId.Authentication's GuardedResiliencePipelines.GetErrorType maps outcomes the same way; this
    // package cannot reference it (ADR-1285), so the two copies are kept in step, and both test suites pin the values.
    internal static string GetErrorType(Outcome<HttpResponseMessage> outcome) =>
        outcome switch
        {
            { Result: { } response } => ((int)response.StatusCode).ToString(
                CultureInfo.InvariantCulture
            ),
            { Exception: TimeoutRejectedException } => ProviderMetrics.TimeoutErrorType,
            { Exception: HttpRequestException exception } => ToSnakeCase(
                exception.HttpRequestError.ToString()
            ),
            _ => ProviderMetrics.OtherErrorType,
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
        ProviderMetrics metrics
    ) =>
        new()
        {
            Timeout = timeout,
            OnTimeout = args =>
            {
                metrics.TimedOut(scope);
                return ValueTask.CompletedTask;
            },
        };

    private static ResiliencePipeline<HttpResponseMessage> Build(
        OpenIdClientOptions options,
        int maxRetryAttempts,
        ProviderMetrics metrics
    )
    {
        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>();
        var totalTimeout = CreateTimeout(
            options.TotalTimeout,
            ProviderMetrics.TotalTimeoutScope,
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
                        metrics.Retried(errorType);
                        args.Outcome.Result?.Dispose();
                        return ValueTask.CompletedTask;
                    },
                }
            );
            var attemptTimeout = CreateTimeout(
                options.AttemptTimeout,
                ProviderMetrics.AttemptTimeoutScope,
                metrics
            );
            builder.AddTimeout(attemptTimeout);
        }

        return builder.Build();
    }
}
