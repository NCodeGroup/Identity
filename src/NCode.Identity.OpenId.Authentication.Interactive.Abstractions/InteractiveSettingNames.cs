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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Contains the names of the settings the interactive surface registers: the branding a tenant shows its end-users on
/// the login, consent, and account pages. Each is a tenant setting, and an empty value leaves the surface's own default.
/// </summary>
// See ADR-0194.
[PublicAPI]
public static class InteractiveSettingNames
{
    /// <summary>
    /// Contains the name of the <c>branding_display_name</c> setting: the name the surface shows its end-users in place
    /// of the product's own.
    /// </summary>
    public const string BrandingDisplayName = "branding_display_name";

    /// <summary>
    /// Contains the name of the <c>branding_logo_uri</c> setting: the absolute <c>https</c> URI of the logo the surface
    /// shows.
    /// </summary>
    public const string BrandingLogoUri = "branding_logo_uri";

    /// <summary>
    /// Contains the name of the <c>branding_primary_color</c> setting: the surface's primary color, as a hexadecimal
    /// color such as <c>#1565c0</c>.
    /// </summary>
    public const string BrandingPrimaryColor = "branding_primary_color";

    /// <summary>
    /// Contains the name of the <c>branding_secondary_color</c> setting: the surface's secondary color, as a
    /// hexadecimal color such as <c>#9c27b0</c>.
    /// </summary>
    public const string BrandingSecondaryColor = "branding_secondary_color";

    /// <summary>
    /// Contains the name of the <c>branding_color_mode</c> setting: the surface's color mode, <c>light</c> or
    /// <c>dark</c>.
    /// </summary>
    public const string BrandingColorMode = "branding_color_mode";
}
