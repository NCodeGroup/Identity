// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// Validates <see cref="ManagementConsoleOptions"/> when the host starts, so a setting that cannot work stops the host
/// instead of failing on the first request.
/// </summary>
internal sealed class ManagementConsoleOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<ManagementConsoleOptions>
{
    private const string Key = ManagementConsoleDefaults.ConfigurationSectionName;

    private static bool IsAbsoluteHttpsUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal);

    private static bool IsValidManagementApiUrl(string? url, bool isDevelopment)
    {
        if (
            string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length != 0
            || uri.Query.Length != 0
            || uri.Fragment.Length != 0
            || url.Contains('?', StringComparison.Ordinal)
            || url.Contains('#', StringComparison.Ordinal)
        )
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            || (
                isDevelopment
                && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            );
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ManagementConsoleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        var isDevelopment = environment.IsDevelopment();

        if (!IsValidManagementApiUrl(options.ManagementApiUrl, isDevelopment))
        {
            failures.Add(
                $"{Key}:ManagementApiUrl must be an absolute https URL without user info, query or fragment."
            );
        }

        if (string.IsNullOrWhiteSpace(options.BundleRootPath))
        {
            failures.Add(
                $"{Key}:BundleRootPath must name the directory of the built apps/mgmt bundle."
            );
        }

        var branding = options.Branding;
        if (branding is not null)
        {
            if (branding.LogoUrl is not null && !IsAbsoluteHttpsUrl(branding.LogoUrl))
            {
                failures.Add($"{Key}:Branding:LogoUrl must be an absolute https URL.");
            }

            if (
                branding.Mode is not null
                && !string.Equals(branding.Mode, "light", StringComparison.Ordinal)
                && !string.Equals(branding.Mode, "dark", StringComparison.Ordinal)
            )
            {
                failures.Add($"{Key}:Branding:Mode must be 'light' or 'dark'.");
            }
        }

        if (options.Features is not null && options.Features.Keys.Any(string.IsNullOrWhiteSpace))
        {
            failures.Add($"{Key}:Features has a feature without a name.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
