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

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
