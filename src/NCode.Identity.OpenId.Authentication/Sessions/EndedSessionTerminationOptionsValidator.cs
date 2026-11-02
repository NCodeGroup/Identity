// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Validates the options for terminating the sessions that ended while no request used them when the host starts, so
/// an interval that a timer refuses stops the host instead of failing the first time the termination runs.
/// </summary>
// See ADR-1223 and ADR-1221.
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
