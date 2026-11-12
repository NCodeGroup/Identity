// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Time;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates the master key lifecycle options when the host starts: the rotation period, the refresh interval, the
/// retirement grace and the maintenance interval are within the ranges the lifecycle relies on. Every message names
/// the full configuration key.
/// </summary>
// See ADR-1137 (intervals and retentions are configured and validated when the host starts) and ADR-1407.
internal sealed class MasterKeyOptionsValidator : IValidateOptions<SecretProtectionOptions>
{
    internal static TimeSpan MinimumRotationPeriod { get; } = TimeSpan.FromDays(1);
    internal static TimeSpan MinimumRefreshInterval { get; } = TimeSpan.FromSeconds(1);
    internal static TimeSpan MaximumRefreshInterval { get; } = TimeSpan.FromHours(1);
    internal static TimeSpan MinimumMaintenanceInterval { get; } = TimeSpan.FromMinutes(1);

    private static string Key(string member) =>
        $"{SecretProtectionOptions.DefaultSectionName}:{nameof(SecretProtectionOptions.MasterKeys)}:{member}";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SecretProtectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var keys = options.MasterKeys;
        List<string> failures = [];

        if (keys.RotationPeriod < MinimumRotationPeriod)
        {
            failures.Add(
                $"{Key(nameof(MasterKeyOptions.RotationPeriod))} must be at least {MinimumRotationPeriod}; it is {keys.RotationPeriod}."
            );
        }

        if (
            keys.RefreshInterval < MinimumRefreshInterval
            || keys.RefreshInterval > MaximumRefreshInterval
        )
        {
            failures.Add(
                $"{Key(nameof(MasterKeyOptions.RefreshInterval))} must be between {MinimumRefreshInterval} and {MaximumRefreshInterval}; it is {keys.RefreshInterval}."
            );
        }

        if (keys.RetirementGrace < keys.RefreshInterval * 2)
        {
            failures.Add(
                $"{Key(nameof(MasterKeyOptions.RetirementGrace))} must be at least twice {Key(nameof(MasterKeyOptions.RefreshInterval))} ({keys.RefreshInterval * 2}); it is {keys.RetirementGrace}."
            );
        }

        if (
            keys.MaintenanceInterval < MinimumMaintenanceInterval
            || !TimerPeriod.IsValid(keys.MaintenanceInterval)
        )
        {
            failures.Add(
                $"{Key(nameof(MasterKeyOptions.MaintenanceInterval))} must be between {MinimumMaintenanceInterval} and {TimerPeriod.MaxValue}; it is {keys.MaintenanceInterval}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
