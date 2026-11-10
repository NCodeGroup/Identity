// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.AspNetCore;
using NCode.Buffers;
using NCode.Identity.Bff;
using NCode.Identity.Bff.Options;
using NCode.Identity.ManagementConsole.Options;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public partial class ManagementConsolePageHandlerTests : BaseTests
{
    private sealed class StubBffOptionsAccessor(BffOptions current) : IBffOptionsAccessor
    {
        public BffOptions Current { get; } = current;
    }

    private const string IndexHtml =
        "<!doctype html><html><head><meta property=\"csp-nonce\" content=\"__NCODE_CSP_NONCE__\"></head><body></body></html>";

    [GeneratedRegex("'nonce-([^']+)'")]
    private static partial Regex NonceRegex();

    private DirectoryInfo ContentRoot { get; } = Directory.CreateTempSubdirectory("console-");

    private string DistPath => Path.Combine(ContentRoot.FullName, "dist");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ContentRoot.Delete(recursive: true);
        }

        base.Dispose(disposing);
    }

    #region Helpers

    private static async Task<(DefaultHttpContext HttpContext, string Body)> HandleAsync(
        ManagementConsolePageHandler handler
    )
    {
        var serviceProvider = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Response.Body = new MemoryStream();

        var result = await handler.HandleAsync(httpContext);
        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        using var streamReader = new StreamReader(httpContext.Response.Body, SecureEncoding.UTF8);
        return (httpContext, await streamReader.ReadToEndAsync());
    }

    private ManagementConsolePageHandler CreateHandler(
        out SinglePageBundle<ManagementConsolePage> reader,
        string? faviconUrl = null
    )
    {
        var options = Microsoft.Extensions.Options.Options.Create(
            new ManagementConsoleOptions
            {
                BundleRootPath = "dist",
                Branding = new ManagementConsoleBrandingOptions { FaviconUrl = faviconUrl },
            }
        );
        reader = new SinglePageBundle<ManagementConsolePage>(
            DistPath,
            static html => new ManagementConsolePage(html)
        );

        var bffOptions = new StubBffOptionsAccessor(
            new BffOptions
            {
                Frontends =
                {
                    [ManagementConsoleDefaults.FrontendName] = new BffFrontendOptions
                    {
                        Authority = "https://id.example/tenants/control",
                    },
                },
            }
        );

        return new ManagementConsolePageHandler(reader, bffOptions, options);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTheBundleIsMissing_AnswersA503ProblemThatIsNotCached()
    {
        var handler = CreateHandler(out var reader);
        using var _ = reader;

        var (httpContext, body) = await HandleAsync(handler);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, httpContext.Response.StatusCode);
        Assert.Equal(
            "no-store, no-cache, max-age=0",
            httpContext.Response.Headers.CacheControl.ToString()
        );
        Assert.Contains("The management console is not built.", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenTheBundleExists_WritesThePageWithTheNonceOfThePolicy()
    {
        Directory.CreateDirectory(DistPath);
        await File.WriteAllTextAsync(
            Path.Combine(DistPath, "index.html"),
            IndexHtml,
            SecureEncoding.UTF8,
            CancellationToken.None
        );
        var handler = CreateHandler(out var reader);
        using var _ = reader;

        var (httpContext, body) = await HandleAsync(handler);

        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.StartsWith("text/html", httpContext.Response.ContentType, StringComparison.Ordinal);

        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        var match = NonceRegex().Match(policy);
        Assert.True(match.Success);
        Assert.Contains($"content=\"{match.Groups[1].Value}\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(
            ManagementConsoleDefaults.NoncePlaceholder,
            body,
            StringComparison.Ordinal
        );
        Assert.Contains("form-action 'self' https://id.example", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenTheBrandingHasAFavicon_WritesItsLinkAndAdmitsItsOrigin()
    {
        Directory.CreateDirectory(DistPath);
        await File.WriteAllTextAsync(
            Path.Combine(DistPath, "index.html"),
            "<!doctype html><html><head><link rel=\"icon\" href=\"./assets/favicon-ab12.ico\"></head><body></body></html>",
            SecureEncoding.UTF8,
            CancellationToken.None
        );
        var handler = CreateHandler(out var reader, "https://cdn.example/tab.ico");
        using var _ = reader;

        var (httpContext, body) = await HandleAsync(handler);

        Assert.Contains(
            "<link rel=\"icon\" href=\"https://cdn.example/tab.ico\" />",
            body,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("favicon-ab12", body, StringComparison.Ordinal);

        var policy = httpContext.Response.Headers.ContentSecurityPolicy.ToString();
        var imgSrc = policy
            .Split(';', StringSplitOptions.TrimEntries)
            .Single(directive => directive.StartsWith("img-src ", StringComparison.Ordinal));
        Assert.Contains("https://cdn.example", imgSrc, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_WhenTheBrandingHasNoFavicon_KeepsTheIcons()
    {
        Directory.CreateDirectory(DistPath);
        await File.WriteAllTextAsync(
            Path.Combine(DistPath, "index.html"),
            "<!doctype html><html><head><link rel=\"icon\" href=\"./assets/favicon-ab12.ico\"></head><body></body></html>",
            SecureEncoding.UTF8,
            CancellationToken.None
        );
        var handler = CreateHandler(out var reader);
        using var _ = reader;

        var (_, body) = await HandleAsync(handler);

        Assert.Contains("favicon-ab12", body, StringComparison.Ordinal);
    }

    #endregion
}
