// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Setup;
using NCode.Identity.OpenId.Setup.Contracts;
using NCode.Identity.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// An uninitialized instance is set up once through /setup with a one-time token, which creates the root global
// administrator and ends the surface (ADR-1512).
public class InstanceSetupTests
{
    private const string SetupToken = "integration-test-setup-token-0123456789";
    private const string RootUserName = "root-admin";
    private const string RootPassword = "root-admin-password-0123";
    private const string StatusPath = "/setup/api/status";
    private const string RootPath = "/setup/api/root";
    private const string TokenPath = "/setup/api/token";
    private const string PagePath = "/setup/";
    private const string ConsoleBaseUrl = "https://console.example.test";
    private const string IndexHtml =
        "<!doctype html><html><head><meta property=\"csp-nonce\" content=\"__NCODE_CSP_NONCE__\"></head><body></body></html>";

    #region Scaffolding

    private static ConfiguredHostApplicationFactory CreateFactory(
        IReadOnlyDictionary<string, string?>? extraSettings = null
    )
    {
        var settings = new Dictionary<string, string?>
        {
            [$"{SetupOptions.SectionName}:Token"] = SetupToken,
        };

        if (extraSettings is not null)
        {
            foreach (var (key, value) in extraSettings)
                settings[key] = value;
        }

        return new ConfiguredHostApplicationFactory(settings);
    }

    private static ConfiguredHostApplicationFactory CreateConsoleHintedFactory() =>
        CreateFactory(
            new Dictionary<string, string?>
            {
                [$"{SetupOptions.SectionName}:Wizard:ManagementConsole:BaseUrl"] = ConsoleBaseUrl,
            }
        );

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async ValueTask<HttpResponseMessage> PostRootAsync(
        HttpClient client,
        string token,
        CancellationToken cancellationToken
    ) =>
        await client.PostAsJsonAsync(
            RootPath,
            new CreateRootRequest
            {
                Token = token,
                UserName = RootUserName,
                Email = "root-admin@example.test",
                Password = RootPassword,
            },
            cancellationToken
        );

    private static async ValueTask<HttpResponseMessage> PostTokenAsync(
        HttpClient client,
        string token,
        CancellationToken cancellationToken
    ) =>
        await client.PostAsJsonAsync(
            TokenPath,
            new CheckSetupTokenRequest { Token = token },
            cancellationToken
        );

    #endregion

    #region Status

    [Fact]
    public async Task GetStatus_WhenTheInstanceIsUninitialized_ReturnsTheConfiguredTokenSource()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync(StatusPath);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var status = await response.Content.ReadFromJsonAsync<SetupStatusResponse>();
        Assert.NotNull(status);
        Assert.Equal(SetupTokenSources.Configured, status.TokenSource);
        Assert.Equal(SetupDefaults.MinimumRootPasswordLength, status.MinimumPasswordLength);
    }

    #endregion

    #region Token

    [Fact]
    public async Task PostToken_WhenTheTokenIsWrong_ReturnsForbidden()
    {
        using var factory = CreateConsoleHintedFactory();
        using var client = CreateClient(factory);

        using var response = await PostTokenAsync(
            client,
            "a-wrong-setup-token-0123456789012345",
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostToken_WhenTheTokenIsRight_ReturnsNoConsoleHint()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await PostTokenAsync(client, SetupToken, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckSetupTokenResponse>();
        Assert.NotNull(body);
        Assert.Null(body.ManagementConsole);
    }

    [Fact]
    public async Task PostToken_WhenTheWizardNamesAConsole_ReturnsTheHint()
    {
        using var factory = CreateConsoleHintedFactory();
        using var client = CreateClient(factory);

        using var response = await PostTokenAsync(client, SetupToken, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CheckSetupTokenResponse>();
        Assert.NotNull(body);
        var hint = Assert.IsType<SetupManagementConsoleHint>(body.ManagementConsole);
        Assert.Equal(ConsoleBaseUrl, hint.BaseUrl);
        Assert.Equal(SetupManagementConsoleOptions.DefaultClientId, hint.ClientId);
    }

    #endregion

    #region Page

    [Fact]
    public async Task GetPage_WhenTheWizardNamesAConsole_AllowsConnectingToItsOrigin()
    {
        var bundleRoot = Directory.CreateTempSubdirectory("setup-bundle-");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(bundleRoot.FullName, "index.html"),
                IndexHtml,
                SecureEncoding.UTF8
            );
            using var factory = CreateFactory(
                new Dictionary<string, string?>
                {
                    [$"{SetupOptions.SectionName}:Wizard:ManagementConsole:BaseUrl"] =
                        ConsoleBaseUrl,
                    [$"{SetupOptions.SectionName}:BundleRootPath"] = bundleRoot.FullName,
                }
            );
            using var client = CreateClient(factory);

            using var response = await client.GetAsync(PagePath);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
            var connectSrc = Assert.Single(
                policy.Split(';', StringSplitOptions.TrimEntries),
                directive => directive.StartsWith("connect-src ", StringComparison.Ordinal)
            );
            Assert.Contains(ConsoleBaseUrl, connectSrc.Split(' '));
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    #endregion

    #region Root

    [Fact]
    public async Task PostRoot_WhenTheTokenIsWrong_ReturnsForbiddenAndLeavesTheInstanceUninitialized()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await PostRootAsync(
            client,
            "a-wrong-setup-token-0123456789012345",
            CancellationToken.None
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        using var status = await client.GetAsync(StatusPath);
        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
    }

    [Fact]
    public async Task PostRoot_WhenTheTokenIsRight_ReturnsCreatedAndAssignsGlobalAdminAtThePlatform()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await PostRootAsync(client, SetupToken, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var root = await response.Content.ReadFromJsonAsync<CreateRootResponse>();
        Assert.NotNull(root);
        Assert.Equal(RootUserName, root.UserName);

        using var scope = factory.Services.CreateScope();
        await using var storeManager = await scope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        var assignments = await storeManager
            .GetStore<IRoleAssignmentStore>()
            .GetByPrincipalAsync(root.PrincipalId, CancellationToken.None);

        var assignment = Assert.Single(assignments);
        Assert.Equal(root.TenantId, assignment.TenantId);
        Assert.Equal(BuiltInRoles.GlobalAdmin, assignment.RoleName);
        Assert.Equal(OpenIdResourceTypes.Platform, assignment.ResourceType);
        Assert.Equal(OpenIdConstants.PlatformResourceId, assignment.ResourceId);
    }

    [Fact]
    public async Task PostRoot_WhenTheTokenIsRight_ReturnsTheInstanceAddresses()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var response = await PostRootAsync(client, SetupToken, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var root = await response.Content.ReadFromJsonAsync<CreateRootResponse>();
        Assert.NotNull(root);

        var issuerOrigin = new Uri(HostApplicationFactory.TestServerIssuer).GetLeftPart(
            UriPartial.Authority
        );
        Assert.Equal(HostApplicationFactory.TestServerIssuer, root.Issuer);
        Assert.EndsWith(
            "/.well-known/openid-configuration",
            root.DiscoveryUrl,
            StringComparison.Ordinal
        );
        Assert.StartsWith(root.Issuer.TrimEnd('/'), root.DiscoveryUrl, StringComparison.Ordinal);
        Assert.Equal($"{issuerOrigin}/api", root.ManagementApiUrl);
    }

    [Fact]
    public async Task PostRoot_WhenTheInstanceIsInitialized_AnswersNotFoundOnEverySetupRoute()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);

        using var created = await PostRootAsync(client, SetupToken, CancellationToken.None);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var status = await client.GetAsync(StatusPath);
        using var root = await PostRootAsync(client, SetupToken, CancellationToken.None);
        using var page = await client.GetAsync(PagePath);

        Assert.Equal(HttpStatusCode.NotFound, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, page.StatusCode);
    }

    #endregion
}
