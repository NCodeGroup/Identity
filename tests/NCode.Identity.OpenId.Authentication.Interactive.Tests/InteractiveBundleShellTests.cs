// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.AspNetCore;
using NCode.Buffers;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public class InteractiveBundleShellTests
{
    #region TryCreate Tests

    [Fact]
    public void TryCreate_WhenTheDocumentHasAMountPoint_SplitsItAfterTheHeadStartTagAndInsideTheMountPoint()
    {
        var shell = InteractiveBundleShell.TryCreate(
            "<html><HEAD lang=\"en\"><title>t</title></HEAD><body><div id=\"root\"></div><script></script></body></html>"
        );

        Assert.NotNull(shell);
        var prelude = shell.Prelude.ToString();
        var head = shell.Head.ToString();
        var tail = shell.Tail.ToString();

        Assert.Equal("<html><HEAD lang=\"en\">", prelude);
        Assert.Equal("<title>t</title></HEAD><body><div id=\"root\">", head);
        Assert.Equal("</div><script></script></body></html>", tail);
    }

    [Fact]
    public void TryCreate_WhenAHeaderPrecedesTheHead_SplitsAfterTheHeadStartTag()
    {
        var shell = InteractiveBundleShell.TryCreate(
            "<!-- <header> --><head></head><body><div id=\"root\"></div></body>"
        );

        Assert.NotNull(shell);
        var prelude = shell.Prelude.ToString();

        Assert.Equal("<!-- <header> --><head>", prelude);
    }

    [Fact]
    public void TryCreate_WhenTheHeadHasIconLinks_BuildsAHeadWithoutThem()
    {
        var shell = InteractiveBundleShell.TryCreate(
            "<html><head><link rel=\"icon\" href=\"a.ico\"><link rel=\"apple-touch-icon\" href=\"a.png\"><link rel=\"stylesheet\" href=\"a.css\"><title>t</title></head><body><div id=\"root\"></div></body></html>"
        );

        Assert.NotNull(shell);
        var head = shell.Head.ToString();
        var headWithoutIcons = shell.HeadWithoutIcons.ToString();

        Assert.Equal(
            "<link rel=\"icon\" href=\"a.ico\"><link rel=\"apple-touch-icon\" href=\"a.png\"><link rel=\"stylesheet\" href=\"a.css\"><title>t</title></head><body><div id=\"root\">",
            head
        );
        Assert.Equal(
            "<link rel=\"stylesheet\" href=\"a.css\"><title>t</title></head><body><div id=\"root\">",
            headWithoutIcons
        );
    }

    [Fact]
    public void TryCreate_WhenTheHeadHasNoIconLinks_BuildsAnIdenticalHeadWithoutIcons()
    {
        var shell = InteractiveBundleShell.TryCreate(
            "<html><head><link rel=\"stylesheet\" href=\"a.css\"><title>t</title></head><body><div id=\"root\"></div></body></html>"
        );

        Assert.NotNull(shell);
        var head = shell.Head.ToString();
        var headWithoutIcons = shell.HeadWithoutIcons.ToString();

        Assert.Equal(head, headWithoutIcons);
    }

    [Theory]
    [InlineData("<body><div id=\"root\"></div></body>")]
    [InlineData("<header></header><body><div id=\"root\"></div></body>")]
    [InlineData("<head")]
    public void TryCreate_WhenNoHeadStartTagPrecedesTheMountPoint_ReturnsNull(string html)
    {
        var shell = InteractiveBundleShell.TryCreate(html + "<div id=\"root\"></div>");

        Assert.Null(shell);
    }

    [Fact]
    public void TryCreate_WhenTheMountPointIsNotEmpty_ReturnsNull()
    {
        var shell = InteractiveBundleShell.TryCreate("<body><div id=\"root\">x</div></body>");

        Assert.Null(shell);
    }

    [Fact]
    public void TryCreate_WhenTheDocumentHasNoMountPoint_ReturnsNull()
    {
        var shell = InteractiveBundleShell.TryCreate("<body></body>");

        Assert.Null(shell);
    }

    [BuiltAuthBundleFact]
    public void TryCreate_WhenGivenTheBuiltBundlesIndex_SplitsItSoTheLoginPageLoadsTheBundleBeneathTheSurface()
    {
        var indexHtml = File.ReadAllText(
            BuiltAuthBundleFactAttribute.IndexPath ?? string.Empty,
            SecureEncoding.UTF8
        );
        var shell = InteractiveBundleShell.TryCreate(indexHtml);
        Assert.NotNull(shell);

        var writer = new ArrayBufferWriter<char>();
        var frame = new InteractivePageFrame(
            shell,
            "/acme/account/",
            null,
            "n0nce",
            TestLocales.Xx
        );
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            frame,
            returnUrl: "/",
            antiforgeryFieldName: "__RequestVerificationToken",
            antiforgeryToken: "t",
            errorMessage: null
        );
        var html = writer.WrittenSpan.ToString();

        // The base precedes the bundle's script, so the script's relative URL resolves beneath the tenant's surface.
        var baseIndex = html.IndexOf("<base href=\"/acme/account/\" />", StringComparison.Ordinal);
        var scriptIndex = html.IndexOf("<script type=\"module\"", StringComparison.Ordinal);
        Assert.True(baseIndex >= 0);
        Assert.True(scriptIndex >= 0);
        Assert.True(baseIndex < scriptIndex);
        Assert.Contains("src=\"./assets/", html, StringComparison.Ordinal);
        Assert.Contains(
            "<div id=\"root\"><main class=\"auth-card\">",
            html,
            StringComparison.Ordinal
        );

        // The build writes the placeholder that Vite's html.cspNonce names and the host replaces it with the nonce.
        Assert.Contains(
            "<script type=\"module\" crossorigin src=\"./assets/",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "<meta property=\"csp-nonce\" nonce=\"n0nce\">",
            html,
            StringComparison.Ordinal
        );
        Assert.Matches(
            "<script type=\"module\" crossorigin src=\"\\./assets/[^\"]+\" nonce=\"n0nce\"></script>",
            html
        );
        Assert.DoesNotContain(
            InteractiveBundleShell.NoncePlaceholder,
            html,
            StringComparison.Ordinal
        );

        // The build keeps the language placeholder in the html element's lang attribute, which the host replaces
        // with the selected locale's tag, so the bundle reads the page's language from the document.
        Assert.Contains("<html lang=\"xx\">", html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            InteractiveBundleShell.LanguagePlaceholder,
            html,
            StringComparison.Ordinal
        );
    }

    [BuiltAuthBundleTheory]
    [InlineData(
        "<link rel=\"icon\" href=\"\\./assets/(?<asset>favicon-[^\"/]+\\.ico)\" sizes=\"32x32\"",
        "image/x-icon"
    )]
    [InlineData(
        "<link rel=\"icon\" href=\"\\./assets/(?<asset>favicon-[^\"/]+\\.svg)\" type=\"image/svg\\+xml\"",
        "image/svg+xml"
    )]
    [InlineData(
        "<link rel=\"apple-touch-icon\" href=\"\\./assets/(?<asset>apple-touch-icon-[^\"/]+\\.png)\"",
        "image/png"
    )]
    public void TryCreate_WhenGivenTheBuiltBundlesIndex_LinksEachIconAsAnAssetServedWithItsImageType(
        string linkPattern,
        string contentType
    )
    {
        var indexPath = BuiltAuthBundleFactAttribute.IndexPath ?? string.Empty;
        var html = File.ReadAllText(indexPath, SecureEncoding.UTF8);
        var bundleRootPath = Path.GetDirectoryName(indexPath);
        using var bundle = new SinglePageBundle<InteractiveBundleShell>(
            bundleRootPath,
            InteractiveBundleShell.TryCreate
        );
        var httpContext = new DefaultHttpContext();

        var link = Regex.Match(html, linkPattern);
        var assetPath = link.Groups["asset"].Value;
        var result = bundle.ServeAsset(httpContext.Response, assetPath);

        Assert.True(link.Success, linkPattern);
        var file = Assert.IsType<PhysicalFileHttpResult>(result);
        Assert.Equal(contentType, file.ContentType);
    }

    [Fact]
    public void LanguagePlaceholder_Always_IsTheLangAttributeOfTheBundlesIndex()
    {
        // The bundle's source index.html and the host agree on the placeholder, so a page the host serves never
        // keeps the raw placeholder as its language; the bundle's own tests hold its side of the contract.
        var indexPath = Path.Combine(AppContext.BaseDirectory, "auth-index.html");
        var indexHtml = File.ReadAllText(indexPath, SecureEncoding.UTF8);

        Assert.Contains(
            $"<html lang=\"{InteractiveBundleShell.LanguagePlaceholder}\">",
            indexHtml,
            StringComparison.Ordinal
        );
    }

    #endregion
}
