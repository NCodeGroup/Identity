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
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The branding of the tenant a page is served for, which the host embeds in every page it serves with the
/// single-page bundle so the bundle themes itself without a request. It has the shape of the frontend bootstrap
/// document's <c>branding</c> member, and a token the tenant leaves unset is omitted.
/// </summary>
/// <param name="DisplayName">The name shown in place of the product's, or <see langword="null"/>.</param>
/// <param name="LogoUrl">The absolute <c>https</c> URI of the logo, or <see langword="null"/>.</param>
/// <param name="PrimaryColor">The primary color, or <see langword="null"/>.</param>
/// <param name="SecondaryColor">The secondary color, or <see langword="null"/>.</param>
/// <param name="Mode">The color mode, <c>light</c> or <c>dark</c>, or <see langword="null"/>.</param>
/// <param name="Environment">
/// The tenant's environment when its effective environment type is not a production one, or
/// <see langword="null"/>.
/// </param>
// See ADR-0194, ADR-0253 and ADR-0287.
internal sealed record InteractiveBranding(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LogoUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PrimaryColor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SecondaryColor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        InteractiveEnvironment? Environment
)
{
    /// <summary>
    /// Reads the branding tokens from a tenant's effective <paramref name="settings"/>, and the environment when the
    /// tenant's effective environment type, resolved through <paramref name="tenantEnvironmentCatalog"/>, is not a
    /// production one.
    /// </summary>
    public static InteractiveBranding FromSettings(
        IReadOnlySettingCollection settings,
        ITenantEnvironmentCatalog tenantEnvironmentCatalog
    )
    {
        var type = tenantEnvironmentCatalog.GetEffectiveType(settings);
        var environment = type.IsProduction
            ? null
            : new InteractiveEnvironment(
                type.TypeName,
                NullIfEmpty(settings.GetValue(OpenIdSettingKeys.EnvironmentName))
            );

        return new InteractiveBranding(
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingDisplayName)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingLogoUri)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingPrimaryColor)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingSecondaryColor)),
            NullIfEmpty(settings.GetValue(InteractiveSettingKeys.BrandingColorMode)),
            environment
        );
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
}
