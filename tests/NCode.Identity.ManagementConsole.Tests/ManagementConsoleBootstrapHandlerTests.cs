// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Bff;
using NCode.Identity.Bff.Options;
using NCode.Identity.ManagementConsole.Options;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsoleBootstrapHandlerTests : BaseTests
{
    private sealed class StubBffOptionsAccessor(BffOptions current) : IBffOptionsAccessor
    {
        public BffOptions Current { get; } = current;
    }

    private static BffOptions CreateBffOptions(string pathPrefix = "/bff") =>
        new()
        {
            Frontends =
            {
                [ManagementConsoleDefaults.FrontendName] = new BffFrontendOptions
                {
                    PathPrefix = pathPrefix,
                },
            },
        };

    private static ManagementConsoleBootstrapHandler CreateHandler(
        ManagementConsoleOptions options,
        BffOptions bffOptions
    ) =>
        new(
            Microsoft.Extensions.Options.Options.Create(options),
            new StubBffOptionsAccessor(bffOptions)
        );

    private static async Task<(DefaultHttpContext HttpContext, JsonElement Document)> HandleAsync(
        ManagementConsoleBootstrapHandler handler,
        string pathBase = ""
    )
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext { RequestServices = services };
        httpContext.Request.PathBase = pathBase;
        httpContext.Response.Body = new MemoryStream();

        await handler.Handle(httpContext).ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;
        var document = await JsonSerializer.DeserializeAsync<JsonElement>(
            httpContext.Response.Body
        );
        return (httpContext, document);
    }

    #region Create Tests

    [Fact]
    public void Create_WithDefaults_PublishesTheConsoleCoordinates()
    {
        var document = ManagementConsoleBootstrapDocument.Create(
            new ManagementConsoleOptions(),
            CreateBffOptions(),
            PathString.Empty
        );

        Assert.Equal("/management", document.ApiBaseUrl);
        Assert.Equal("/bff", document.Bff.BasePath);
        Assert.Equal("X-CSRF", document.Bff.AntiforgeryHeader.Name);
        Assert.Equal("1", document.Bff.AntiforgeryHeader.Value);
        Assert.Empty(document.Features);
        Assert.Null(document.Branding);
    }

    [Fact]
    public void Create_WithPathBase_RootsTheUrlsAtThePathBase()
    {
        var document = ManagementConsoleBootstrapDocument.Create(
            new ManagementConsoleOptions(),
            CreateBffOptions(),
            new PathString("/console")
        );

        Assert.Equal("/console/management", document.ApiBaseUrl);
        Assert.Equal("/console/bff", document.Bff.BasePath);
    }

    [Fact]
    public void Create_WithCustomPrefixAndHeader_PublishesThem()
    {
        var bffOptions = CreateBffOptions("/auth");
        bffOptions.Antiforgery.HeaderName = "X-Custom";
        bffOptions.Antiforgery.HeaderValue = "yes";

        var document = ManagementConsoleBootstrapDocument.Create(
            new ManagementConsoleOptions(),
            bffOptions,
            PathString.Empty
        );

        Assert.Equal("/auth", document.Bff.BasePath);
        Assert.Equal("X-Custom", document.Bff.AntiforgeryHeader.Name);
        Assert.Equal("yes", document.Bff.AntiforgeryHeader.Value);
    }

    [Fact]
    public void Create_WithFeaturesAndBranding_PublishesThem()
    {
        var options = new ManagementConsoleOptions
        {
            Features = { ["audit"] = true, ["beta"] = false },
            Branding =
            {
                DisplayName = "Console",
                LogoUrl = "https://cdn.example.com/logo.svg",
                PrimaryColor = "#112233",
                SecondaryColor = "#445566",
                Mode = "dark",
            },
        };

        var document = ManagementConsoleBootstrapDocument.Create(
            options,
            CreateBffOptions(),
            PathString.Empty
        );

        Assert.True(document.Features["audit"]);
        Assert.False(document.Features["beta"]);
        Assert.NotNull(document.Branding);
        Assert.Equal("Console", document.Branding.DisplayName);
        Assert.Equal("https://cdn.example.com/logo.svg", document.Branding.LogoUrl);
        Assert.Equal("#112233", document.Branding.PrimaryColor);
        Assert.Equal("#445566", document.Branding.SecondaryColor);
        Assert.Equal("dark", document.Branding.Mode);
    }

    [Fact]
    public void Create_WithBlankBranding_OmitsTheBranding()
    {
        var options = new ManagementConsoleOptions
        {
            Branding =
            {
                DisplayName = "  ",
                LogoUrl = "",
                PrimaryColor = null,
            },
        };

        var document = ManagementConsoleBootstrapDocument.Create(
            options,
            CreateBffOptions(),
            PathString.Empty
        );

        Assert.Null(document.Branding);
    }

    [Fact]
    public void Create_WithPartialBranding_OmitsTheBlankValues()
    {
        var options = new ManagementConsoleOptions
        {
            Branding = { DisplayName = "Console", LogoUrl = " " },
        };

        var document = ManagementConsoleBootstrapDocument.Create(
            options,
            CreateBffOptions(),
            PathString.Empty
        );

        Assert.NotNull(document.Branding);
        Assert.Equal("Console", document.Branding.DisplayName);
        Assert.Null(document.Branding.LogoUrl);
        Assert.Null(document.Branding.Mode);
    }

    #endregion

    #region Handle Tests

    [Fact]
    public async Task Handle_Always_MarksTheResponseNoStore()
    {
        var handler = CreateHandler(new ManagementConsoleOptions(), CreateBffOptions());

        var (httpContext, _) = await HandleAsync(handler);

        Assert.Equal(
            "no-store, no-cache, max-age=0",
            httpContext.Response.Headers.CacheControl.ToString()
        );
    }

    [Fact]
    public async Task Handle_WithDefaults_WritesTheCamelCaseDocument()
    {
        var handler = CreateHandler(new ManagementConsoleOptions(), CreateBffOptions());

        var (_, document) = await HandleAsync(handler);

        Assert.Equal("/management", document.GetProperty("apiBaseUrl").GetString());
        var bff = document.GetProperty("bff");
        Assert.Equal("/bff", bff.GetProperty("basePath").GetString());
        var header = bff.GetProperty("antiforgeryHeader");
        Assert.Equal("X-CSRF", header.GetProperty("name").GetString());
        Assert.Equal("1", header.GetProperty("value").GetString());
        Assert.Equal(JsonValueKind.Object, document.GetProperty("features").ValueKind);
        Assert.False(document.TryGetProperty("branding", out _));
    }

    [Fact]
    public async Task Handle_WithBranding_OmitsTheBlankBrandingValues()
    {
        var options = new ManagementConsoleOptions
        {
            Features = { ["audit"] = true },
            Branding = { DisplayName = "Console", Mode = "light" },
        };
        var handler = CreateHandler(options, CreateBffOptions());

        var (_, document) = await HandleAsync(handler, "/console");

        Assert.Equal("/console/management", document.GetProperty("apiBaseUrl").GetString());
        Assert.True(document.GetProperty("features").GetProperty("audit").GetBoolean());
        var branding = document.GetProperty("branding");
        Assert.Equal("Console", branding.GetProperty("displayName").GetString());
        Assert.Equal("light", branding.GetProperty("mode").GetString());
        Assert.False(branding.TryGetProperty("logoUrl", out _));
    }

    #endregion
}
