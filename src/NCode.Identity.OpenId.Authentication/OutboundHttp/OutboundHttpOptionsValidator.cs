// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Validates the options that bound every outbound call to a caller-supplied URL when the host starts, so a timeout
/// that a resilience pipeline refuses or a retry limit out of range stops the host instead of failing the first call.
/// </summary>
// See ADR-1315.
internal sealed class OutboundHttpOptionsValidator : IValidateOptions<OutboundHttpOptions>
{
    private static readonly TimeSpan MinTimeout = TimeSpan.FromMilliseconds(10);
    private static readonly TimeSpan MaxTimeout = TimeSpan.FromDays(1);

    private const int MaxRetryAttemptsLimit = 10;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OutboundHttpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.AttemptTimeout < MinTimeout || options.AttemptTimeout > MaxTimeout)
        {
            failures.Add(
                $"{nameof(OutboundHttpOptions)}.{nameof(OutboundHttpOptions.AttemptTimeout)} ('{OutboundHttpOptions.SectionName}:{nameof(OutboundHttpOptions.AttemptTimeout)}') must be between {MinTimeout} and {MaxTimeout}, the timeouts a resilience timeout accepts; it is {options.AttemptTimeout}."
            );
        }

        if (options.TotalTimeout < MinTimeout || options.TotalTimeout > MaxTimeout)
        {
            failures.Add(
                $"{nameof(OutboundHttpOptions)}.{nameof(OutboundHttpOptions.TotalTimeout)} ('{OutboundHttpOptions.SectionName}:{nameof(OutboundHttpOptions.TotalTimeout)}') must be between {MinTimeout} and {MaxTimeout}, the timeouts a resilience timeout accepts; it is {options.TotalTimeout}."
            );
        }

        if (options.AttemptTimeout > options.TotalTimeout)
        {
            failures.Add(
                $"{nameof(OutboundHttpOptions)}.{nameof(OutboundHttpOptions.AttemptTimeout)} ('{OutboundHttpOptions.SectionName}:{nameof(OutboundHttpOptions.AttemptTimeout)}') must not be greater than {nameof(OutboundHttpOptions)}.{nameof(OutboundHttpOptions.TotalTimeout)} ('{OutboundHttpOptions.SectionName}:{nameof(OutboundHttpOptions.TotalTimeout)}'); it is {options.AttemptTimeout} and the total is {options.TotalTimeout}."
            );
        }

        if (options.MaxRetryAttempts < 0 || options.MaxRetryAttempts > MaxRetryAttemptsLimit)
        {
            failures.Add(
                $"{nameof(OutboundHttpOptions)}.{nameof(OutboundHttpOptions.MaxRetryAttempts)} ('{OutboundHttpOptions.SectionName}:{nameof(OutboundHttpOptions.MaxRetryAttempts)}') must be between 0 and {MaxRetryAttemptsLimit}; it is {options.MaxRetryAttempts}."
            );
        }

        if (options.RetryDelay < TimeSpan.Zero || options.RetryDelay > MaxTimeout)
        {
            failures.Add(
                $"{nameof(OutboundHttpOptions)}.{nameof(OutboundHttpOptions.RetryDelay)} ('{OutboundHttpOptions.SectionName}:{nameof(OutboundHttpOptions.RetryDelay)}') must be between {TimeSpan.Zero} and {MaxTimeout}; it is {options.RetryDelay}."
            );
        }

        if (
            options.FailureCacheDuration <= TimeSpan.Zero
            || options.FailureCacheDuration > MaxTimeout
        )
        {
            failures.Add(
                $"{nameof(OutboundHttpOptions)}.{nameof(OutboundHttpOptions.FailureCacheDuration)} ('{OutboundHttpOptions.SectionName}:{nameof(OutboundHttpOptions.FailureCacheDuration)}') must be greater than {TimeSpan.Zero} and at most {MaxTimeout}; it is {options.FailureCacheDuration}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
