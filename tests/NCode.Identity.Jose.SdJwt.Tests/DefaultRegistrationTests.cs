// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Issuance;
using NCode.Identity.Jose.SdJwt.Options;
using NCode.Identity.Jose.SdJwt.Presentation;
using NCode.Identity.Jose.SdJwt.Verification;
using NCode.Identity.Jwt;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Registration;

namespace NCode.Identity.Jose.SdJwt.Tests;

public class DefaultRegistrationTests
{
    #region AddSdJwt Tests

    [Fact]
    public async Task AddSdJwt_RegistersTheServicesAndTheBuiltInAlgorithms()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddSdJwt();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        Assert.NotNull(serviceProvider.GetRequiredService<ISdJwtIssuer>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISdJwtHolder>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISdJwtVerifier>());
        Assert.Single(
            serviceProvider.GetRequiredService<IEnumerable<IConfirmationKeyResolver>>(),
            x => x.Member == "jwk"
        );

        var catalog = serviceProvider.GetRequiredService<ISdJwtDigestAlgorithmCatalog>();
        Assert.True(catalog.TryGet("sha-256", out _));
        Assert.True(catalog.TryGet("sha-384", out _));
        Assert.True(catalog.TryGet("sha-512", out _));
        Assert.Equal(3, catalog.Algorithms.Count);
    }

    [Fact]
    public void AddSdJwt_WhenCalledTwice_RegistersEachAlgorithmOnce()
    {
        var serviceCollection = new ServiceCollection();
        var jwt = serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();

        jwt.AddSdJwt();
        jwt.AddSdJwt();

        Assert.Equal(
            3,
            serviceCollection.Count(x => x.ServiceType == typeof(SdJwtDigestAlgorithm))
        );
        Assert.Single(serviceCollection, x => x.ServiceType == typeof(ISdJwtVerifier));
    }

    [Fact]
    public async Task AddSdJwt_WithOptionsBelowTheMinimum_FailsValidation()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddSdJwt(options => options.SaltSizeBytes = 8);
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var accessor = serviceProvider.GetRequiredService<IOptions<SdJwtOptions>>();

        Assert.Throws<OptionsValidationException>(() => accessor.Value);
    }

    [Fact]
    public async Task AddSdJwt_WithAnotherAlgorithmRegistered_ServesIt()
    {
        var custom = new SdJwtDigestAlgorithm
        {
            Name = "sha3-256",
            HashAlgorithmName = System.Security.Cryptography.HashAlgorithmName.SHA256,
        };
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.TryAddEnumerableInstance(custom);
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddSdJwt();
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var catalog = serviceProvider.GetRequiredService<ISdJwtDigestAlgorithmCatalog>();

        Assert.True(catalog.TryGet("sha3-256", out var algorithm));
        Assert.Same(custom, algorithm);
    }

    [Fact]
    public async Task AddSdJwt_WithASecondResolverForAMember_FailsToComposeTheVerifier()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddSdJwt();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.Singleton<IConfirmationKeyResolver, SecondJwkResolver>()
        );
        await using var serviceProvider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            serviceProvider.GetRequiredService<ISdJwtVerifier>()
        );

        Assert.Contains("'jwk'", exception.Message, StringComparison.Ordinal);
    }

    private sealed class SecondJwkResolver : IConfirmationKeyResolver
    {
        public string Member => "jwk";

        public ValueTask<SecretKey?> ResolveAsync(
            JsonElement confirmation,
            CancellationToken cancellationToken
        ) => ValueTask.FromResult<SecretKey?>(null);
    }

    #endregion
}
