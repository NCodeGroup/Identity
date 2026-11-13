// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;

namespace NCode.Identity.OpenId.Setup;

public sealed class SetupPageTests
{
    private const string BundleIndexVariable = "NCODE_SETUP_BUNDLE_INDEX";

    #region Render Tests

    [Fact]
    public void Render_WhenThePageHasPlaceholders_ReplacesEveryPlaceholder()
    {
        const string html =
            "<meta content=\"__NCODE_CSP_NONCE__\"><script nonce=\"__NCODE_CSP_NONCE__\">";

        var rendered = new SetupPage(html).Render("abc", PathString.Empty);

        Assert.Equal("<meta content=\"abc\"><script nonce=\"abc\">", rendered);
    }

    [Fact]
    public void Render_WhenThePageHasNoPlaceholder_ReturnsThePageUnchanged()
    {
        var rendered = new SetupPage("<html></html>").Render("abc", PathString.Empty);

        Assert.Equal("<html></html>", rendered);
    }

    [Fact]
    public void Render_WhenThePageHasAHead_InsertsTheBaseAfterTheOpeningTag()
    {
        const string html = "<html><head><title>t</title></head><body></body></html>";

        var rendered = new SetupPage(html).Render("abc", PathString.Empty);

        Assert.Equal(
            "<html><head><base href=\"/setup/\" /><title>t</title></head><body></body></html>",
            rendered
        );
    }

    [Fact]
    public void Render_WhenTheHeadHasAttributes_InsertsTheBaseAfterTheOpeningTag()
    {
        const string html = "<html><HEAD lang=\"en\"><title>t</title></HEAD></html>";

        var rendered = new SetupPage(html).Render("abc", PathString.Empty);

        Assert.Equal(
            "<html><HEAD lang=\"en\"><base href=\"/setup/\" /><title>t</title></HEAD></html>",
            rendered
        );
    }

    [Fact]
    public void Render_WhenAPathBaseIsGiven_PrefixesTheBaseWithIt()
    {
        const string html = "<html><head></head></html>";

        var rendered = new SetupPage(html).Render("abc", new PathString("/op"));

        Assert.Contains("<base href=\"/op/setup/\" />", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenThePathBaseHoldsMarkup_EncodesItForTheAttribute()
    {
        const string html = "<html><head></head></html>";

        var rendered = new SetupPage(html).Render("abc", new PathString("/a\"><script>"));

        Assert.DoesNotContain("<script>", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("\"><", rendered, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_WhenThePageHasAHeaderElementButNoHead_KeepsThePageUnchanged()
    {
        const string html = "<header></header><body></body>";

        var rendered = new SetupPage(html).Render("abc", PathString.Empty);

        Assert.Equal(html, rendered);
    }

    [Fact]
    public void Render_WhenThePageHasNoHead_KeepsThePageWithoutABase()
    {
        const string html = "<body><script nonce=\"__NCODE_CSP_NONCE__\"></script></body>";

        var rendered = new SetupPage(html).Render("abc", PathString.Empty);

        Assert.Equal("<body><script nonce=\"abc\"></script></body>", rendered);
    }

    [Fact]
    public async Task Render_WhenGivenTheBuiltBundlesIndex_LeavesNoPlaceholderAndNoncesEveryScript()
    {
        var indexPath = Environment.GetEnvironmentVariable(BundleIndexVariable);
        if (string.IsNullOrEmpty(indexPath))
        {
            return;
        }

        var html = await File.ReadAllTextAsync(
            indexPath,
            SecureEncoding.UTF8,
            CancellationToken.None
        );

        var rendered = new SetupPage(html).Render("n0nce", PathString.Empty);

        Assert.DoesNotContain(SetupPage.NoncePlaceholder, rendered, StringComparison.Ordinal);
        Assert.Contains("<base href=\"/setup/\" />", rendered, StringComparison.Ordinal);

        var scriptTags = Regex.Matches(rendered, "<script\\b[^>]*>");
        Assert.NotEmpty(scriptTags);
        Assert.All(
            scriptTags,
            tag => Assert.Contains("nonce=\"n0nce\"", tag.Value, StringComparison.Ordinal)
        );
    }

    #endregion
}
