// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
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
        Assert.Equal("<html><HEAD lang=\"en\">", shell.Prelude.ToString());
        Assert.Equal("<title>t</title></HEAD><body><div id=\"root\">", shell.Head.ToString());
        Assert.Equal("</div><script></script></body></html>", shell.Tail.ToString());
    }

    [Fact]
    public void TryCreate_WhenAHeaderPrecedesTheHead_SplitsAfterTheHeadStartTag()
    {
        var shell = InteractiveBundleShell.TryCreate(
            "<!-- <header> --><head></head><body><div id=\"root\"></div></body>"
        );

        Assert.NotNull(shell);
        Assert.Equal("<!-- <header> --><head>", shell.Prelude.ToString());
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
        var shell = InteractiveBundleShell.TryCreate(
            File.ReadAllText(
                BuiltAuthBundleFactAttribute.IndexPath ?? string.Empty,
                SecureEncoding.UTF8
            )
        );
        Assert.NotNull(shell);

        var writer = new ArrayBufferWriter<char>();
        InteractiveAuthenticationPageRenderer.RenderLoginPage(
            writer,
            new InteractivePageFrame(shell, "/acme/account/", null, "n0nce", TestLocales.Xx),
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

    [Fact]
    public void LanguagePlaceholder_Always_IsTheLangAttributeOfTheBundlesIndex()
    {
        // The bundle's source index.html and the host agree on the placeholder, so a page the host serves never
        // keeps the raw placeholder as its language; the bundle's own tests hold its side of the contract.
        var indexHtml = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "auth-index.html"),
            SecureEncoding.UTF8
        );

        Assert.Contains(
            $"<html lang=\"{InteractiveBundleShell.LanguagePlaceholder}\">",
            indexHtml,
            StringComparison.Ordinal
        );
    }

    #endregion
}
