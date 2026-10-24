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
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Usage;

/// <summary>
/// Validates the options for recording tenant usage when the host starts, so a negative retention or a flush interval
/// that a timer refuses stops the host instead of failing the first time the usage is flushed.
/// </summary>
// See ADR-0199 and ADR-0221.
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

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
