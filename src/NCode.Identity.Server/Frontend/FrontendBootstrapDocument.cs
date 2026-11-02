// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Server.Frontend;

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
    /// Builds the bootstrap document from the configured <paramref name="options"/> and the resolved
    /// <paramref name="issuer"/>. When <see cref="FrontendBootstrapOptions.ApiBaseUrl"/> is not configured, the API base
    /// is the host's <paramref name="pathBase"/>, root-relative (<c>/</c> at the root), so the browser resolves it
    /// against the origin that served the bundle and the request's <c>Host</c> contributes nothing. When
    /// <paramref name="surface"/> names a configured <see cref="FrontendBootstrapOptions.Surfaces"/> entry, that
    /// surface's coordinates (client id, scopes, feature flags, branding) are published instead of the host default; an
    /// unknown or absent surface receives the default.
    /// </summary>
    // See ADR-1312.
    public static FrontendBootstrapDocument Create(
        FrontendBootstrapOptions options,
        string issuer,
        PathString pathBase,
        string? surface = null
    )
    {
        var apiBaseUrl = string.IsNullOrWhiteSpace(options.ApiBaseUrl)
            ? (pathBase.HasValue ? pathBase.Value : "/")
            : NormalizeOrigin(options.ApiBaseUrl);
        var normalizedIssuer = NormalizeOrigin(issuer);

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
            Issuer = normalizedIssuer,
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
