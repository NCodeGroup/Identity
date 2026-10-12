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
    /// configured, so a single-origin host needs no explicit configuration.
    /// </summary>
    public static FrontendBootstrapDocument Create(
        FrontendBootstrapOptions options,
        string requestOrigin
    )
    {
        var origin = NormalizeOrigin(requestOrigin);

        var apiBaseUrl = NormalizeOrigin(
            string.IsNullOrWhiteSpace(options.ApiBaseUrl) ? origin : options.ApiBaseUrl
        );
        var issuer = NormalizeOrigin(
            string.IsNullOrWhiteSpace(options.Issuer) ? origin : options.Issuer
        );

        var scopes = options.Scopes is { Length: > 0 } configuredScopes ? configuredScopes : null;

        var branding = options.Branding is { IsEmpty: false } source
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
            ClientId = NullIfEmpty(options.ClientId),
            Scopes = scopes,
            Features = new Dictionary<string, bool>(options.Features, StringComparer.Ordinal),
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
