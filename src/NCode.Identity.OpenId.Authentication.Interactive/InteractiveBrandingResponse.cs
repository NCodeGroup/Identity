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

using System.Text.Json.Serialization;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The JSON payload the bundle fetches to theme itself with its tenant's branding. It has the shape of the frontend
/// bootstrap document's <c>branding</c> member, and a token the tenant leaves unset is omitted.
/// </summary>
/// <param name="DisplayName">The name shown in place of the product's, or <see langword="null"/>.</param>
/// <param name="LogoUrl">The absolute <c>https</c> URI of the logo, or <see langword="null"/>.</param>
/// <param name="PrimaryColor">The primary color, or <see langword="null"/>.</param>
/// <param name="SecondaryColor">The secondary color, or <see langword="null"/>.</param>
/// <param name="Mode">The color mode, <c>light</c> or <c>dark</c>, or <see langword="null"/>.</param>
// See ADR-0194.
internal sealed record InteractiveBrandingResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LogoUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PrimaryColor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SecondaryColor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Mode
)
{
    /// <summary>
    /// Reads the branding tokens from a tenant's effective <paramref name="settings"/>.
    /// </summary>
    public static InteractiveBrandingResponse FromSettings(IReadOnlySettingCollection settings) =>
        new(
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingDisplayName)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingLogoUri)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingPrimaryColor)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingSecondaryColor)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingColorMode))
        );

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
