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

using System.Text.RegularExpressions;
using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the settings the interactive surface registers:
/// the branding a tenant shows its end-users. Each is a tenant setting whose empty default leaves the surface's own
/// theme, and each value is checked against the shape the bundle themes with, so a stored value always renders.
/// </summary>
// See ADR-0194.
internal partial class InteractiveSettingDescriptorDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<SettingDescriptor>
{
    private const string LightColorMode = "light";
    private const string DarkColorMode = "dark";

    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // branding_display_name: the name the surface shows in place of the product's. Not advertised. Override
            // (Replace): a child fully replaces the parent value.
            yield return BrandingDescriptor(
                InteractiveSettingNames.BrandingDisplayName,
                SettingRules.Must<string>(
                    "at most 100 characters with no control characters",
                    value => value.Length <= 100 && !value.Any(char.IsControl)
                )
            );

            // branding_logo_uri: the logo the surface shows, fetched by the end-user's browser, so it must be served
            // over TLS like the page that shows it.
            yield return BrandingDescriptor(
                InteractiveSettingNames.BrandingLogoUri,
                SettingRules.Must<string>(
                    "an absolute https URI",
                    value =>
                        Uri.TryCreate(value, UriKind.Absolute, out var uri)
                        && uri.Scheme == Uri.UriSchemeHttps
                )
            );

            // branding_primary_color / branding_secondary_color: the theme's palette colors. Hexadecimal only, the one
            // notation every theme engine reads the same way.
            yield return BrandingDescriptor(
                InteractiveSettingNames.BrandingPrimaryColor,
                HexColor()
            );
            yield return BrandingDescriptor(
                InteractiveSettingNames.BrandingSecondaryColor,
                HexColor()
            );

            // branding_color_mode: the theme's color mode.
            yield return BrandingDescriptor(
                InteractiveSettingNames.BrandingColorMode,
                SettingRules.OneOf(LightColorMode, DarkColorMode)
            );
        }
    }

    private static SettingDescriptor<string> BrandingDescriptor(
        string name,
        SettingRule<string> rule
    ) =>
        new()
        {
            Name = name,
            AppliesTo = [OpenIdSettingScopes.Tenant],
            Rule = SettingRules.EmptyOr(rule),
            Default = string.Empty,

            IsDiscoverable = false,
            OnMerge = SettingMerge.Replace,
        };

    private static SettingRule<string> HexColor() =>
        SettingRules.Must<string>(
            "a hexadecimal color such as #1565c0",
            value => HexColorRegex().IsMatch(value)
        );

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.CultureInvariant)]
    private static partial Regex HexColorRegex();
}
