// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace NCode.Identity.Secrets.AwsKms.Options;

/// <summary>
/// Validates the AWS Key Management Service remote key provider options when the host starts, so a missing or
/// malformed region, or a pending window outside the range that the service allows, stops the host instead of failing
/// the first time a key is created or destroyed. The options are always validated, because registering the provider
/// selects it.
/// </summary>
// See ADR-1471 and ADR-1137 (configuration is validated at startup).
internal sealed partial class AwsKmsRemoteKeyProviderOptionsValidator
    : IValidateOptions<AwsKmsRemoteKeyProviderOptions>
{
    private const int MinimumPendingWindowInDays = 7;
    private const int MaximumPendingWindowInDays = 30;

    // '\z' rather than '$', which also matches before a trailing newline, and '[0-9]' rather than '\d', which accepts
    // any Unicode digit.
    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)+\z")]
    private static partial Regex RegionPattern();

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AwsKmsRemoteKeyProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string>? failures = null;

        if (options.Region is not { } region || !RegionPattern().IsMatch(region))
        {
            failures ??= [];
            failures.Add(
                "SecretProtection:RemoteKeyProviders:AwsKms:Region must be the system name of an AWS region, such as us-west-2."
            );
        }

        if (
            options.PendingWindowInDays
            is < MinimumPendingWindowInDays
                or > MaximumPendingWindowInDays
        )
        {
            failures ??= [];
            failures.Add(
                $"SecretProtection:RemoteKeyProviders:AwsKms:PendingWindowInDays must be from {MinimumPendingWindowInDays} to {MaximumPendingWindowInDays}."
            );
        }

        return failures is null
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
