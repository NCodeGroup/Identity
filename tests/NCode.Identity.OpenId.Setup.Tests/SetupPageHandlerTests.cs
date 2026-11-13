// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Setup;

public sealed partial class SetupPageHandlerTests : IDisposable
{
    private const string IndexHtml =
        "<!doctype html><html><head><meta property=\"csp-nonce\" content=\"__NCODE_CSP_NONCE__\"></head><body></body></html>";

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NonceRegex();

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<ISetupSurfaceGate> MockSurfaceGate { get; }
    private DirectoryInfo BundleRoot { get; } = Directory.CreateTempSubdirectory("setup-");
    private List<SetupPageHandler> Handlers { get; } = [];

    public SetupPageHandlerTests()
    {
        MockSurfaceGate = MockRepository.Create<ISetupSurfaceGate>();
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

    private SetupPageHandler CreateHandler(string? managementConsoleBaseUrl = null)
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new SetupOptions
            {
                BundleRootPath = BundleRoot.FullName,
                Wizard = new SetupWizardOptions
                {
                    ManagementConsole = new SetupWizardManagementConsoleOptions
                    {
                        BaseUrl = managementConsoleBaseUrl,
                    },
                },
            }
        );
        var handler = new SetupPageHandler(options, MockSurfaceGate.Object);
        Handlers.Add(handler);
        return handler;
    }

    private void SetupIsServed(bool isServed, Times? times = null) =>
        MockSurfaceGate
            .Setup(x => x.IsServedAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(isServed)
            .Verifiable(times ?? Times.Once());

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

    private static DefaultHttpContext CreateHttpContext(string? assetPath = null)
    {
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.Scheme = Uri.UriSchemeHttps;
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
        SetupIsServed(true);
        var handler = CreateHandler();
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
        SetupIsServed(true);
        var handler = CreateHandler();
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
    public async Task HandlePageAsync_WhenAConsoleIsHinted_AllowsConnectingToItsOrigin()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsServed(true);
        var handler = CreateHandler("https://console.example.com");
        var httpContext = CreateHttpContext();

        await handler.HandlePageAsync(httpContext, CancellationToken.None);

        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains(
            "connect-src 'self' https://console.example.com;",
            policy,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task HandlePageAsync_WhenNoConsoleIsHinted_AllowsConnectingOnlyToItsOwnOrigin()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsServed(true);
        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        await handler.HandlePageAsync(httpContext, CancellationToken.None);

        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains("connect-src 'self';", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheBundleExists_DoesNotCacheThePage()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsServed(true);
        var handler = CreateHandler();
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
        SetupIsServed(true);
        var handler = CreateHandler();
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
        SetupIsServed(true, Times.Exactly(2));
        var handler = CreateHandler();
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
    public async Task HandlePageAsync_WhenTheSurfaceIsNotServed_Answers404()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsServed(false);
        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
        Assert.False(httpContext.Response.Headers.ContainsKey("Content-Security-Policy"));
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheSurfaceIsServed_WritesThePage()
    {
        await WriteIndexAsync(CancellationToken.None);
        SetupIsServed(true);
        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        var result = await handler.HandlePageAsync(httpContext, CancellationToken.None);

        Assert.IsType<ContentHttpResult>(result);
    }

    [Fact]
    public async Task HandlePageAsync_WhenTheBundleIsMissing_Answers404()
    {
        SetupIsServed(true);
        var handler = CreateHandler();
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
        SetupIsServed(true);
        var handler = CreateHandler();
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
        SetupIsServed(true);
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(assetPath: "missing.js");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenTheRouteHasNoPath_Answers404()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsServed(true);
        var handler = CreateHandler();
        var httpContext = CreateHttpContext();

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenThePathLeavesTheAssetsDirectory_Answers404()
    {
        await WriteIndexAsync(CancellationToken.None);
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsServed(true);
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(assetPath: "../index.html");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task HandleAssetAsync_WhenTheSurfaceIsNotServed_Answers404EvenForAnExistingFile()
    {
        await WriteAssetAsync("index-ab12.js", CancellationToken.None);
        SetupIsServed(false);
        var handler = CreateHandler();
        var httpContext = CreateHttpContext(assetPath: "index-ab12.js");

        var result = await handler.HandleAssetAsync(httpContext, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion
}
