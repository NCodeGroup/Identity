// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using DotNet.Testcontainers.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// The environment that the end-to-end tests share: Keycloak in a container, three backend-for-frontend (BFF)
/// applications, the origin pages and Playwright.
/// </summary>
public sealed class EndToEndEnvironment : IAsyncLifetime
{
    private const string StrictClientId = "bff-strict";
    private const string LaxClientId = "bff-lax";
    private const string BlazorClientId = "bff-blazor";
    private const string ContainerHost = "host.testcontainers.internal";

    private ConcurrentDictionary<string, Task<IBrowser>> Browsers { get; } = new();

    private TestCertificate? Certificate { get; set; }
    private KeycloakProvider? KeycloakOrNull { get; set; }
    private BffApplication? StrictBffOrNull { get; set; }
    private BffApplication? LaxBffOrNull { get; set; }
    private BlazorBffApplication? BlazorBffOrNull { get; set; }
    private OriginServer? OriginServer { get; set; }
    private IPlaywright? PlaywrightOrNull { get; set; }
    private string? CrossSiteDeniedOrNull { get; set; }
    private string? CrossSiteAdmittedOrNull { get; set; }
    private string? SameSiteUntrustedOrNull { get; set; }
    private string? SameSiteTrustedOrNull { get; set; }

    /// <summary>
    /// Gets Keycloak, the OpenID Provider.
    /// </summary>
    public KeycloakProvider Keycloak => KeycloakOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets the BFF whose session cookie is <c>SameSite=Strict</c>.
    /// </summary>
    public BffApplication StrictBff => StrictBffOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets the BFF whose session cookie is <c>SameSite=Lax</c>.
    /// </summary>
    public BffApplication LaxBff => LaxBffOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets the BFF that hosts the Blazor Web App, whose session cookie is <c>SameSite=Strict</c>.
    /// </summary>
    public BlazorBffApplication BlazorBff => BlazorBffOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets the Playwright instance.
    /// </summary>
    public IPlaywright Playwright => PlaywrightOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets a cross-site origin that the CORS policy does not admit.
    /// </summary>
    public string CrossSiteDenied => CrossSiteDeniedOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets a cross-site origin that the CORS policy admits.
    /// </summary>
    public string CrossSiteAdmitted => CrossSiteAdmittedOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets a same-site origin that the CORS policy admits and that is not trusted.
    /// </summary>
    public string SameSiteUntrusted => SameSiteUntrustedOrNull ?? throw NotInitialized();

    /// <summary>
    /// Gets a same-site origin that the CORS policy admits and that is trusted.
    /// </summary>
    public string SameSiteTrusted => SameSiteTrustedOrNull ?? throw NotInitialized();

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var strictPort = LoopbackPort.Reserve();
        var laxPort = LoopbackPort.Reserve();
        var blazorPort = LoopbackPort.Reserve();
        var crossSiteDeniedPort = LoopbackPort.Reserve();
        var crossSiteAdmittedPort = LoopbackPort.Reserve();
        var sameSiteUntrustedPort = LoopbackPort.Reserve();
        var sameSiteTrustedPort = LoopbackPort.Reserve();

        CrossSiteDeniedOrNull = $"https://127.0.0.1:{crossSiteDeniedPort}";
        CrossSiteAdmittedOrNull = $"https://127.0.0.1:{crossSiteAdmittedPort}";
        SameSiteUntrustedOrNull = $"https://localhost:{sameSiteUntrustedPort}";
        SameSiteTrustedOrNull = $"https://localhost:{sameSiteTrustedPort}";

        Certificate = TestCertificate.Create();

        await TestcontainersSettings.ExposeHostPortsAsync([
            (ushort)strictPort,
            (ushort)laxPort,
            (ushort)blazorPort,
        ]);

        KeycloakOrNull = await KeycloakProvider.StartAsync(
            Certificate,
            new Dictionary<string, string>
            {
                ["BFF_STRICT_ORIGIN"] = $"https://localhost:{strictPort}",
                ["BFF_STRICT_CONTAINER_ORIGIN"] = $"https://{ContainerHost}:{strictPort}",
                ["BFF_LAX_ORIGIN"] = $"https://localhost:{laxPort}",
                ["BFF_LAX_CONTAINER_ORIGIN"] = $"https://{ContainerHost}:{laxPort}",
                ["BFF_BLAZOR_ORIGIN"] = $"https://localhost:{blazorPort}",
                ["BFF_BLAZOR_CONTAINER_ORIGIN"] = $"https://{ContainerHost}:{blazorPort}",
            }
        );

        StrictBffOrNull = await BffApplication.StartAsync(
            CreateSettings(StrictClientId, strictPort, SameSiteMode.Strict)
        );
        LaxBffOrNull = await BffApplication.StartAsync(
            CreateSettings(LaxClientId, laxPort, SameSiteMode.Lax)
        );
        BlazorBffOrNull = await BlazorBffApplication.StartAsync(
            new BffApplicationSettings(
                BlazorClientId,
                blazorPort,
                SameSiteMode.Strict,
                Keycloak.Issuer,
                Certificate,
                [],
                [],
                TimeSpan.FromSeconds(2)
            )
        );

        OriginServer = await OriginServer.StartAsync(
            Certificate,
            [crossSiteDeniedPort, crossSiteAdmittedPort, sameSiteUntrustedPort, sameSiteTrustedPort]
        );

        PlaywrightOrNull = await Microsoft.Playwright.Playwright.CreateAsync();
    }

    /// <summary>
    /// Creates a browser context that ignores certificate errors, in a browser that is launched once per run.
    /// </summary>
    public async Task<IBrowserContext> NewContextAsync(string browserName)
    {
        var browser = await Browsers.GetOrAdd(browserName, LaunchBrowserAsync);
        return await browser.NewContextAsync(
            new BrowserNewContextOptions { IgnoreHTTPSErrors = true }
        );
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        foreach (var browser in Browsers.Values)
        {
            await (await browser).DisposeAsync();
        }

        Browsers.Clear();

        PlaywrightOrNull?.Dispose();

        if (OriginServer is not null)
            await OriginServer.DisposeAsync();

        if (BlazorBffOrNull is not null)
            await BlazorBffOrNull.DisposeAsync();

        if (LaxBffOrNull is not null)
            await LaxBffOrNull.DisposeAsync();

        if (StrictBffOrNull is not null)
            await StrictBffOrNull.DisposeAsync();

        if (KeycloakOrNull is not null)
            await KeycloakOrNull.DisposeAsync();

        Certificate?.Dispose();
    }

    private BffApplicationSettings CreateSettings(
        string clientId,
        int port,
        SameSiteMode sameSite
    ) =>
        new(
            clientId,
            port,
            sameSite,
            Keycloak.Issuer,
            Certificate ?? throw NotInitialized(),
            [SameSiteUntrusted, SameSiteTrusted, CrossSiteAdmitted],
            [SameSiteTrusted],
            TimeSpan.FromMinutes(1)
        );

    private Task<IBrowser> LaunchBrowserAsync(string browserName)
    {
        var browserType = Playwright[browserName];
        return browserType.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    }

    private static InvalidOperationException NotInitialized() =>
        new("The end-to-end environment has not been initialized.");
}
