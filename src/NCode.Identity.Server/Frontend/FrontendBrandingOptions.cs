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

namespace NCode.Identity.Server.Frontend;

/// <summary>
/// Configures the brandable theme tokens injected into the frontend bundles at runtime.
/// </summary>
// See ADR-0064 (branding seam).
[PublicAPI]
public sealed class FrontendBrandingOptions
{
    /// <summary>
    /// Gets or sets the product/display name shown in the bundle's chrome.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the logo to render.
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the primary theme color.
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// Gets or sets the secondary theme color.
    /// </summary>
    public string? SecondaryColor { get; set; }

    /// <summary>
    /// Gets or sets the default color mode (<c>light</c> or <c>dark</c>).
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// Gets a value indicating whether any branding token has been set, so an all-empty section is omitted
    /// from the published document.
    /// </summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(DisplayName)
        && string.IsNullOrWhiteSpace(LogoUrl)
        && string.IsNullOrWhiteSpace(PrimaryColor)
        && string.IsNullOrWhiteSpace(SecondaryColor)
        && string.IsNullOrWhiteSpace(Mode);
}
