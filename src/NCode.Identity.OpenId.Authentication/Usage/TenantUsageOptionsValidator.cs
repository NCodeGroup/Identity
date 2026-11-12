// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Time;

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Validates the options for recording tenant usage when the host starts, so a negative retention or a flush interval
/// that a timer refuses stops the host instead of failing the first time the usage is flushed.
/// </summary>
// See ADR-1199 and ADR-1137.
internal sealed class TenantUsageOptionsValidator : IValidateOptions<TenantUsageOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TenantUsageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!TimerPeriod.IsValid(options.FlushInterval))
        {
            failures.Add(
                $"{nameof(TenantUsageOptions)}.{nameof(TenantUsageOptions.FlushInterval)} ('{TenantUsageOptions.SectionName}:{nameof(TenantUsageOptions.FlushInterval)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.FlushInterval}."
            );
        }

        if (options.Retention < TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(TenantUsageOptions)}.{nameof(TenantUsageOptions.Retention)} ('{TenantUsageOptions.SectionName}:{nameof(TenantUsageOptions.Retention)}') must not be negative; it is {options.Retention}."
            );
        }

        if (!TimerPeriod.IsValid(options.PruneInterval))
        {
            failures.Add(
                $"{nameof(TenantUsageOptions)}.{nameof(TenantUsageOptions.PruneInterval)} ('{TenantUsageOptions.SectionName}:{nameof(TenantUsageOptions.PruneInterval)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.PruneInterval}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
