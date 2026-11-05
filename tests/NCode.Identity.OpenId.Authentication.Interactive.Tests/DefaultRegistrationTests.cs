// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
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
using NCode.Identity.Settings;
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
        serviceCollection.AddSingleton<IUiLocaleCatalog, StubUiLocaleCatalog>();
        serviceCollection
            .NewBuilder<OpenIdAuthenticationLibrary>()
            .AddInteractiveAuthentication(configuration, hostEnvironment, configureOptions);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider
            .GetRequiredService<IOptions<InteractiveAuthenticationOptions>>()
            .Value;
    }

    private static List<IUiLocale?> GetRegisteredLocales(IServiceCollection serviceCollection) =>
        serviceCollection
            .Where(descriptor => descriptor.ServiceType == typeof(IUiLocale))
            .Select(descriptor => descriptor.ImplementationInstance as IUiLocale)
            .ToList();

    private static InteractiveLocale CreateRewordedEnglish() =>
        InteractiveLocales.English with
        {
            Messages = InteractiveLocales.English.Messages with { SignInTitle = "Log in" },
        };

    private static InteractiveLocale CreateRewordedGerman() =>
        InteractiveLocales.German with
        {
            Messages = InteractiveLocales.German.Messages with { SignInTitle = "Einloggen" },
        };

    private static InteractiveLocale CreateItalian() =>
        InteractiveLocales.English with
        {
            LanguageTag = "it",
        };

    // Runs the registration calls the names list, in that order: "auth" is AddInteractiveAuthentication and any
    // other name is AddInteractiveLocale with the locale of that name.
    private List<IUiLocale?> RegisterInOrder(
        IReadOnlyDictionary<string, InteractiveLocale> locales,
        IEnumerable<string> calls
    )
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();

        foreach (var call in calls)
        {
            if (call == "auth")
            {
                builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);
            }
            else
            {
                builder.AddInteractiveLocale(locales[call]);
            }
        }

        return GetRegisteredLocales(serviceCollection);
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

    [Fact]
    public void AddInteractiveAuthentication_WhenTheHostRegisteredATagItShipsFirst_KeepsTheHostsLocaleInItsPlace()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        var reworded = CreateRewordedEnglish();

        builder.AddInteractiveLocale(reworded);
        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        var locales = serviceCollection
            .Where(descriptor => descriptor.ServiceType == typeof(IUiLocale))
            .Select(descriptor => descriptor.ImplementationInstance)
            .ToList();
        Assert.Equal(InteractiveLocales.All.Length, locales.Count);
        Assert.Same(reworded, locales[0]);
        Assert.DoesNotContain(InteractiveLocales.English, locales);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenTheHostRegisteredANewTagFirst_KeepsTheShippedLocalesAheadOfIt()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        var italian = InteractiveLocales.English with { LanguageTag = "it" };

        builder.AddInteractiveLocale(italian);
        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        var locales = GetRegisteredLocales(serviceCollection);
        Assert.Equal([.. InteractiveLocales.All, italian], locales);
        Assert.Same(InteractiveLocales.English, locales[0]);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenTheHostRewordedAShippedLocaleOtherThanEnglishFirst_KeepsEnglishFirst()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        var reworded = InteractiveLocales.German with
        {
            Messages = InteractiveLocales.German.Messages with { SignInTitle = "Einloggen" },
        };

        builder.AddInteractiveLocale(reworded);
        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        var locales = GetRegisteredLocales(serviceCollection);
        Assert.Equal(
            [
                InteractiveLocales.English,
                reworded,
                InteractiveLocales.Spanish,
                InteractiveLocales.French,
            ],
            locales
        );
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenTheHostRegisteredSeveralLocalesFirst_KeepsEachRewordedLocaleInItsShippedPlace()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        var italian = InteractiveLocales.English with { LanguageTag = "it" };
        var rewordedFrench = InteractiveLocales.French with
        {
            Messages = InteractiveLocales.French.Messages with { SignInTitle = "Connexion" },
        };

        builder.AddInteractiveLocale(italian);
        builder.AddInteractiveLocale(rewordedFrench);
        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        var locales = GetRegisteredLocales(serviceCollection);
        Assert.Equal(
            [
                InteractiveLocales.English,
                InteractiveLocales.German,
                InteractiveLocales.Spanish,
                rewordedFrench,
                italian,
            ],
            locales
        );
    }

    [Theory]
    [InlineData("de", "en", "auth")]
    [InlineData("auth", "de", "en")]
    [InlineData("de", "auth", "en")]
    [InlineData("en", "de", "auth")]
    public void AddInteractiveAuthentication_WhenTheHostRewordsTwoShippedLocales_KeepsTheShippedOrderWhateverTheOrderOfTheCalls(
        params string[] calls
    )
    {
        // The registered order is the shipped order whichever reworded shipped locale is registered first.
        var rewordedEnglish = CreateRewordedEnglish();
        var rewordedGerman = CreateRewordedGerman();

        var locales = RegisterInOrder(
            new Dictionary<string, InteractiveLocale>
            {
                ["en"] = rewordedEnglish,
                ["de"] = rewordedGerman,
            },
            calls
        );

        Assert.Equal(
            [
                rewordedEnglish,
                rewordedGerman,
                InteractiveLocales.Spanish,
                InteractiveLocales.French,
            ],
            locales
        );
    }

    [Theory]
    [InlineData("it", "de", "auth")]
    [InlineData("de", "it", "auth")]
    [InlineData("it", "auth", "de")]
    [InlineData("de", "auth", "it")]
    [InlineData("auth", "it", "de")]
    [InlineData("auth", "de", "it")]
    public void AddInteractiveAuthentication_WhenTheHostAddsATagAndRewordsAShippedLocale_RegistersOneOrderWhateverTheOrderOfTheCalls(
        params string[] calls
    )
    {
        var italian = CreateItalian();
        var rewordedGerman = CreateRewordedGerman();

        var locales = RegisterInOrder(
            new Dictionary<string, InteractiveLocale> { ["it"] = italian, ["de"] = rewordedGerman },
            calls
        );

        Assert.Equal(
            [
                InteractiveLocales.English,
                rewordedGerman,
                InteractiveLocales.Spanish,
                InteractiveLocales.French,
                italian,
            ],
            locales
        );
    }

    [Theory]
    [InlineData("pt", "it", "auth")]
    [InlineData("pt", "auth", "it")]
    [InlineData("auth", "pt", "it")]
    public void AddInteractiveAuthentication_WhenTheHostAddsSeveralTags_KeepsThemInTheOrderTheHostRegisteredThem(
        params string[] calls
    )
    {
        var italian = CreateItalian();
        var portuguese = InteractiveLocales.English with { LanguageTag = "pt" };

        var locales = RegisterInOrder(
            new Dictionary<string, InteractiveLocale> { ["it"] = italian, ["pt"] = portuguese },
            calls
        );

        Assert.Equal([.. InteractiveLocales.All, portuguese, italian], locales);
    }

    [Fact]
    public void AddInteractiveLocale_WhenALocaleIsRegisteredThroughAFactory_KeepsItsDescriptorInPlace()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        serviceCollection.AddSingleton<IUiLocale>(_ => new PlainLocale("it"));
        var before = serviceCollection[^1];

        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);
        serviceCollection.AddSingleton<IUiLocale>(_ => new PlainLocale("pt"));
        var after = serviceCollection[^1];
        builder.AddInteractiveLocale(CreateRewordedGerman());

        var descriptors = serviceCollection
            .Where(descriptor => descriptor.ServiceType == typeof(IUiLocale))
            .ToList();
        Assert.Same(before, descriptors[0]);
        Assert.Same(after, descriptors[^1]);
        Assert.Equal(InteractiveLocales.All.Length + 2, descriptors.Count);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenARegisteredLocaleCarriesNoMessages_FailsTheOptionsValidation()
    {
        // The validation runs when the host starts, so the misregistration stops the host, not its first page.
        var hostEnvironment = CreateHostEnvironment();
        var configuration = new ConfigurationBuilder().Build();
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IUiLocaleCatalog, StubUiLocaleCatalog>();
        serviceCollection.AddSingleton<IUiLocale>(new PlainLocale("it"));
        serviceCollection
            .NewBuilder<OpenIdAuthenticationLibrary>()
            .AddInteractiveAuthentication(configuration, hostEnvironment);
        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<
            IOptions<InteractiveAuthenticationOptions>
        >();

        var exception = Assert.Throws<OptionsValidationException>(() => options.Value);

        var failure = Assert.Single(exception.Failures);
        Assert.Contains("'it'", failure, StringComparison.Ordinal);
    }

    #endregion

    #region AddInteractiveLocale Tests

    [Fact]
    public void AddInteractiveLocale_WhenTheTagIsShipped_TakesTheShippedLocalesPlace()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        var reworded = CreateRewordedEnglish();

        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);
        builder.AddInteractiveLocale(reworded);

        var locales = serviceCollection
            .Where(descriptor => descriptor.ServiceType == typeof(IUiLocale))
            .Select(descriptor => descriptor.ImplementationInstance)
            .ToList();
        Assert.Equal(InteractiveLocales.All.Length, locales.Count);
        Assert.Same(reworded, locales[0]);
        Assert.DoesNotContain(InteractiveLocales.English, locales);
    }

    [Fact]
    public void AddInteractiveLocale_WhenTheTagDiffersOnlyInCase_TakesTheRegisteredLocalesPlace()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        var reworded = CreateRewordedEnglish() with { LanguageTag = "EN" };

        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);
        builder.AddInteractiveLocale(reworded);

        var locales = serviceCollection
            .Where(descriptor => descriptor.ServiceType == typeof(IUiLocale))
            .Select(descriptor => descriptor.ImplementationInstance)
            .ToList();
        Assert.Equal(InteractiveLocales.All.Length, locales.Count);
        Assert.Same(reworded, locales[0]);
    }

    [Fact]
    public void AddInteractiveLocale_WhenTheTagIsNew_RegistersTheLocaleAfterTheShippedOnes()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        var serviceCollection = new ServiceCollection();
        var builder = serviceCollection.NewBuilder<OpenIdAuthenticationLibrary>();
        var italian = InteractiveLocales.English with { LanguageTag = "it" };

        builder.AddInteractiveAuthentication(configuration, hostEnvironment.Object);
        builder.AddInteractiveLocale(italian);

        var locales = serviceCollection
            .Where(descriptor => descriptor.ServiceType == typeof(IUiLocale))
            .Select(descriptor => descriptor.ImplementationInstance)
            .ToList();
        Assert.Equal(InteractiveLocales.All.Length + 1, locales.Count);
        Assert.Same(InteractiveLocales.English, locales[0]);
        Assert.Same(italian, locales[^1]);
    }

    [Fact]
    public void AddInteractiveLocale_WhenTheLocaleIsNull_Throws()
    {
        var builder = new ServiceCollection().NewBuilder<OpenIdAuthenticationLibrary>();

        Assert.Throws<ArgumentNullException>(() => builder.AddInteractiveLocale(null!));
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

    // The registered locales as the core catalog lists them; the registration under test resolves nothing else.
    private sealed class StubUiLocaleCatalog(IEnumerable<IUiLocale> locales) : IUiLocaleCatalog
    {
        public IReadOnlyList<IUiLocale> Locales { get; } = [.. locales];

        public bool TryGet(string languageTag, [MaybeNullWhen(false)] out IUiLocale locale) =>
            throw new NotSupportedException();

        public IUiLocale Select(
            IReadOnlySettingCollection settings,
            IEnumerable<string> preferredLanguageTags
        ) => throw new NotSupportedException();

        public IUiLocale Select(
            IEnumerable<string> preferredLanguageTags,
            IReadOnlyCollection<string>? supportedLanguageTags,
            string? defaultLanguageTag
        ) => throw new NotSupportedException();
    }
}
