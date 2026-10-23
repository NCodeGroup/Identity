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

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Validates the options for removing the grants that ended when the host starts, so a negative retention or a prune
/// interval that a timer refuses stops the host instead of failing the first time the prune runs.
/// </summary>
// See ADR-0218 and ADR-0221.
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
