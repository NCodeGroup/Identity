// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Events;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets;
using NCode.Identity.SharedState;
using NCode.Registration;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.Tests;

public sealed class DefaultRegistrationTests : IAsyncLifetime
{
    private ServiceCollection Services { get; } = new();

    private IServiceBuilder<OpenIdClientLibrary> ClientBuilder { get; }

    private IServiceBuilder<EventsLibrary> EventsBuilder { get; }

    private ServiceProvider? Provider { get; set; }

    public DefaultRegistrationTests()
    {
        Services.AddLogging();
        ClientBuilder = Services
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        EventsBuilder = Services.AddEventServices();

        Services
            .AddSharedStateServices()
            .AddInProcessExpiringKeySet()
            .AddInProcessExpiringValueSet()
            .AddInProcessExpiringIndexSet();
        Services.AddSingleton(new Mock<IHostEnvironment>().Object);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (Provider is not null)
            await Provider.DisposeAsync();
    }

    private ServiceProvider BuildProvider()
    {
        Provider = Services.BuildServiceProvider();
        return Provider;
    }

    #region AddBffLibrary Tests

    [Fact]
    public void AddBffLibrary_RegistersTheSessionServices()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IBffSessionStore>());
        Assert.NotNull(provider.GetRequiredService<IBffSessionManager>());
        Assert.IsType<DefaultBffConnectionRevalidator>(
            provider.GetRequiredService<IBffConnectionRevalidator>()
        );
    }

    [Fact]
    public void AddBffLibrary_RegistersEveryTokenRequirement()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        var names = provider
            .GetServices<IBffTokenRequirement>()
            .Select(requirement => requirement.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal(
            [
                BffTokenRequirementNames.Client,
                BffTokenRequirementNames.Exchange,
                BffTokenRequirementNames.None,
                BffTokenRequirementNames.User,
                BffTokenRequirementNames.UserOrClient,
            ],
            names
        );
    }

    [Fact]
    public void AddBffLibrary_RegistersTheClientAuthenticationCheck()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        Assert.Single(
            provider.GetServices<IBffFrontendSetCheck>().OfType<BffClientAuthenticationCheck>()
        );
    }

    [Fact]
    public void AddBffLibrary_RegistersTheKeyRingCheck()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        Assert.Single(provider.GetServices<IBffFrontendSetCheck>().OfType<BffKeyRingCheck>());
    }

    [Fact]
    public void AddBffLibrary_RegistersTheRemoteApiServices()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IHttpForwarder>());
        Assert.NotNull(provider.GetRequiredService<BffRemoteApiInvokerFactory>());
        Assert.NotNull(provider.GetRequiredService<BffRemoteApiRegistry>());
        Assert.NotNull(provider.GetRequiredService<BffRemoteApiHandler>());
    }

    [Fact]
    public void AddBffLibrary_RegistersTheFrontendServices()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        var names = provider
            .GetServices<IBffFrontendSelector>()
            .Select(selector => selector.Name)
            .Order(StringComparer.Ordinal);
        Assert.Equal([BffFrontendSelectorNames.Host, BffFrontendSelectorNames.Path], names);
        Assert.NotNull(provider.GetRequiredService<BffFrontendCatalog>());
        Assert.Contains(
            provider.GetServices<MatcherPolicy>(),
            policy => policy is BffFrontendMatcherPolicy
        );
    }

    [Fact]
    public void AddBffLibrary_RegistersTheOptionsAccessorAsTheCatalog()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        Assert.Same(
            provider.GetRequiredService<BffFrontendCatalog>(),
            provider.GetRequiredService<IBffOptionsAccessor>()
        );
    }

    [Fact]
    public void AddBffLibrary_ConfiguresHostAntiforgeryCookie()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        var cookie = provider.GetRequiredService<IOptions<AntiforgeryOptions>>().Value.Cookie;
        Assert.Equal(BffDefaults.AntiforgeryCookieName, cookie.Name);
        Assert.Equal(CookieSecurePolicy.Always, cookie.SecurePolicy);
        Assert.Equal("/", cookie.Path);
        Assert.Equal(SameSiteMode.Strict, cookie.SameSite);
        Assert.True(cookie.HttpOnly);
        Assert.Null(cookie.Domain);
    }

    [Fact]
    public void AddBffLibrary_RegistersTheEndpointStartupCheck()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        Assert.Contains(
            Services,
            descriptor =>
                descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(BffEndpointStartupCheck)
        );
    }

    [Fact]
    public void AddBffLibrary_WhenCalledTwice_Throws()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        Assert.Throws<InvalidOperationException>(() => ClientBuilder.AddBffLibrary(EventsBuilder));
    }

    [Fact]
    public async Task AddBffLibrary_RegistersTheBffSchemeAsTheDefault()
    {
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var scheme = await schemes.GetSchemeAsync(BffDefaults.AuthenticationScheme);
        var defaultScheme = await schemes.GetDefaultAuthenticateSchemeAsync();
        Assert.NotNull(scheme);
        Assert.NotNull(defaultScheme);
        Assert.Equal(BffDefaults.AuthenticationScheme, defaultScheme.Name);
    }

    [Fact]
    public async Task AddBffLibrary_WhenTheHostSetsADefaultScheme_KeepsIt()
    {
        const string hostScheme = "Host";
        Services
            .AddAuthentication(hostScheme)
            .AddScheme<AuthenticationSchemeOptions, StubAuthenticationHandler>(hostScheme, null);
        ClientBuilder.AddBffLibrary(EventsBuilder);

        var provider = BuildProvider();

        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();
        var defaultScheme = await schemes.GetDefaultAuthenticateSchemeAsync();
        Assert.NotNull(defaultScheme);
        Assert.Equal(hostScheme, defaultScheme.Name);
    }

    private sealed class StubAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    ) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());
    }

    #endregion

    #region AddBffConfiguration Tests

    [Fact]
    public void AddBffConfiguration_WithUnknownKey_ThrowsOnValue()
    {
        const string key = $"{BffDefaults.ConfigurationSectionName}:NoSuchKey";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = "x" })
            .Build();
        ClientBuilder.AddBffLibrary(EventsBuilder).AddBffConfiguration(configuration);

        var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<BffOptions>>();
        var exception = Assert.Throws<InvalidOperationException>(() => options.Value);
        Assert.Contains("NoSuchKey", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddBffConfiguration_SectionReloads_TheCatalogFollows()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Bff:Frontends:web:Authority"] = "https://op.example",
                    ["Bff:Frontends:web:PublicOrigin"] = "https://orders.example",
                    ["Bff:Frontends:web:ClientId"] = "orders-web",
                    ["Bff:Frontends:web:ReturnUrls:0"] = "/",
                }
            )
            .Build();
        ClientBuilder.AddBffLibrary(EventsBuilder).AddBffConfiguration(configuration);
        var provider = BuildProvider();
        var catalog = provider.GetRequiredService<BffFrontendCatalog>();
        Assert.Equal(["web"], catalog.Frontends.Select(frontend => frontend.Name));

        configuration["Bff:Frontends:admin:Authority"] = "https://op.example";
        configuration["Bff:Frontends:admin:PublicOrigin"] = "https://orders.example";
        configuration["Bff:Frontends:admin:ClientId"] = "admin-web";
        configuration["Bff:Frontends:admin:ReturnUrls:0"] = "/admin";
        configuration["Bff:Frontends:admin:PathPrefix"] = "/admin/bff";
        configuration["Bff:Frontends:admin:Selection:Rule"] = "path";
        configuration["Bff:Frontends:admin:Selection:Values:0"] = "/admin";
        configuration.Reload();

        Assert.Equal(["admin", "web"], catalog.Frontends.Select(frontend => frontend.Name));
    }

    #endregion
}
