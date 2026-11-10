// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Keys;
using NCode.Identity.Cose.Options;
using NCode.Identity.Jose;
using NCode.Identity.Secrets;

namespace NCode.Identity.Cose.Tests;

public class DefaultRegistrationTests
{
    #region AddCoseLibrary Tests

    [Fact]
    public async Task AddCoseLibrary_Registered_ResolvesEveryService()
    {
        await using var host = new CoseTestHost();

        Assert.NotNull(host.Serializer);
        Assert.NotNull(host.KeyConverter);
        Assert.NotNull(host.Algorithms);
        Assert.NotNull(host.Services.GetRequiredService<ICoseKeyTypeCatalog>());
        Assert.NotNull(host.Services.GetRequiredService<IOptions<CoseOptions>>().Value);
    }

    [Fact]
    public async Task AddCoseLibrary_DefaultAlgorithms_ExcludeTheOptInDescriptors()
    {
        await using var host = new CoseTestHost();

        Assert.False(host.Algorithms.TryGet(CoseAlgorithmIdentifiers.HmacSha256Truncated64, out _));
        Assert.False(host.Algorithms.TryGet(CoseAlgorithmIdentifiers.RsaesOaepSha1, out _));
        Assert.False(host.Algorithms.TryGet(CoseAlgorithmIdentifiers.Rs256, out _));
        Assert.True(host.Algorithms.TryGet(CoseAlgorithmIdentifiers.Ed25519, out _));
    }

    [Fact]
    public async Task AddCoseLibrary_InvalidOptions_FailsValidation()
    {
        await using var host = new CoseTestHost(options => options.MaxRecipients = 0);

        Assert.Throws<OptionsValidationException>(() =>
            host.Services.GetRequiredService<IOptions<CoseOptions>>().Value
        );
    }

    #endregion

    #region AddCoseAlgorithm Tests

    [Fact]
    public async Task AddCoseAlgorithm_DefaultDescriptor_IsRegisteredOnce()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        var cose = serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddCoseLibrary();
        cose.AddCoseAlgorithm(CoseAlgorithms.Es256);
        await using var provider = serviceCollection.BuildServiceProvider();

        var descriptors = provider.GetServices<CoseAlgorithm>().ToList();

        Assert.Equal(descriptors.Count, descriptors.Distinct().Count());
        Assert.Single(descriptors, CoseAlgorithms.Es256);
        Assert.Single(provider.GetServices<ICoseSerializer>());
    }

    [Fact]
    public async Task AddCoseAlgorithm_TruncatedHmac_MakesItAvailable()
    {
        await using var host = new CoseTestHost(
            extraAlgorithms: [CoseAlgorithms.HmacSha256Truncated64]
        );

        Assert.True(host.Algorithms.TryGet(CoseAlgorithmIdentifiers.HmacSha256Truncated64, out _));
    }

    [Fact]
    public void AddCoseAlgorithm_NullAlgorithm_Throws()
    {
        var serviceCollection = new ServiceCollection();
        var cose = serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddCoseLibrary();

        Assert.Throws<ArgumentNullException>(() => cose.AddCoseAlgorithm(null!));
    }

    #endregion
}
