// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NCode.Buffers;
using HostEnvironments = Microsoft.Extensions.Hosting.Environments;

namespace NCode.Identity.OpenId.Setup;

public sealed partial class SetupPageHandlerTests : IDisposable
{
    private const string IndexHtml =
        "<!doctype html><html><head><meta property=\"csp-nonce\" content=\"__NCODE_CSP_NONCE__\"></head><body></body></html>";

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NonceRegex();

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IInstanceSetupService> MockInstanceSetupService { get; }
    private Mock<IHostEnvironment> MockHostEnvironment { get; }
    private DirectoryInfo BundleRoot { get; } = Directory.CreateTempSubdirectory("setup-");
    private List<SetupPageHandler> Handlers { get; } = [];

    public SetupPageHandlerTests()
    {
        MockInstanceSetupService = MockRepository.Create<IInstanceSetupService>();
        MockHostEnvironment = MockRepository.Create<IHostEnvironment>();
    }

    public void Dispose()
    {
        foreach (var handler in Handlers)
        {
            handler.Dispose();
        }

        BundleRoot.Delete(recursive: true);
        MockRepository.Verify();
    }

    #region Helpers

    private SetupPageHandler CreateHandler(string environmentName)
    {
        MockHostEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();

        var options = Microsoft.Extensions.Options.Options.Create(
            new SetupOptions { BundleRootPath = BundleRoot.FullName }
        );
        var handler = new SetupPageHandler(
            options,
            MockInstanceSetupService.Object,
            MockHostEnvironment.Object
        );
        Handlers.Add(handler);
        return handler;
    }

    private void SetupIsInitialized(bool isInitialized) =>
        MockInstanceSetupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isInitialized)
            .Verifiable();

    private async ValueTask WriteIndexAsync(CancellationToken cancellationToken) =>
        await File.WriteAllTextAsync(
            Path.Combine(BundleRoot.FullName, "index.html"),
            IndexHtml,
            SecureEncoding.UTF8,
            cancellationToken
        );

    private async ValueTask WriteAssetAsync(string name, CancellationToken cancellationToken)
    {
        var assetsPath = Directory.CreateDirectory(Path.Combine(BundleRoot.FullName, "assets"));
        await File.WriteAllTextAsync(
            Path.Combine(assetsPath.FullName, name),
            "console.log(1);",
            SecureEncoding.UTF8,
            cancellationToken
        );
    }

    private static DefaultHttpContext CreateHttpContext(
        bool isHttps = true,
        string? assetPath = null
    )
    {
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Scheme = isHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        httpContext.Response.Body = new MemoryStream();
        if (assetPath is not null)
        {
            httpContext.Request.RouteValues[SetupPageHandler.AssetRouteParameterName] = assetPath;
        }

        return httpContext;
    }

    private static async ValueTask<string> ReadBodyAsync(
        DefaultHttpContext httpContext,
        IResult result,
        CancellationToken cancellationToken
    )
    {
        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        using var streamReader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return await streamReader.ReadToEndAsync(cancellationToken);
    }

    #endregion

    #region HandlePageAsync Tests

    [Fact]
    public async Task HandlePageAsync_WhenTheBundleExists_WritesThePageWithTheNonceOfThePolicy()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext();

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);
        var body = await ReadBodyAsync(httpContext, result, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.StartsWith("text/html", httpContext.Response.ContentType, StringComparison.Ordinal);

        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        var match = NonceRegex().Match(policy);
        Assert.True(match.Success);
        Assert.Contains($"content=\"{match.Groups[1].Value}\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(SetupPage.NoncePlaceholder, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheBundleExists_WritesTheNonceAndSameOriginBaseUriPolicy()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext();

        await handler.HandlePageAsync(httpContext, CancellationToken.None);

        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        var nonce = NonceRegex().Match(policy).Groups[1].Value;
        Assert.Equal(
            $"default-src 'none'; script-src 'nonce-{nonce}' 'strict-dynamic'; style-src 'nonce-{nonce}'; img-src 'self'; connect-src 'self'; base-uri 'self'; form-action 'none'; frame-ancestors 'none'",
            policy
        );
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheBundleExists_DoesNotCacheThePage()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext();

        await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.Equal(
            "no-store, no-cache, max-age=0",
            httpContext.Response.Headers.CacheControl.ToString()
        );
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheRequestHasAPathBase_WritesABaseUnderIt()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext();
        httpContext.Request.PathBase = "/op";

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);
        var body = await ReadBodyAsync(httpContext, result, CancellationToken.None);

        Assert.Contains("<base href=\"/op/setup/\" />", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlePageAsync_Always_WritesAFreshNoncePerResponse()
    {
        await WriteIndexAsync(CancellationToken.None);
        MockInstanceSetupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .Verifiable(Times.Exactly(2));
        var handler = CreateHandler(HostEnvironments.Development);
        var first = CreateHttpContext();
        var second = CreateHttpContext();

        await handler.HandlePageAsync(first, CancellationToken.None);
        await handler.HandlePageAsync(second, CancellationToken.None);

        var firstNonce = NonceRegex()
            .Match(first.Response.Headers.ContentSecurityPolicy.ToString())
            .Groups[1]
            .Value;
        var secondNonce = NonceRegex()
            .Match(second.Response.Headers.ContentSecurityPolicy.ToString())
            .Groups[1]
            .Value;
        Assert.NotEmpty(firstNonce);
        Assert.NotEqual(firstNonce, secondNonce);
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheInstanceIsInitialized_Answers404()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsInitialized(true);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext();

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
        Assert.False(httpContext.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task HandlePageAsync_WhenProductionAndTheRequestIsNotHttps_Answers404WithoutReadingTheInstance()
    {
        await WriteIndexAsync(CancellationToken.None);
        var handler = CreateHandler(HostEnvironments.Production);
        var httpContext = CreateHttpContext(isHttps: false);

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandlePageAsync_WhenProductionAndTheRequestIsHttps_WritesThePage()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Production);
        var httpContext = CreateHttpContext(isHttps: true);

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.IsType<ContentHttpResult>(result);
    }

    [Fact]
    public async Task HandlePageAsync_WhenDevelopmentAndTheRequestIsNotHttps_WritesThePage()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext(isHttps: false);

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.IsType<ContentHttpResult>(result);
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheBundleIsMissing_Answers404()
    {
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext();

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region HandleAssetAsync Tests

    [Fact]
    public async Task HandleAssetAsync_WhenTheFileExists_ServesItWithAnImmutableCacheLifetime()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext(assetPath: "index-ab12.js");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        var file = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.StartsWith("text/javascript", file.ContentType, StringComparison.Ordinal);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            httpContext.Response.Headers.CacheControl.ToString()
        );
    }

    [Fact]
    public async Task HandleAssetAsync_WhenNoFileExists_Answers404()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext(assetPath: "missing.js");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenTheRouteHasNoPath_Answers404()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext();

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenThePathLeavesTheAssetsDirectory_Answers404()
    {
        await WriteIndexAsync(CancellationToken.None);
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext(assetPath: "../index.html");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenTheInstanceIsInitialized_Answers404EvenForAnExistingFile()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsInitialized(true);
        var handler = CreateHandler(HostEnvironments.Development);
        var httpContext = CreateHttpContext(assetPath: "index-ab12.js");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenProductionAndTheRequestIsNotHttps_Answers404EvenForAnExistingFile()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        var handler = CreateHandler(HostEnvironments.Production);
        var httpContext = CreateHttpContext(isHttps: false, assetPath: "index-ab12.js");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenProductionAndTheRequestIsHttps_ServesTheFile()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsInitialized(false);
        var handler = CreateHandler(HostEnvironments.Production);
        var httpContext = CreateHttpContext(isHttps: true, assetPath: "index-ab12.js");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<PhysicalFileHttpResult>(result);
    }

    #endregion
}
