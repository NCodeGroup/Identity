// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.ManagementConsole.Options;

/// <summary>
/// Validates <see cref="HostFilteringOptions"/> when the host starts, so a deployment that accepts any <c>Host</c>
/// header stops at startup instead of serving the console for a host name it does not own. A host configured for
/// local development (<c>LocalDevelopment:Enabled</c>) may accept any host.
/// </summary>
internal sealed class ManagementConsoleHostFilteringValidator(
    IOptions<LocalDevelopmentOptions> localDevelopmentOptions
) : IValidateOptions<HostFilteringOptions>
{
    private const string Wildcard = "*";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, HostFilteringOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (localDevelopmentOptions.Value.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        var hosts = options.AllowedHosts;
        if (
            hosts is { Count: > 0 }
            && !hosts.Any(host => string.Equals(host?.Trim(), Wildcard, StringComparison.Ordinal))
        )
        {
            return ValidateOptionsResult.Success;
        }

        var value = hosts is null ? string.Empty : string.Join(';', hosts);
        return ValidateOptionsResult.Fail(
            $"AllowedHosts must list this deployment's host names and must not be empty or contain '{Wildcard}' unless LocalDevelopment:Enabled is true (value: '{value}')."
        );
    }
}
