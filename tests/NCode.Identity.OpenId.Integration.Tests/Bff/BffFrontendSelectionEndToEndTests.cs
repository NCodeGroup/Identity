// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff;
using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.Hardening;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.Server;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// Proves that frontends selected by host share one route pattern through real routing in a backend-for-frontend (BFF)
/// host.
/// </summary>
public class BffFrontendSelectionEndToEndTests
{
    private const string ShopOrigin = "https://shop.example";
    private const string AdminOrigin = "https://admin.example";
    private const string AdminKeyRing = "bff-admin";
    private const string AdminContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    #region Jwks Tests

    [Fact]
    public async Task Jwks_OnTheFrontendThatEnablesIt_ReturnsTheKeySet()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync($"{AdminOrigin}/bff/jwks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, body.GetProperty("keys").ValueKind);
    }

    [Fact]
    public async Task Jwks_OnTheFrontendThatDoesNotEnableIt_ReturnsNotFound()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestServer().CreateClient();

        using var response = await client.GetAsync($"{ShopOrigin}/bff/jwks");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    #endregion

    #region User Tests

    [Theory]
    [InlineData(ShopOrigin)]
    [InlineData(AdminOrigin)]
    public async Task User_WithoutASession_ReturnsUnauthorizedOnEachFrontend(string origin)
    {
        await using var app = await StartAsync();
        using var client = app.GetTestServer().CreateClient();

        using var response = await SendAsync(client, $"{origin}/bff/user");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region SecurityHeaders Tests

    [Theory]
    [InlineData(ShopOrigin, null)]
    [InlineData(AdminOrigin, AdminContentSecurityPolicy)]
    public async Task User_ContentSecurityPolicy_IsTheOneOfTheSelectedFrontend(
        string origin,
        string? configuredPolicy
    )
    {
        await using var app = await StartAsync();
        using var client = app.GetTestServer().CreateClient();
        var expected = configuredPolicy ?? new BffSecurityHeadersOptions().ContentSecurityPolicy;

        using var response = await SendAsync(client, $"{origin}/bff/user");

        Assert.Equal(
            expected,
            Assert.Single(response.Headers.GetValues("Content-Security-Policy"))
        );
    }

    #endregion

    #region RequireBffFrontend Tests

    [Fact]
    public async Task LocalApi_LimitedToAnotherFrontend_ReturnsNotFound()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestServer().CreateClient();

        using var response = await SendAsync(client, $"{ShopOrigin}/api/admin");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task LocalApi_LimitedToTheSelectedFrontend_ReturnsUnauthorizedWithoutASession()
    {
        await using var app = await StartAsync();
        using var client = app.GetTestServer().CreateClient();

        using var response = await SendAsync(client, $"{AdminOrigin}/api/admin");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Scaffolding

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string uri)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add(BffDefaults.AntiforgeryHeaderName, BffDefaults.AntiforgeryHeaderValue);
        return await client.SendAsync(request);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder(
            new WebApplicationOptions
            {
                EnvironmentName = Microsoft.Extensions.Hosting.Environments.Development,
            }
        );
        builder.WebHost.UseTestServer();

        const string shop = "Bff:Frontends:shop";
        const string admin = "Bff:Frontends:admin";
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                [$"{shop}:Authority"] = "https://op.example",
                [$"{shop}:PublicOrigin"] = ShopOrigin,
                [$"{shop}:ClientId"] = "shop-client",
                [$"{shop}:ClientAuthentication:Method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                [$"{shop}:ReturnUrls:0"] = "/",
                [$"{admin}:Selection:Rule"] = "host",
                [$"{admin}:Selection:Values:0"] = "admin.example",
                [$"{admin}:Authority"] = "https://op.example",
                [$"{admin}:PublicOrigin"] = AdminOrigin,
                [$"{admin}:ClientId"] = "admin-client",
                [$"{admin}:ClientAuthentication:Method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
                [$"{admin}:ClientAuthentication:KeyRing"] = AdminKeyRing,
                [$"{admin}:ReturnUrls:0"] = "/",
                [$"{admin}:Jwks:Enabled"] = "true",
                [$"{admin}:SecurityHeaders:ContentSecurityPolicy"] = AdminContentSecurityPolicy,
            }
        );

        var (keySet, valueSet, indexSet) = BffTestHost.CreateSharedState();
        var serviceCollection = builder.Services;
        serviceCollection.AddSingleton(keySet);
        serviceCollection.AddSingleton(valueSet);
        serviceCollection.AddSingleton(indexSet);
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            AdminKeyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        serviceProvider
                            .GetRequiredService<ISecretKeyFactory>()
                            .CreateSymmetric(
                                new KeyMetadata { KeyId = "s" },
                                BffTestHost.ClientSecret.AsSpan()
                            ),
                    ])
        );
        serviceCollection.AddIdentityBff(builder.Configuration);

        var app = builder.Build();
        app.UseBffSecurityHeaders();
        app.UseBff();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapBffEndpoints();
        app.MapGet("/api/admin", () => "admin").AsBffApi().RequireBffFrontend("admin");

        await app.StartAsync();
        return app;
    }

    #endregion
}
