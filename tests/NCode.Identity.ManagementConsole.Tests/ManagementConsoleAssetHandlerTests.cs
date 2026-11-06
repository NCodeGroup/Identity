// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsoleAssetHandlerTests : BaseTests
{
    private DirectoryInfo ContentRoot { get; } = Directory.CreateTempSubdirectory("console-");

    private string AssetsPath => Path.Combine(ContentRoot.FullName, "dist", "assets");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ContentRoot.Delete(recursive: true);
        }

        base.Dispose(disposing);
    }

    #region Helpers

    private ManagementConsoleAssetHandler CreateHandler(out ManagementConsoleBundleReader reader)
    {
        var mockEnvironment = CreateStrictMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.ContentRootPath).Returns(ContentRoot.FullName);

        reader = new ManagementConsoleBundleReader(
            Options.Create(new ManagementConsoleOptions { BundleRootPath = "dist" }),
            mockEnvironment.Object
        );
        return new ManagementConsoleAssetHandler(reader);
    }

    private void WriteAsset(string name, string contents)
    {
        Directory.CreateDirectory(AssetsPath);
        File.WriteAllText(Path.Combine(AssetsPath, name), contents, SecureEncoding.UTF8);
    }

    private static DefaultHttpContext CreateHttpContext(string asset)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues[ManagementConsoleAssetHandler.AssetRouteParameterName] =
            asset;
        return httpContext;
    }

    #endregion

    #region Handle Tests

    [Fact]
    public void Handle_WhenTheAssetExists_ServesItWithAnImmutableCacheLifetime()
    {
        WriteAsset("index-abc123.js", "console.log('hi');");
        var handler = CreateHandler(out var reader);
        using var _ = reader;
        var httpContext = CreateHttpContext("index-abc123.js");

        var result = handler.Handle(httpContext);

        var file = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.EndsWith("index-abc123.js", file.FileName, StringComparison.Ordinal);
        Assert.NotNull(file.EntityTag);
        Assert.True(file.EnableRangeProcessing);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            httpContext.Response.Headers.CacheControl.ToString()
        );
    }

    [Fact]
    public void Handle_WhenTheAssetIsMissing_AnswersNotFoundWithoutACacheLifetime()
    {
        WriteAsset("index-abc123.js", "console.log('hi');");
        var handler = CreateHandler(out var reader);
        using var _ = reader;
        var httpContext = CreateHttpContext("missing.js");

        var result = handler.Handle(httpContext);

        Assert.IsType<NotFound>(result);
        Assert.False(httpContext.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public void Handle_WhenTheAssetHasAnUnknownType_AnswersNotFound()
    {
        WriteAsset("data.unknownext123", "x");
        var handler = CreateHandler(out var reader);
        using var _ = reader;
        var httpContext = CreateHttpContext("data.unknownext123");

        var result = handler.Handle(httpContext);

        Assert.IsType<NotFound>(result);
        Assert.False(httpContext.Response.Headers.ContainsKey("Cache-Control"));
    }

    [Fact]
    public void Handle_WithoutAnAssetRouteValue_AnswersNotFound()
    {
        var handler = CreateHandler(out var reader);
        using var _ = reader;

        var result = handler.Handle(new DefaultHttpContext());

        Assert.IsType<NotFound>(result);
    }

    #endregion
}
