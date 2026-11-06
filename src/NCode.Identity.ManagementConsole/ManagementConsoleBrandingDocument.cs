// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The serializable branding tokens embedded in the console's bootstrap document.
/// </summary>
internal sealed record ManagementConsoleBrandingDocument
{
    [JsonPropertyName("displayName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DisplayName { get; init; }

    [JsonPropertyName("logoUrl")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LogoUrl { get; init; }

    [JsonPropertyName("primaryColor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PrimaryColor { get; init; }

    [JsonPropertyName("secondaryColor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SecondaryColor { get; init; }

    [JsonPropertyName("mode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Mode { get; init; }

    /// <summary>
    /// Builds the branding document from the configured <paramref name="options"/>, omitting every blank value, or
    /// returns <see langword="null"/> when every value is blank.
    /// </summary>
    public static ManagementConsoleBrandingDocument? Create(
        ManagementConsoleBrandingOptions options
    )
    {
        var branding = new ManagementConsoleBrandingDocument
        {
            DisplayName = NullIfBlank(options.DisplayName),
            LogoUrl = NullIfBlank(options.LogoUrl),
            PrimaryColor = NullIfBlank(options.PrimaryColor),
            SecondaryColor = NullIfBlank(options.SecondaryColor),
            Mode = NullIfBlank(options.Mode),
        };

        return
            branding
                is {
                    DisplayName: null,
                    LogoUrl: null,
                    PrimaryColor: null,
                    SecondaryColor: null,
                    Mode: null
                }
            ? null
            : branding;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
