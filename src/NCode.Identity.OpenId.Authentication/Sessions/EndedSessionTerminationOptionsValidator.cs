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

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Validates the options for terminating the sessions that ended while no request used them when the host starts, so
/// an interval that a timer refuses stops the host instead of failing the first time the termination runs.
/// </summary>
// See ADR-0223 and ADR-0221.
internal sealed class EndedSessionTerminationOptionsValidator
    : IValidateOptions<EndedSessionTerminationOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, EndedSessionTerminationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (TimerPeriod.IsValid(options.Interval))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"{nameof(EndedSessionTerminationOptions)}.{nameof(EndedSessionTerminationOptions.Interval)} ('{EndedSessionTerminationOptions.SectionName}:{nameof(EndedSessionTerminationOptions.Interval)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.Interval}."
        );
    }
}
