// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Bff;
using NCode.Identity.ManagementConsole.Host;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// Hosts the management console's reference host in-process on a <see cref="TestServer"/>. The console's bundle is a
/// small page and one asset in a temporary directory, its two key rings are in-memory P-256 keys, and, when a
/// <see cref="HostApplicationFactory"/> is given, the console reaches that provider's endpoints and its management API
/// through its test server.
/// </summary>
public sealed class ManagementConsoleApplicationFactory
    : WebApplicationFactory<ManagementConsoleHostMarker>
{
    /// <summary>
    /// The name of the console's private_key_jwt key ring.
    /// </summary>
    public const string ClientKeyRing = "mgmt-client";

    /// <summary>
    /// The name of the console's DPoP key ring.
    /// </summary>
    public const string DPoPKeyRing = "mgmt-dpop";

    /// <summary>
    /// The path of the bundle's asset, relative to the console's origin.
    /// </summary>
    public const string AssetPath = "/assets/index-test.js";

    /// <summary>
    /// The content of the bundle's asset.
    /// </summary>
    public const string AssetContent = "export const ready = true;";

    private const string IndexHtml = """
        <!doctype html>
        <html lang="en">
          <head>
            <meta charset="utf-8" />
            <meta property="csp-nonce" nonce="__NCODE_CSP_NONCE__" />
            <title>Management console</title>
            <script type="module" nonce="__NCODE_CSP_NONCE__" src="/assets/index-test.js"></script>
          </head>
          <body>
            <div id="root"></div>
          </body>
        </html>
        """;

    private static Uri BrowserBaseAddress { get; } = new("https://localhost:5005/");

    private readonly HostApplicationFactory? _provider;
    private readonly ECDsa _clientKey;
    private readonly ECDsa _dpopKey;
    private readonly bool _ownsClientKey;
    private readonly string _bundleRootPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagementConsoleApplicationFactory"/> class.
    /// </summary>
    /// <param name="provider">The OpenID provider that the console reaches in-process, or <c>null</c> for none.</param>
    /// <param name="clientKey">The console's private_key_jwt key, or <c>null</c> to create one that the factory owns.</param>
    public ManagementConsoleApplicationFactory(
        HostApplicationFactory? provider = null,
        ECDsa? clientKey = null
    )
    {
        _provider = provider;
        _ownsClientKey = clientKey is null;
        _clientKey = clientKey ?? ECDsa.Create(ECCurve.NamedCurves.nistP256);
        _dpopKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        _bundleRootPath = Path.Combine(Path.GetTempPath(), $"mgmt-console-{Guid.NewGuid():N}");
        var assetsPath = Path.Combine(_bundleRootPath, "assets");
        Directory.CreateDirectory(assetsPath);
        File.WriteAllText(
            Path.Combine(_bundleRootPath, "index.html"),
            IndexHtml,
            SecureEncoding.UTF8
        );
        File.WriteAllText(
            Path.Combine(assetsPath, "index-test.js"),
            AssetContent,
            SecureEncoding.UTF8
        );
    }

    /// <summary>
    /// Creates a client that behaves as a browser on the console's origin: it follows no redirect and keeps no cookie.
    /// </summary>
    public HttpClient CreateBrowserClient() =>
        CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = BrowserBaseAddress,
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Resolve the content root to the host project so its appsettings.json is loaded.
        builder.UseSolutionRelativeContentRoot("src/NCode.Identity.ManagementConsole.Host");
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);

        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Bff:Frontends:mgmt:Authority"] = HostApplicationFactory.TestServerIssuer,
                    ["ManagementConsole:ManagementApiUrl"] = "http://localhost/api",
                    ["ManagementConsole:BundleRootPath"] = _bundleRootPath,
                }
            )
        );

        builder.ConfigureTestServices(serviceCollection =>
        {
            AddKeyRing(serviceCollection, ClientKeyRing, _clientKey);
            AddKeyRing(serviceCollection, DPoPKeyRing, _dpopKey);

            if (_provider is not null)
            {
                // Registered after the library, which configures its own primary handler for each named client.
                serviceCollection
                    .AddHttpClient(OpenIdClientDefaults.HttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => _provider.Server.CreateHandler());
                serviceCollection
                    .AddHttpClient(BffDefaults.RemoteApiHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => _provider.Server.CreateHandler());
            }
        });
    }

    private static void AddKeyRing(IServiceCollection serviceCollection, string name, ECDsa key) =>
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            name,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        serviceProvider
                            .GetRequiredService<ISecretKeyFactory>()
                            .CreateEcc(new KeyMetadata { KeyId = name }, key),
                    ])
        );

    /// <inheritdoc />
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();

        _dpopKey.Dispose();
        if (_ownsClientKey)
        {
            _clientKey.Dispose();
        }

        if (Directory.Exists(_bundleRootPath))
        {
            Directory.Delete(_bundleRootPath, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
