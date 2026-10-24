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
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Contains the <see cref="SettingKey{TValue}"/> of each setting the interactive surface registers.
/// </summary>
// See ADR-0194.
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
