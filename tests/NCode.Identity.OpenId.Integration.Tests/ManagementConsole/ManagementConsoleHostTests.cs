// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.ManagementConsole;

/// <summary>
/// Proves the management console's host over HTTP: the page it serves under a fresh nonce, its bundle's assets, its
/// bootstrap document, and the BFF guard on its session and forwarded-API endpoints.
/// </summary>
public class ManagementConsoleHostTests
{
    private const string CsrfHeaderName = BffDefaults.AntiforgeryHeaderName;
    private const string CsrfHeaderValue = BffDefaults.AntiforgeryHeaderValue;

    private static string GetNonceFromPolicy(string policy)
    {
        const string marker = "'nonce-";
        var start = policy.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = policy.IndexOf('\'', start);
        return policy[start..end];
    }

    #region Page Tests

    [Fact]
    public async Task GetPage_AtAConsoleRoute_ServesTheBundleUnderAFreshNonce()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetAsync("/tenants/acme/clients");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        var nonce = GetNonceFromPolicy(policy);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains($"nonce=\"{nonce}\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("__NCODE_CSP_NONCE__", body, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", policy, StringComparison.Ordinal);
        Assert.Contains("form-action 'self' http://localhost", policy, StringComparison.Ordinal);
        Assert.Contains("no-store", Assert.Single(response.Headers.GetValues("Cache-Control")));
    }

    [Fact]
    public async Task GetPage_Twice_ServesADifferentNonceEachTime()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = factory.CreateBrowserClient();

        using var first = await client.GetAsync("/");
        using var second = await client.GetAsync("/");

        var firstNonce = GetNonceFromPolicy(
            Assert.Single(first.Headers.GetValues("Content-Security-Policy"))
        );
        var secondNonce = GetNonceFromPolicy(
            Assert.Single(second.Headers.GetValues("Content-Security-Policy"))
        );
        Assert.NotEqual(firstNonce, secondNonce);
    }

    #endregion

    #region Asset Tests

    [Fact]
    public async Task GetAsset_OfTheBundle_IsServedImmutable()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetAsync(ManagementConsoleApplicationFactory.AssetPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var assetBody = await response.Content.ReadAsStringAsync();
        Assert.Equal(ManagementConsoleApplicationFactory.AssetContent, assetBody);
        var cacheControl = Assert.Single(response.Headers.GetValues("Cache-Control"));
        Assert.Contains("immutable", cacheControl, StringComparison.Ordinal);
        Assert.Contains("max-age=31536000", cacheControl, StringComparison.Ordinal);
    }

    #endregion

    #region Bootstrap Tests

    [Fact]
    public async Task GetBootstrap_Always_PublishesTheBffDocument()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetAsync("/config/bootstrap.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var root = JsonElement.Parse(body);
        Assert.Equal("/management", root.GetProperty("apiBaseUrl").GetString());
        var bff = root.GetProperty("bff");
        Assert.Equal("/bff", bff.GetProperty("basePath").GetString());
        var header = bff.GetProperty("antiforgeryHeader");
        Assert.Equal("X-CSRF", header.GetProperty("name").GetString());
        Assert.Equal("1", header.GetProperty("value").GetString());
        Assert.Equal(
            "NCode.Identity (dev)",
            root.GetProperty("branding").GetProperty("displayName").GetString()
        );
    }

    #endregion

    #region Session Tests

    [Fact]
    public async Task GetUser_WithoutASession_IsUnauthorized()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = factory.CreateBrowserClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/user");
        request.Headers.Add(CsrfHeaderName, CsrfHeaderValue);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Management API Tests

    [Fact]
    public async Task GetManagementApi_WithoutASession_IsUnauthorized()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = factory.CreateBrowserClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/management/api/caller");
        request.Headers.Add(CsrfHeaderName, CsrfHeaderValue);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetManagementApi_WithoutTheAntiforgeryHeader_IsForbidden()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        using var client = factory.CreateBrowserClient();

        using var response = await client.GetAsync("/management/api/caller");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region RemoteKeyProviders Tests

    [Fact]
    public async Task Services_WhenEveryRemoteKeyProviderSectionIsConfigured_StartsWithEveryProviderRegistered()
    {
        await using var factory = new ManagementConsoleApplicationFactory();
        await using var derived = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("SecretProtection:RemoteKeyProviders:AwsKms:Region", "us-west-2");
            builder.UseSetting(
                "SecretProtection:RemoteKeyProviders:AzureKeyVault:VaultUri",
                "https://contoso.vault.azure.net"
            );
            builder.UseSetting(
                "SecretProtection:RemoteKeyProviders:GoogleCloudKms:KeyRing",
                "projects/p/locations/l/keyRings/r"
            );
        });

        var catalog = derived.Services.GetRequiredService<IRemoteKeyProviderCatalog>();

        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AwsKms, out _));
        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.AzureKeyVault, out _));
        Assert.True(catalog.TryGetProvider(RemoteKeyProviderNames.GoogleCloudKms, out _));
    }

    #endregion
}
