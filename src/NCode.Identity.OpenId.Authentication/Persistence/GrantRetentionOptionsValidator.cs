// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Time;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Validates the options for removing the grants that ended when the host starts, so a negative retention or a prune
/// interval that a timer refuses stops the host instead of failing the first time the prune runs.
/// </summary>
// See ADR-1218 and ADR-1221.
internal sealed class GrantRetentionOptionsValidator : IValidateOptions<GrantRetentionOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GrantRetentionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (options.Retention < TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(GrantRetentionOptions)}.{nameof(GrantRetentionOptions.Retention)} ('{GrantRetentionOptions.SectionName}:{nameof(GrantRetentionOptions.Retention)}') must not be negative; it is {options.Retention}."
            );
        }

        if (!TimerPeriod.IsValid(options.PruneInterval))
        {
            failures.Add(
                $"{nameof(GrantRetentionOptions)}.{nameof(GrantRetentionOptions.PruneInterval)} ('{GrantRetentionOptions.SectionName}:{nameof(GrantRetentionOptions.PruneInterval)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.PruneInterval}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
