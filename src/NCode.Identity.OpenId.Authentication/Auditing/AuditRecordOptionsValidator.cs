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
