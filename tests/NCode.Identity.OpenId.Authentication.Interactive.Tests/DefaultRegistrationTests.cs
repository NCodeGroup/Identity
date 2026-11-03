// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Locales;
using NCode.Identity.OpenId.Tenants;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class DefaultRegistrationTests : IDisposable
{
    private static string ContentRootPath { get; } = Path.Combine(Path.GetTempPath(), "host");

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private IHostEnvironment CreateHostEnvironment()
    {
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        hostEnvironment.Setup(x => x.ContentRootPath).Returns(ContentRootPath).Verifiable();
        return hostEnvironment.Object;
    }

    private static InteractiveAuthenticationOptions ResolveOptions(
        IHostEnvironment hostEnvironment,
        Dictionary<string, string?> configurationData,
        Action<InteractiveAuthenticationOptions>? configureOptions = null
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .NewBuilder<OpenIdAuthenticationLibrary>()
            .AddInteractiveAuthentication(configuration, hostEnvironment, configureOptions);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider
            .GetRequiredService<IOptions<InteractiveAuthenticationOptions>>()
            .Value;
    }

    #region AddInteractiveAuthentication Tests

    [Fact]
    public void AddInteractiveAuthentication_WhenSectionIsAbsent_LeavesTheBundleUnset()
    {
        var hostEnvironment = CreateHostEnvironment();

        var options = ResolveOptions(hostEnvironment, []);

        Assert.Null(options.BundleRootPath);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenBundleRootPathIsRelative_ResolvesItAgainstTheContentRoot()
    {
        var hostEnvironment = CreateHostEnvironment();

        var options = ResolveOptions(
            hostEnvironment,
            new Dictionary<string, string?>
            {
                ["InteractiveAuthentication:BundleRootPath"] = "../frontend/apps/auth/dist",
            }
        );

        Assert.Equal(
            Path.Combine(ContentRootPath, "../frontend/apps/auth/dist"),
            options.BundleRootPath
        );
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenBundleRootPathIsRooted_UsesItAsIs()
    {
        var rooted = Path.Combine(Path.GetTempPath(), "bundle");

        var hostEnvironment = CreateHostEnvironment();

        var options = ResolveOptions(
            hostEnvironment,
            new Dictionary<string, string?>
            {
                ["InteractiveAuthentication:BundleRootPath"] = rooted,
            }
        );

        Assert.Equal(rooted, options.BundleRootPath);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenConfiguredInCode_OverridesConfiguration()
    {
        var hostEnvironment = CreateHostEnvironment();

        var options = ResolveOptions(
            hostEnvironment,
            new Dictionary<string, string?> { ["InteractiveAuthentication:BundleRootPath"] = "a" },
            configure => configure.BundleRootPath = "b"
        );

        Assert.Equal(Path.Combine(ContentRootPath, "b"), options.BundleRootPath);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenSectionHasUnknownKey_Throws()
    {
        // A misspelled key would otherwise leave the surface without its bundle and no sign of why.
        var hostEnvironment = MockRepository.Create<IHostEnvironment>().Object;

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ResolveOptions(
                hostEnvironment,
                new Dictionary<string, string?>
                {
                    ["InteractiveAuthentication:BundlePath"] = "../frontend/apps/auth/dist",
                }
            )
        );

        Assert.Contains("'InteractiveAuthentication'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'BundlePath'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddInteractiveAuthentication_WhenNoBackendOffersAPasswordChange_ReportsItNotSupported()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .NewBuilder<OpenIdAuthenticationLibrary>()
            .AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var passwordChanger = serviceProvider.GetRequiredService<IInteractivePasswordChanger>();

        var result = await passwordChanger.ChangePasswordAsync(
            null!,
            new ClaimsPrincipal(),
            "current",
            "new",
            CancellationToken.None
        );

        Assert.Equal(InteractivePasswordChangeResult.NotSupported, result);
    }

    [Fact]
    public void AddInteractiveAuthentication_ConfiguresAHostOnlySessionCookie()
    {
        using var serviceProvider = BuildCookieServiceProvider();

        var cookie = serviceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(InteractiveAuthenticationDefaults.CookieSchemeName)
            .Cookie;

        Assert.Equal(InteractiveAuthenticationDefaults.SessionCookieName, cookie.Name);
        Assert.Equal("/", cookie.Path);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
        Assert.True(cookie.HttpOnly);
        Assert.Equal(SameSiteMode.Lax, cookie.SameSite);
    }

    [Fact]
    public void AddInteractiveAuthentication_ConfiguresAHostOnlyAntiforgeryCookie()
    {
        using var serviceProvider = BuildCookieServiceProvider();

        var cookie = serviceProvider
            .GetRequiredService<IOptions<AntiforgeryOptions>>()
            .Value.Cookie;

        Assert.Equal(InteractiveAuthenticationDefaults.AntiforgeryCookieName, cookie.Name);
        Assert.Equal("/", cookie.Path);
        Assert.Equal(CookieSecurePolicy.SameAsRequest, cookie.SecurePolicy);
    }

    [Fact]
    public void AddInteractiveAuthentication_RegistersTheLocalesTheSurfaceShipsEnglishFirst()
    {
        using var serviceProvider = BuildCookieServiceProvider();

        var locales = serviceProvider.GetServices<IUiLocale>().ToList();

        Assert.Equal(
            InteractiveLocales.All.Select(locale => locale.LanguageTag),
            locales.Select(locale => locale.LanguageTag)
        );
        Assert.Equal("en", locales[0].LanguageTag);
        Assert.All(locales, locale => Assert.IsType<InteractiveLocale>(locale));
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenCalledTwice_RegistersEachLocaleOnce()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();

        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);
        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        Assert.Equal(
            InteractiveLocales.All.Length,
            serviceCollection.Count(descriptor => descriptor.ServiceType == typeof(IUiLocale))
        );
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("fr-CA de", "&ui_locales=fr-CA%20de")]
    public async Task AddInteractiveAuthentication_RedirectsALoginChallengeWithTheRequestsUiLocales(
        string? uiLocales,
        string expectedQuerySuffix
    )
    {
        using var serviceProvider = BuildCookieServiceProvider(
            RoutePatternFactory.Parse("/{tenantId}")
        );
        var options = serviceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(InteractiveAuthenticationDefaults.CookieSchemeName);
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        httpContext.Request.RouteValues["tenantId"] = "acme";
        const string continueUrl = "https://op.example/acme/oauth2/continue?state=k";
        var properties = new AuthenticationProperties { RedirectUri = continueUrl };
        properties.SetParameter("ui_locales", uiLocales);
        var scheme = new AuthenticationScheme(
            InteractiveAuthenticationDefaults.CookieSchemeName,
            displayName: null,
            typeof(CookieAuthenticationHandler)
        );
        var context = new RedirectContext<CookieAuthenticationOptions>(
            httpContext,
            scheme,
            options,
            properties,
            "/ignored"
        );

        await options.Events.OnRedirectToLogin(context);

        Assert.Equal(
            "/acme/account/login?ReturnUrl="
                + Uri.EscapeDataString(continueUrl)
                + expectedQuerySuffix,
            context.RedirectUri
        );
        Assert.Equal(context.RedirectUri, httpContext.Response.Headers.Location);
    }

    #endregion

    private ServiceProvider BuildCookieServiceProvider(RoutePattern? tenantRoute = null)
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var tenantResolver = MockRepository.Create<ITenantResolver>();
        if (tenantRoute is not null)
        {
            tenantResolver.Setup(x => x.GetTenantRoute()).Returns(tenantRoute).Verifiable();
        }

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging().AddRouting();
        serviceCollection.AddSingleton(tenantResolver.Object);
        serviceCollection
            .NewBuilder<OpenIdAuthenticationLibrary>()
            .AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        return serviceCollection.BuildServiceProvider();
    }
}
