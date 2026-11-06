// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Options;
using NCode.Identity.ManagementConsole.Options;

namespace NCode.Identity.ManagementConsole;

/// <summary>
/// The serializable runtime bootstrap document that the console publishes to its bundle. Property names serialize to
/// the camelCase shape <c>@ncode/config</c> parses; the branding is omitted when absent so the document stays minimal.
/// </summary>
internal sealed record ManagementConsoleBootstrapDocument
{
    [JsonPropertyName("apiBaseUrl")]
    public required string ApiBaseUrl { get; init; }

    [JsonPropertyName("bff")]
    public required ManagementConsoleBffDocument Bff { get; init; }

    [JsonPropertyName("features")]
    public IReadOnlyDictionary<string, bool> Features { get; init; } =
        new Dictionary<string, bool>(StringComparer.Ordinal);

    [JsonPropertyName("branding")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ManagementConsoleBrandingDocument? Branding { get; init; }

    /// <summary>
    /// Builds the bootstrap document from the configured <paramref name="options"/> and <paramref name="bff"/> options.
    /// The API base and the BFF's base path are rooted at the host's <paramref name="pathBase"/>, so the browser
    /// resolves them against the origin that served the bundle.
    /// </summary>
    public static ManagementConsoleBootstrapDocument Create(
        ManagementConsoleOptions options,
        BffOptions bff,
        PathString pathBase
    ) =>
        new()
        {
            ApiBaseUrl = pathBase + ManagementConsoleDefaults.ApiBasePath,
            Bff = new ManagementConsoleBffDocument
            {
                BasePath =
                    pathBase + bff.Frontends[ManagementConsoleDefaults.FrontendName].PathPrefix,
                AntiforgeryHeader = new ManagementConsoleHeaderDocument
                {
                    Name = bff.Antiforgery.HeaderName,
                    Value = bff.Antiforgery.HeaderValue,
                },
            },
            Features = new Dictionary<string, bool>(options.Features, StringComparer.Ordinal),
            Branding = ManagementConsoleBrandingDocument.Create(options.Branding),
        };
}
