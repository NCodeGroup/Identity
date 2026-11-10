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

        var rendered = new ManagementConsolePage(html).Render("abc");

        Assert.Equal("<meta content=\"abc\"><script nonce=\"abc\">", rendered);
    }

    [Fact]
    public void Render_WhenThePageHasNoPlaceholder_ReturnsThePageUnchanged()
    {
        var rendered = new ManagementConsolePage("<html></html>").Render("abc");

        Assert.Equal("<html></html>", rendered);
    }

    [BuiltConsoleBundleFact]
    public async Task Render_WhenGivenTheBuiltBundlesIndex_LeavesNoPlaceholderAndNoncesEveryScript()
    {
        var html = await File.ReadAllTextAsync(
            BuiltConsoleBundleFactAttribute.IndexPath ?? string.Empty,
            SecureEncoding.UTF8
        );

        var rendered = new ManagementConsolePage(html).Render("n0nce");

        Assert.DoesNotContain("__NCODE_CSP_NONCE__", rendered, StringComparison.Ordinal);
        Assert.Matches("<meta property=\"csp-nonce\"[^>]*nonce=\"n0nce\"", rendered);

        var scriptTags = Regex.Matches(rendered, "<script\\b[^>]*>");
        Assert.NotEmpty(scriptTags);
        Assert.All(
            scriptTags,
            tag => Assert.Contains("nonce=\"n0nce\"", tag.Value, StringComparison.Ordinal)
        );
    }

    [BuiltConsoleBundleFact]
    public void Render_WhenGivenTheBuiltBundlesIndex_LinksTheFamilysIconsFromTheAssetsTheHostServes()
    {
        var indexPath = BuiltConsoleBundleFactAttribute.IndexPath ?? string.Empty;
        var html = File.ReadAllText(indexPath, SecureEncoding.UTF8);
        using var bundle = new SinglePageBundle<ManagementConsolePage>(
            Path.GetDirectoryName(indexPath),
            static html => new ManagementConsolePage(html)
        );

        (string Pattern, string ContentType)[] icons =
        [
            (
                "<link rel=\"icon\" href=\"/assets/(?<asset>favicon-[^\"/]+\\.ico)\" sizes=\"32x32\"",
                "image/x-icon"
            ),
            (
                "<link rel=\"icon\" href=\"/assets/(?<asset>favicon-[^\"/]+\\.svg)\" type=\"image/svg\\+xml\"",
                "image/svg+xml"
            ),
            (
                "<link rel=\"apple-touch-icon\" href=\"/assets/(?<asset>apple-touch-icon-[^\"/]+\\.png)\"",
                "image/png"
            ),
        ];

        foreach (var (pattern, contentType) in icons)
        {
            var link = Regex.Match(html, pattern);
            Assert.True(link.Success, pattern);

            var result = bundle.ServeAsset(
                new DefaultHttpContext().Response,
                link.Groups["asset"].Value
            );
            Assert.Equal(contentType, Assert.IsType<PhysicalFileHttpResult>(result).ContentType);
        }

        // The page's policy admits images from its own origin, where the host serves the icons.
        var policy = ManagementConsolePageSecurity
            .Create(new Uri("https://id.example"), logoUrl: null)
            .Policy.ToContentSecurityPolicy();
        Assert.Contains("img-src 'self'", policy, StringComparison.Ordinal);
    }

    #endregion
}
