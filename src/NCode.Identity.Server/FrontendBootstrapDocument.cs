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

namespace NCode.Identity.Server;

/// <summary>
/// The serializable runtime bootstrap document published to the frontend bundles. Property names serialize to the
/// camelCase shape <c>@ncode/config</c> parses; optional members are omitted when absent so the document stays minimal.
/// </summary>
internal sealed record FrontendBootstrapDocument
{
    [JsonPropertyName("apiBaseUrl")]
    public required string ApiBaseUrl { get; init; }

    [JsonPropertyName("issuer")]
    public required string Issuer { get; init; }

    [JsonPropertyName("clientId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientId { get; init; }

    [JsonPropertyName("scopes")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Scopes { get; init; }

    [JsonPropertyName("features")]
    public IReadOnlyDictionary<string, bool> Features { get; init; } =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    [JsonPropertyName("branding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public FrontendBrandingDocument? Branding { get; init; }

    /// <summary>
    /// Builds the bootstrap document from the configured <paramref name="options"/>, falling back to the serving
    /// <paramref name="requestOrigin"/> (<c>scheme://host</c>) for the API base and issuer when they are not
    /// configured, so a single-origin host needs no explicit configuration. When <paramref name="surface"/> names a
    /// configured <see cref="FrontendBootstrapOptions.Surfaces"/> entry, that surface's coordinates (client id,
    /// scopes, feature flags, branding) are published instead of the host default; an unknown or absent surface
    /// receives the default.
    /// </summary>
    public static FrontendBootstrapDocument Create(
        FrontendBootstrapOptions options,
        string requestOrigin,
        string? surface = null
    )
    {
        var origin = NormalizeOrigin(requestOrigin);

        var apiBaseUrl = NormalizeOrigin(
            string.IsNullOrWhiteSpace(options.ApiBaseUrl) ? origin : options.ApiBaseUrl
        );
        var issuer = NormalizeOrigin(
            string.IsNullOrWhiteSpace(options.Issuer) ? origin : options.Issuer
        );

        var overrides =
            surface is not null && options.Surfaces.TryGetValue(surface, out var surfaceOptions)
                ? surfaceOptions
                : null;

        // A surface override REPLACES the default client id and scopes (so a read-only surface can publish none),
        // while feature flags merge per key and branding falls back to the default when the surface sets none.
        var clientId = overrides is not null ? overrides.ClientId : options.ClientId;

        var scopeSource = overrides?.Scopes ?? options.Scopes;
        var scopes = scopeSource is { Length: > 0 } configuredScopes ? configuredScopes : null;

        var features = new Dictionary<string, bool>(options.Features, StringComparer.Ordinal);
        if (overrides?.Features is { } surfaceFeatures)
        {
            foreach (var (key, value) in surfaceFeatures)
            {
                features[key] = value;
            }
        }

        var brandingSource = overrides?.Branding is { IsEmpty: false } surfaceBranding
            ? surfaceBranding
            : options.Branding;

        var branding = brandingSource is { IsEmpty: false } source
            ? new FrontendBrandingDocument
            {
                DisplayName = NullIfEmpty(source.DisplayName),
                LogoUrl = NullIfEmpty(source.LogoUrl),
                PrimaryColor = NullIfEmpty(source.PrimaryColor),
                SecondaryColor = NullIfEmpty(source.SecondaryColor),
                Mode = NullIfEmpty(source.Mode),
            }
            : null;

        return new FrontendBootstrapDocument
        {
            ApiBaseUrl = apiBaseUrl,
            Issuer = issuer,
            ClientId = NullIfEmpty(clientId),
            Scopes = scopes,
            Features = features,
            Branding = branding,
        };
    }

    private static string NormalizeOrigin(string value) =>
        value.EndsWith('/') ? value.TrimEnd('/') : value;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>
/// The serializable branding tokens embedded in the frontend bootstrap document.
/// </summary>
internal sealed record FrontendBrandingDocument
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
}
