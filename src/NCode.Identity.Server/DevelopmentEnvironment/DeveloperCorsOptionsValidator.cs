// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.OpenId;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Validates <see cref="DeveloperCorsOptions"/>, so an allowed origin that can never match a browser's <c>Origin</c>
/// header — a trailing slash, a path, a wildcard, a missing scheme — stops the host when it starts instead of leaving
/// the dev server's cross-origin calls failing with no sign of why.
/// </summary>
// See ADR-1137.
internal class DeveloperCorsOptionsValidator : IValidateOptions<DeveloperCorsOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, DeveloperCorsOptions options)
    {
        var failures = new List<string>();

        for (var index = 0; index < options.AllowedOrigins.Length; index++)
        {
            var origin = options.AllowedOrigins[index];
            if (!IsOrigin(origin))
            {
                failures.Add(
                    $"{DeveloperCorsOptions.DefaultSectionName}:{nameof(options.AllowedOrigins)}:{index} '{origin}' "
                        + "is not an origin; write it as a scheme and host with an optional port and no path, query, "
                        + "or trailing slash (for example 'https://app.example.com')."
                );
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsOrigin(string origin) =>
        AbsoluteUri.TryParse(origin, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && !origin.EndsWith('/');
}
