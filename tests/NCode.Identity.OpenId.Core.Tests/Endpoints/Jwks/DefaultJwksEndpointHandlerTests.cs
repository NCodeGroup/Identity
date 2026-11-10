// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Jwks;

public sealed class DefaultJwksEndpointHandlerTests : BaseTests, IAsyncLifetime
{
    private static DateTimeOffset Now { get; } = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private ServiceProvider ServiceProvider { get; }

    public DefaultJwksEndpointHandlerTests()
    {
        // The real key-type catalog exports each key's public JWK, so the test proves the keys as published.
        var serviceCollection = new ServiceCollection();
        var secrets = serviceCollection.AddSecretsLibrary();
        secrets.AddJoseLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        Handler = new DefaultJwksEndpointHandler(
            ServiceProvider.GetRequiredService<ISecretKeyTypeCatalog>(),
            new FakeTimeProvider(Now)
        );
    }

    private DefaultJwksEndpointHandler Handler { get; }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync() => await ServiceProvider.DisposeAsync();

    #region HandleRouteAsync Tests

    [Fact]
    public async Task HandleRouteAsync_PublishesEveryKeyInItsValidityWindowAndOmitsAnExpiredOne()
    {
        // A verifier must hold a successor before the first token it signs and a retired key until the last token it
        // signed expires, so the upcoming and the retired key are published; an expired key verifies nothing a
        // relying party could still accept, so it is not.
        var upcoming = CreateKey("upcoming", activatesWhen: Now.AddDays(2));
        var active = CreateKey("active", activatesWhen: Now.AddDays(-30));
        var retired = CreateKey(
            "retired",
            activatesWhen: Now.AddDays(-120),
            retiresWhen: Now.AddDays(-1),
            expiresWhen: Now.AddDays(6)
        );
        var expired = CreateKey(
            "expired",
            activatesWhen: Now.AddDays(-200),
            retiresWhen: Now.AddDays(-110),
            expiresWhen: Now
        );
        var httpContext = CreateHttpContext([upcoming, active, retired, expired]);

        var result = await Handler.HandleRouteAsync(httpContext, CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<JsonWebKeySet>>(result);
        Assert.NotNull(json.Value);
        Assert.Equal(["upcoming", "active", "retired"], json.Value.Keys.Select(key => key.KeyId));
    }

    #endregion

    #region Helpers

    private RsaSecretKey CreateKey(
        string keyId,
        DateTimeOffset activatesWhen,
        DateTimeOffset? retiresWhen = null,
        DateTimeOffset? expiresWhen = null
    )
    {
        using var rsa = RSA.Create(2048);
        var metadata = new KeyMetadata
        {
            KeyId = keyId,
            Use = SecretKeyUses.Signature,
            Algorithm = "RS256",
            ActivatesWhen = activatesWhen,
            RetiresWhen = retiresWhen ?? activatesWhen.AddDays(90),
            ExpiresWhen = expiresWhen ?? (retiresWhen ?? activatesWhen.AddDays(90)).AddDays(7),
        };
        return ServiceProvider.GetRequiredService<ISecretKeyFactory>().CreateRsa(metadata, rsa);
    }

    private DefaultHttpContext CreateHttpContext(IReadOnlyList<SecretKey> keys)
    {
        var mockKeys = CreateLooseMock<ISecretKeyCollection>();
        mockKeys.Setup(x => x.GetEnumerator()).Returns(() => keys.GetEnumerator()).Verifiable();
        var mockSecretsProvider = CreateStrictMock<ISecretKeyCollectionProvider>();
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(mockKeys.Object).Verifiable();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant
            .SetupGet(x => x.SecretsProvider)
            .Returns(mockSecretsProvider.Object)
            .Verifiable();
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        mockEnvironment
            .SetupGet(x => x.JsonSerializerOptions)
            .Returns(JsonSerializerOptions.Default)
            .Verifiable();
        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object).Verifiable();

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == mockContext.Object)
        );
        return httpContext;
    }

    #endregion
}
