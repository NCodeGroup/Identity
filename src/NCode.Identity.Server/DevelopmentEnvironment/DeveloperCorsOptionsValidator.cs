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

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// Validates <see cref="DeveloperCorsOptions"/>, so an allowed origin that can never match a browser's <c>Origin</c>
/// header — a trailing slash, a path, a wildcard, a missing scheme — stops the host when it starts instead of leaving
/// the dev server's cross-origin calls failing with no sign of why.
/// </summary>
// See ADR-0137.
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
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsolutePath == "/"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment)
        && !origin.EndsWith('/');
}
