// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Time;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Validates <see cref="AuditRecordOptions"/> when the host starts.
/// </summary>
// See ADR-1303 and ADR-1137.
internal sealed class AuditRecordOptionsValidator : IValidateOptions<AuditRecordOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AuditRecordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!TimerPeriod.IsValid(options.FlushInterval))
        {
            failures.Add(
                $"{nameof(AuditRecordOptions)}.{nameof(AuditRecordOptions.FlushInterval)} ('{AuditRecordOptions.SectionName}:{nameof(AuditRecordOptions.FlushInterval)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.FlushInterval}."
            );
        }

        if (options.QueueCapacity < 1)
        {
            failures.Add(
                $"{nameof(AuditRecordOptions)}.{nameof(AuditRecordOptions.QueueCapacity)} ('{AuditRecordOptions.SectionName}:{nameof(AuditRecordOptions.QueueCapacity)}') must be at least 1; it is {options.QueueCapacity}."
            );
        }

        if (options.Retention < TimeSpan.FromDays(1))
        {
            failures.Add(
                $"{nameof(AuditRecordOptions)}.{nameof(AuditRecordOptions.Retention)} ('{AuditRecordOptions.SectionName}:{nameof(AuditRecordOptions.Retention)}') must be at least one day; it is {options.Retention}."
            );
        }

        if (!TimerPeriod.IsValid(options.PruneInterval))
        {
            failures.Add(
                $"{nameof(AuditRecordOptions)}.{nameof(AuditRecordOptions.PruneInterval)} ('{AuditRecordOptions.SectionName}:{nameof(AuditRecordOptions.PruneInterval)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.PruneInterval}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
