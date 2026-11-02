// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Client.Authentication;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Authentication;

public class SharedSecretClientAuthenticatorTests : BaseTests, IAsyncLifetime
{
    private const string Endpoint = "https://op.example.test/token";

    private ServiceProvider ServiceProvider { get; } = CreateServiceProvider();

    private static ServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary();
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            "ring",
            (serviceProvider, _) => CreateRing(serviceProvider, CreateSymmetricKey(serviceProvider))
        );
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            "rsa-ring",
            (serviceProvider, _) => CreateRing(serviceProvider, CreateRsaKey(serviceProvider))
        );
        return serviceCollection.BuildServiceProvider();
    }

    private static ISecretKeyCollectionProvider CreateRing(
        IServiceProvider serviceProvider,
        SecretKey key
    ) =>
        serviceProvider
            .GetRequiredService<ISecretKeyCollectionProviderFactory>()
            .CreateStatic([key]);

    private static SecretKey CreateSymmetricKey(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateSymmetric(new KeyMetadata { KeyId = "s1" }, "s p".AsSpan());

    private static SecretKey CreateRsaKey(IServiceProvider serviceProvider) =>
        serviceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "r1" }, RSA.Create(2048));

    private IClientKeyRingProvider CreateKeyRingProvider() =>
        new DefaultClientKeyRingProvider(ServiceProvider);

    private static ClientRegistration CreateRegistration(string method, string? keyRing) =>
        new("https://op.example.test", "client:1", method) { KeyRing = keyRing };

    private static ClientAuthenticationContext CreateContext(
        ClientRegistration registration,
        List<KeyValuePair<string, string>> parameters
    ) =>
        new(
            registration,
            new OpenIdProviderMetadata(),
            Endpoint,
            new HttpRequestMessage(HttpMethod.Post, Endpoint),
            parameters
        );

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await ServiceProvider.DisposeAsync();

    #region MethodName Tests

    [Fact]
    public void MethodName_Basic_IsClientSecretBasic()
    {
        var authenticator = new ClientSecretBasicClientAuthenticator(CreateKeyRingProvider());

        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            authenticator.MethodName
        );
    }

    [Fact]
    public void MethodName_Post_IsClientSecretPost()
    {
        var authenticator = new ClientSecretPostClientAuthenticator(CreateKeyRingProvider());

        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            authenticator.MethodName
        );
    }

    #endregion

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_Basic_SetsAuthorizationHeader()
    {
        var authenticator = new ClientSecretBasicClientAuthenticator(CreateKeyRingProvider());
        var registration = CreateRegistration(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            "ring"
        );
        var context = CreateContext(registration, []);

        await authenticator.AuthenticateAsync(context, CancellationToken.None);

        var header = context.Request.Headers.Authorization;
        Assert.NotNull(header);
        Assert.Equal("Basic", header.Scheme);
        Assert.Equal(
            Convert.ToBase64String(SecureEncoding.UTF8.GetBytes("client%3A1:s+p")),
            header.Parameter
        );
    }

    [Fact]
    public async Task AuthenticateAsync_Post_AddsClientIdAndSecretParameters()
    {
        var authenticator = new ClientSecretPostClientAuthenticator(CreateKeyRingProvider());
        var registration = CreateRegistration(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            "ring"
        );
        var parameters = new List<KeyValuePair<string, string>>();
        var context = CreateContext(registration, parameters);

        await authenticator.AuthenticateAsync(context, CancellationToken.None);

        Assert.Equal(
            [
                new KeyValuePair<string, string>("client_id", "client:1"),
                new KeyValuePair<string, string>("client_secret", "s p"),
            ],
            parameters
        );
    }

    [Fact]
    public async Task AuthenticateAsync_WithNullKeyRing_Throws()
    {
        var authenticator = new ClientSecretBasicClientAuthenticator(CreateKeyRingProvider());
        var registration = CreateRegistration(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            keyRing: null
        );
        var context = CreateContext(registration, []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await authenticator.AuthenticateAsync(context, CancellationToken.None)
        );
    }

    [Fact]
    public async Task AuthenticateAsync_WithUnregisteredKeyRing_Throws()
    {
        var authenticator = new ClientSecretPostClientAuthenticator(CreateKeyRingProvider());
        var registration = CreateRegistration(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            "missing"
        );
        var context = CreateContext(registration, []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await authenticator.AuthenticateAsync(context, CancellationToken.None)
        );
    }

    [Fact]
    public async Task AuthenticateAsync_WithRsaOnlyKeyRing_Throws()
    {
        var authenticator = new ClientSecretBasicClientAuthenticator(CreateKeyRingProvider());
        var registration = CreateRegistration(
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            "rsa-ring"
        );
        var context = CreateContext(registration, []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await authenticator.AuthenticateAsync(context, CancellationToken.None)
        );
    }

    #endregion
}
