// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Server;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public sealed class ApiReferenceEndpointTests
{
    private const string BaseAddress = "https://op.integration.test/";

    // The base address's host is the one the strict posture's explicit AllowedHosts admits.
    private const string HostName = "op.integration.test";

    private static Dictionary<string, string?> StrictSettings(
        params (string Key, string? Value)[] extra
    )
    {
        var settings = new Dictionary<string, string?> { ["AllowedHosts"] = HostName };
        foreach (var (key, value) in extra)
        {
            settings[key] = value;
        }

        return settings;
    }

    #region Map Tests

    [Fact]
    public async Task GetOpenApiDocument_WithoutLocalDevelopmentByDefault_ReturnsNotFound()
    {
        using var factory = new StrictPostureHostApplicationFactory(StrictSettings());
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetApiReferencePage_WithoutLocalDevelopmentByDefault_ReturnsNotFound()
    {
        using var factory = new StrictPostureHostApplicationFactory(StrictSettings());
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(IdentityServerApiReferenceOptions.PagePath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOpenApiDocument_WhenEnabledWithoutLocalDevelopment_ReturnsTheDocument()
    {
        using var factory = new StrictPostureHostApplicationFactory(
            StrictSettings(("ApiReference:DocumentEnabled", "true"))
        );
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetApiReferencePage_WhenOnlyTheDocumentIsEnabled_ReturnsNotFound()
    {
        using var factory = new StrictPostureHostApplicationFactory(
            StrictSettings(("ApiReference:DocumentEnabled", "true"))
        );
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(IdentityServerApiReferenceOptions.PagePath);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetApiReferencePage_WithLocalDevelopment_SendsAStrictContentSecurityPolicyWithItsNonce()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(IdentityServerApiReferenceOptions.PagePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        var match = Regex.Match(csp, "'nonce-([A-Za-z0-9_-]+)'");
        Assert.True(match.Success);
        var nonce = match.Groups[1].Value;

        Assert.Equal(
            $"default-src 'none'; script-src 'nonce-{nonce}' 'strict-dynamic'; style-src 'nonce-{nonce}'; img-src 'self' data:; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            csp
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains($"nonce=\"{nonce}\"", body);
        Assert.Contains($"<meta property=\"csp-nonce\" content=\"{nonce}\"", body);
    }

    [Fact]
    public async Task GetApiReferencePage_WithLocalDevelopment_NamesNoExternalOrigin()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(IdentityServerApiReferenceOptions.PagePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var externalUrls = Regex
            .Matches(body, "https?://[^\"'\\s<>]+")
            .Select(match => match.Value)
            .Where(url => !url.StartsWith("https://op.integration.test", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(externalUrls);
    }

    [Fact]
    public async Task GetApiReferencePage_WithLocalDevelopment_DisablesTelemetryAgentMcpAndDefaultFonts()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(IdentityServerApiReferenceOptions.PagePath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"withDefaultFonts\":false", body);
        Assert.Contains("\"telemetry\":false", body);
        Assert.Contains("\"agent\":{\"disabled\":true}", body);
        Assert.Contains("\"mcp\":{\"disabled\":true}", body);
    }

    #endregion

    private static HttpClient CreateClient(WebApplicationFactory<HostApiMarker> factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri(BaseAddress),
                AllowAutoRedirect = false,
            }
        );
}
