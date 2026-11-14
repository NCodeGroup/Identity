// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore;
using NCode.Identity.OpenId;

namespace NCode.Identity.ManagementConsole.Options;

/// <summary>
/// Validates <see cref="ManagementConsoleOptions"/> when the host starts, so a setting that cannot work stops the host
/// instead of failing on the first request. A host configured for local development (<c>LocalDevelopment:Enabled</c>)
/// may use an <c>http</c> management API URL.
/// </summary>
internal sealed class ManagementConsoleOptionsValidator(LocalDevelopmentMode localDevelopment)
    : IValidateOptions<ManagementConsoleOptions>
{
    private const string Key = ManagementConsoleDefaults.ConfigurationSectionName;

    private static string Describe(string? url)
    {
        if (!AbsoluteUri.TryParse(url, out var uri))
        {
            return url ?? string.Empty;
        }

        var schemeAndServer = uri.GetComponents(
            UriComponents.SchemeAndServer,
            UriFormat.UriEscaped
        );
        return $"{schemeAndServer}{uri.AbsolutePath}";
    }

    private static bool IsValidManagementApiUrl(string? url, bool isLocalDevelopment)
    {
        if (
            !AbsoluteUri.TryParse(url, out var uri)
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
                isLocalDevelopment
                && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
            );
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ManagementConsoleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];
        var isLocalDevelopment = localDevelopment.Enabled;

        var isWellFormed = IsValidManagementApiUrl(options.ManagementApiUrl, true);
        var isValid = isLocalDevelopment
            ? isWellFormed
            : IsValidManagementApiUrl(options.ManagementApiUrl, false);

        if (!isValid)
        {
            failures.Add(
                isWellFormed
                    ? $"{Key}:ManagementApiUrl is '{Describe(options.ManagementApiUrl)}' and must use 'https' unless LocalDevelopment:Enabled is true."
                    : $"{Key}:ManagementApiUrl must be an absolute https URL without user info, query or fragment; it is '{Describe(options.ManagementApiUrl)}'."
            );
        }

        if (string.IsNullOrWhiteSpace(options.BundleRootPath))
        {
            failures.Add(
                $"{Key}:BundleRootPath must name the directory of the built apps/mgmt bundle; it is '{options.BundleRootPath}'."
            );
        }

        var branding = options.Branding;

        if (branding.LogoUrl is not null && !AbsoluteUri.TryParseHttps(branding.LogoUrl, out _))
        {
            failures.Add(
                $"{Key}:Branding:LogoUrl must be an absolute https URL; it is '{Describe(branding.LogoUrl)}'."
            );
        }

        if (
            branding.FaviconUrl is not null
            && !AbsoluteUri.TryParseHttps(branding.FaviconUrl, out _)
        )
        {
            failures.Add(
                $"{Key}:Branding:FaviconUrl must be an absolute https URL; it is '{Describe(branding.FaviconUrl)}'."
            );
        }

        if (
            branding.Mode is not null
            && !string.Equals(branding.Mode, "light", StringComparison.Ordinal)
            && !string.Equals(branding.Mode, "dark", StringComparison.Ordinal)
        )
        {
            failures.Add(
                $"{Key}:Branding:Mode must be 'light' or 'dark'; it is '{branding.Mode}'."
            );
        }

        if (options.Features is not null)
        {
            var unnamed = options.Features.Keys.FirstOrDefault(string.IsNullOrWhiteSpace);
            if (unnamed is not null)
            {
                failures.Add($"{Key}:Features has a feature without a name; it is '{unnamed}'.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
