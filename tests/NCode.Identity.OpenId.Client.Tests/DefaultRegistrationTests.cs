#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.Secrets;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests;

public sealed class DefaultRegistrationTests : IAsyncLifetime
{
    private ServiceCollection Services { get; } = new();

    private IServiceBuilder<JwtLibrary> JwtBuilder { get; }

    private ServiceProvider? Provider { get; set; }

    public DefaultRegistrationTests()
    {
        Services.AddLogging();
        JwtBuilder = Services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
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

    private sealed class CustomClientAuthenticator : IClientAuthenticator
    {
        public string MethodName => "custom";

        public ValueTask AuthenticateAsync(
            ClientAuthenticationContext context,
            CancellationToken cancellationToken
        ) => ValueTask.CompletedTask;
    }

    #region AddOpenIdClientLibrary Tests

    [Fact]
    public void AddOpenIdClientLibrary_RegistersThePublicServices()
    {
        JwtBuilder.AddOpenIdClientLibrary();

        var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IProviderMetadataResolver>());
        Assert.NotNull(provider.GetRequiredService<IProviderKeySetResolver>());
        Assert.NotNull(provider.GetRequiredService<IClientKeyRingProvider>());
        Assert.NotNull(provider.GetRequiredService<ITokenClient>());
        Assert.NotNull(provider.GetRequiredService<ITokenRevocationClient>());
        Assert.NotNull(provider.GetRequiredService<IIdTokenValidator>());
    }

    [Fact]
    public void AddOpenIdClientLibrary_RegistersTheThreeBuiltInAuthenticators()
    {
        JwtBuilder.AddOpenIdClientLibrary();

        var provider = BuildProvider();

        var names = provider
            .GetServices<IClientAuthenticator>()
            .Select(authenticator => authenticator.MethodName)
            .Order(StringComparer.Ordinal)
            .ToList();
        var expected = new[]
        {
            ClientAuthenticationMethods.ClientSecretBasic,
            ClientAuthenticationMethods.ClientSecretPost,
            ClientAuthenticationMethods.PrivateKeyJwt,
        }
            .Order(StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expected, names);
    }

    [Fact]
    public void AddOpenIdClientLibrary_WhenCalledTwice_Throws()
    {
        JwtBuilder.AddOpenIdClientLibrary();

        Assert.Throws<InvalidOperationException>(() => JwtBuilder.AddOpenIdClientLibrary());
    }

    [Fact]
    public void AddOpenIdClientLibrary_WithInvalidOptions_ThrowsOnValue()
    {
        JwtBuilder.AddOpenIdClientLibrary();
        Services.Configure<OpenIdClientOptions>(options => options.RequestTimeout = TimeSpan.Zero);

        var provider = BuildProvider();

        var options = provider.GetRequiredService<IOptions<OpenIdClientOptions>>();
        Assert.Throws<OptionsValidationException>(() => options.Value);
    }

    [Fact]
    public void AddOpenIdClientLibrary_ConfiguresHandlerWithoutRedirectsOrCookies()
    {
        JwtBuilder.AddOpenIdClientLibrary();

        var provider = BuildProvider();

        var handler = provider
            .GetRequiredService<IHttpMessageHandlerFactory>()
            .CreateHandler(OpenIdClientDefaults.HttpClientName);
        while (handler is DelegatingHandler delegating)
            handler = delegating.InnerHandler!;

        var socketsHandler = Assert.IsType<SocketsHttpHandler>(handler);
        Assert.False(socketsHandler.AllowAutoRedirect);
        Assert.False(socketsHandler.UseCookies);
    }

    #endregion

    #region AddClientAuthenticator Tests

    [Fact]
    public void AddClientAuthenticator_AddsTheAuthenticator()
    {
        JwtBuilder.AddOpenIdClientLibrary().AddClientAuthenticator<CustomClientAuthenticator>();

        var provider = BuildProvider();

        var names = provider.GetServices<IClientAuthenticator>().Select(x => x.MethodName);
        Assert.Contains("custom", names);
    }

    #endregion
}
