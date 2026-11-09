// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Time;

namespace NCode.Identity.Secrets.Persistence.Options;

/// <summary>
/// Validates the remote key options when the host starts: the deployment name is present when a remote key provider
/// that creates keys is registered and has the shape that scopes the keys the server creates, and the sweep interval
/// and grace period are within the ranges the sweep relies on. Every message names the full configuration key.
/// </summary>
// See ADR-1478.
internal sealed partial class RemoteKeyOptionsValidator(
    IRemoteKeyDescriptorCatalog remoteKeyDescriptorCatalog
) : IValidateOptions<SecretProtectionOptions>
{
    /// <summary>
    /// The longest deployment name, in characters.
    /// </summary>
    internal const int MaximumDeploymentLength = 40;

    /// <summary>
    /// The shortest sweep interval.
    /// </summary>
    internal static TimeSpan MinimumSweepInterval { get; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// The shortest grace period.
    /// </summary>
    internal static TimeSpan MinimumSweepGracePeriod { get; } = TimeSpan.FromHours(1);

    /// <summary>
    /// The longest grace period.
    /// </summary>
    internal static TimeSpan MaximumSweepGracePeriod { get; } = TimeSpan.FromDays(365);

    // '\z' rather than '$', which also matches before a trailing newline.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*\z")]
    private static partial Regex DeploymentPattern();

    private static string Key(string member) =>
        $"{SecretProtectionOptions.DefaultSectionName}:{nameof(SecretProtectionOptions.RemoteKeys)}:{member}";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SecretProtectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var keys = options.RemoteKeys;
        List<string> failures = [];

        if (string.IsNullOrEmpty(keys.Deployment))
        {
            if (remoteKeyDescriptorCatalog.ProvidersThatCreateKeys.Any())
            {
                failures.Add(
                    $"{Key(nameof(RemoteKeyOptions.Deployment))} is required because a remote key provider that creates keys is registered."
                );
            }
        }
        else if (
            keys.Deployment.Length > MaximumDeploymentLength
            || !DeploymentPattern().IsMatch(keys.Deployment)
        )
        {
            failures.Add(
                $"{Key(nameof(RemoteKeyOptions.Deployment))} must be lowercase letters and digits in hyphen-separated groups, at most {MaximumDeploymentLength} characters; it is '{keys.Deployment}'."
            );
        }

        if (keys.SweepInterval < MinimumSweepInterval || !TimerPeriod.IsValid(keys.SweepInterval))
        {
            failures.Add(
                $"{Key(nameof(RemoteKeyOptions.SweepInterval))} must be between {MinimumSweepInterval} and {TimerPeriod.MaxValue}; it is {keys.SweepInterval}."
            );
        }

        if (
            keys.SweepGracePeriod < MinimumSweepGracePeriod
            || keys.SweepGracePeriod > MaximumSweepGracePeriod
        )
        {
            failures.Add(
                $"{Key(nameof(RemoteKeyOptions.SweepGracePeriod))} must be between {MinimumSweepGracePeriod} and {MaximumSweepGracePeriod}; it is {keys.SweepGracePeriod}."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
