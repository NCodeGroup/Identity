// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Authentication.Auditing;

/// <summary>
/// Validates <see cref="AuditRecordOptions"/> when the host starts.
/// </summary>
internal sealed class AuditRecordOptionsValidator : IValidateOptions<AuditRecordOptions>
{
    // The longest period a timer accepts.
    private static readonly TimeSpan MaxTimerPeriod = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AuditRecordOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (
            options.FlushInterval < TimeSpan.FromMilliseconds(1)
            || options.FlushInterval > MaxTimerPeriod
        )
        {
            failures.Add(
                $"OpenId:{AuditRecordOptions.SectionName}:FlushInterval must be at least one millisecond and at most {MaxTimerPeriod:c}."
            );
        }

        if (options.QueueCapacity < 1)
        {
            failures.Add(
                $"OpenId:{AuditRecordOptions.SectionName}:QueueCapacity must be at least one."
            );
        }

        if (options.Retention < TimeSpan.FromDays(1))
        {
            failures.Add(
                $"OpenId:{AuditRecordOptions.SectionName}:Retention must be at least one day."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
