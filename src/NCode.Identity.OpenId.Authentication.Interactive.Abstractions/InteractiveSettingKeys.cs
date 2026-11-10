// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Contains the <see cref="SettingKey{TValue}"/> of each setting the interactive surface registers.
/// </summary>
// See ADR-1194.
[PublicAPI]
public static class InteractiveSettingKeys
{
    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'branding_display_name' setting.
    /// </summary>
    public static SettingKey<string> BrandingDisplayName =>
        new(InteractiveSettingNames.BrandingDisplayName);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'branding_logo_uri' setting.
    /// </summary>
    public static SettingKey<string> BrandingLogoUri =>
        new(InteractiveSettingNames.BrandingLogoUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'branding_favicon_uri' setting.
    /// </summary>
    public static SettingKey<string> BrandingFaviconUri =>
        new(InteractiveSettingNames.BrandingFaviconUri);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'branding_primary_color' setting.
    /// </summary>
    public static SettingKey<string> BrandingPrimaryColor =>
        new(InteractiveSettingNames.BrandingPrimaryColor);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'branding_secondary_color' setting.
    /// </summary>
    public static SettingKey<string> BrandingSecondaryColor =>
        new(InteractiveSettingNames.BrandingSecondaryColor);

    /// <summary>
    /// Gets the <see cref="SettingKey{TValue}"/> for the 'branding_color_mode' setting.
    /// </summary>
    public static SettingKey<string> BrandingColorMode =>
        new(InteractiveSettingNames.BrandingColorMode);
}
