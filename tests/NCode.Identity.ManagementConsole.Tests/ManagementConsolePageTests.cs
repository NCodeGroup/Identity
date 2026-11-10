// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.AspNetCore;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsolePageTests : BaseTests
{
    #region Render Tests

    [Fact]
    public void Render_WhenThePageHasPlaceholders_ReplacesEveryPlaceholder()
    {
        const string html =
            "<meta content=\"__NCODE_CSP_NONCE__\"><script nonce=\"__NCODE_CSP_NONCE__\">";

        var rendered = new ManagementConsolePage(html).Render("abc", null);

        Assert.Equal("<meta content=\"abc\"><script nonce=\"abc\">", rendered);
    }

    [Fact]
    public void Render_WhenThePageHasNoPlaceholder_ReturnsThePageUnchanged()
    {
        var rendered = new ManagementConsolePage("<html></html>").Render("abc", null);

        Assert.Equal("<html></html>", rendered);
    }

    [Fact]
    public void Render_WhenAFaviconIsGiven_LinksItInPlaceOfTheIcons()
    {
        const string html =
            "<html><head><link rel=\"icon\" href=\"./assets/favicon-ab12.ico\"><title>t</title></head><body></body></html>";

        var rendered = new ManagementConsolePage(html).Render(
            "abc",
            new Uri("https://cdn.example/tab.ico")
        );

        Assert.Contains(
            "<link rel=\"icon\" href=\"https://cdn.example/tab.ico\" /></head>",
            rendered,
            StringComparison.Ordinal
        );
        Assert.Contains("<title>t</title>", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("favicon-ab12", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenNoFaviconIsGiven_KeepsTheIcons()
    {
        const string html =
            "<html><head><link rel=\"icon\" href=\"./assets/favicon-ab12.ico\"></head><body></body></html>";

        var rendered = new ManagementConsolePage(html).Render("abc", null);

        Assert.Equal(html, rendered);
    }

    [Fact]
    public void Render_WhenAFaviconIsGivenAndThePageHasNoHead_RemovesTheIconsAndAddsNone()
    {
        const string html = "<link rel=\"icon\" href=\"./assets/favicon-ab12.ico\"><body></body>";

        var rendered = new ManagementConsolePage(html).Render(
            "abc",
            new Uri("https://cdn.example/tab.ico")
        );

        Assert.Equal("<body></body>", rendered);
    }

    [Fact]
    public void Render_WhenAFaviconIsGiven_StillWritesTheNonce()
    {
        const string html =
            "<html><head><meta content=\"__NCODE_CSP_NONCE__\"><link rel=\"icon\" href=\"./a.ico\"></head></html>";

        var rendered = new ManagementConsolePage(html).Render(
            "abc",
            new Uri("https://cdn.example/tab.ico")
        );

        Assert.DoesNotContain("__NCODE_CSP_NONCE__", rendered, StringComparison.Ordinal);
        Assert.Contains("<meta content=\"abc\">", rendered, StringComparison.Ordinal);
    }

    [BuiltConsoleBundleFact]
    public async Task Render_WhenGivenTheBuiltBundlesIndex_LeavesNoPlaceholderAndNoncesEveryScript()
    {
        var html = await File.ReadAllTextAsync(
            BuiltConsoleBundleFactAttribute.IndexPath ?? string.Empty,
            SecureEncoding.UTF8
        );

        var rendered = new ManagementConsolePage(html).Render("n0nce", null);

        Assert.DoesNotContain("__NCODE_CSP_NONCE__", rendered, StringComparison.Ordinal);
        Assert.Matches("<meta property=\"csp-nonce\"[^>]*nonce=\"n0nce\"", rendered);

        var scriptTags = Regex.Matches(rendered, "<script\\b[^>]*>");
        Assert.NotEmpty(scriptTags);
        Assert.All(
            scriptTags,
            tag => Assert.Contains("nonce=\"n0nce\"", tag.Value, StringComparison.Ordinal)
        );
    }

    [BuiltConsoleBundleTheory]
    [InlineData(
        "<link rel=\"icon\" href=\"/assets/(?<asset>favicon-[^\"/]+\\.ico)\" sizes=\"32x32\"",
        "image/x-icon"
    )]
    [InlineData(
        "<link rel=\"icon\" href=\"/assets/(?<asset>favicon-[^\"/]+\\.svg)\" type=\"image/svg\\+xml\"",
        "image/svg+xml"
    )]
    [InlineData(
        "<link rel=\"apple-touch-icon\" href=\"/assets/(?<asset>apple-touch-icon-[^\"/]+\\.png)\"",
        "image/png"
    )]
    public void Render_WhenGivenTheBuiltBundlesIndex_LinksEachIconAsAnAssetServedWithItsImageType(
        string linkPattern,
        string contentType
    )
    {
        var indexPath = BuiltConsoleBundleFactAttribute.IndexPath ?? string.Empty;
        var html = File.ReadAllText(indexPath, SecureEncoding.UTF8);
        var bundleRootPath = Path.GetDirectoryName(indexPath);
        using var bundle = new SinglePageBundle<ManagementConsolePage>(
            bundleRootPath,
            static html => new ManagementConsolePage(html)
        );
        var httpContext = new DefaultHttpContext();

        var link = Regex.Match(html, linkPattern);
        var assetPath = link.Groups["asset"].Value;
        var result = bundle.ServeAsset(httpContext.Response, assetPath);

        Assert.True(link.Success, linkPattern);
        var file = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.Equal(contentType, file.ContentType);
    }

    #endregion
}
