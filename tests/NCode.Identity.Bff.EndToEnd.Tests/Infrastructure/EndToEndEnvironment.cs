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

    private readonly ConcurrentDictionary<string, Task<IBrowser>> _browsers = new();

    private TestCertificate? _certificate;
    private KeycloakProvider? _keycloak;
    private BffApplication? _strictBff;
    private BffApplication? _laxBff;
    private BlazorBffApplication? _blazorBff;
    private OriginServer? _originServer;
    private IPlaywright? _playwright;
    private string? _crossSiteDenied;
    private string? _crossSiteAdmitted;
    private string? _sameSiteUntrusted;
    private string? _sameSiteTrusted;

    /// <summary>
    /// Gets Keycloak, the OpenID Provider.
    /// </summary>
    public KeycloakProvider Keycloak => _keycloak ?? throw NotInitialized();

    /// <summary>
    /// Gets the BFF whose session cookie is <c>SameSite=Strict</c>.
    /// </summary>
    public BffApplication StrictBff => _strictBff ?? throw NotInitialized();

    /// <summary>
    /// Gets the BFF whose session cookie is <c>SameSite=Lax</c>.
    /// </summary>
    public BffApplication LaxBff => _laxBff ?? throw NotInitialized();

    /// <summary>
    /// Gets the BFF that hosts the Blazor Web App, whose session cookie is <c>SameSite=Strict</c>.
    /// </summary>
    public BlazorBffApplication BlazorBff => _blazorBff ?? throw NotInitialized();

    /// <summary>
    /// Gets the Playwright instance.
    /// </summary>
    public IPlaywright Playwright => _playwright ?? throw NotInitialized();

    /// <summary>
    /// Gets a cross-site origin that the CORS policy does not admit.
    /// </summary>
    public string CrossSiteDenied => _crossSiteDenied ?? throw NotInitialized();

    /// <summary>
    /// Gets a cross-site origin that the CORS policy admits.
    /// </summary>
    public string CrossSiteAdmitted => _crossSiteAdmitted ?? throw NotInitialized();

    /// <summary>
    /// Gets a same-site origin that the CORS policy admits and that is not trusted.
    /// </summary>
    public string SameSiteUntrusted => _sameSiteUntrusted ?? throw NotInitialized();

    /// <summary>
    /// Gets a same-site origin that the CORS policy admits and that is trusted.
    /// </summary>
    public string SameSiteTrusted => _sameSiteTrusted ?? throw NotInitialized();

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

        _crossSiteDenied = $"https://127.0.0.1:{crossSiteDeniedPort}";
        _crossSiteAdmitted = $"https://127.0.0.1:{crossSiteAdmittedPort}";
        _sameSiteUntrusted = $"https://localhost:{sameSiteUntrustedPort}";
        _sameSiteTrusted = $"https://localhost:{sameSiteTrustedPort}";

        _certificate = TestCertificate.Create();

        await TestcontainersSettings.ExposeHostPortsAsync([
            (ushort)strictPort,
            (ushort)laxPort,
            (ushort)blazorPort,
        ]);

        _keycloak = await KeycloakProvider.StartAsync(
            _certificate,
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

        _strictBff = await BffApplication.StartAsync(
            CreateSettings(StrictClientId, strictPort, SameSiteMode.Strict)
        );
        _laxBff = await BffApplication.StartAsync(
            CreateSettings(LaxClientId, laxPort, SameSiteMode.Lax)
        );
        _blazorBff = await BlazorBffApplication.StartAsync(
            new BffApplicationSettings(
                BlazorClientId,
                blazorPort,
                SameSiteMode.Strict,
                Keycloak.Issuer,
                _certificate,
                [],
                [],
                TimeSpan.FromSeconds(2)
            )
        );

        _originServer = await OriginServer.StartAsync(
            _certificate,
            [crossSiteDeniedPort, crossSiteAdmittedPort, sameSiteUntrustedPort, sameSiteTrustedPort]
        );

        _playwright = await Microsoft.Playwright.Playwright.CreateAsync();
    }

    /// <summary>
    /// Creates a browser context that ignores certificate errors, in a browser that is launched once per run.
    /// </summary>
    public async Task<IBrowserContext> NewContextAsync(string browserName)
    {
        var browser = await _browsers.GetOrAdd(browserName, LaunchBrowserAsync);
        return await browser.NewContextAsync(
            new BrowserNewContextOptions { IgnoreHTTPSErrors = true }
        );
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        foreach (var browser in _browsers.Values)
        {
            await (await browser).DisposeAsync();
        }

        _browsers.Clear();

        _playwright?.Dispose();

        if (_originServer is not null)
            await _originServer.DisposeAsync();

        if (_blazorBff is not null)
            await _blazorBff.DisposeAsync();

        if (_laxBff is not null)
            await _laxBff.DisposeAsync();

        if (_strictBff is not null)
            await _strictBff.DisposeAsync();

        if (_keycloak is not null)
            await _keycloak.DisposeAsync();

        _certificate?.Dispose();
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
            _certificate ?? throw NotInitialized(),
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
