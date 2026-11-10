// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Keys;
using NCode.Identity.Cose.Options;
using NCode.Identity.Cose.Randomness;
using NCode.Identity.Jose;
using NCode.Identity.Jose.EdDsa;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Cose.Tests;

/// <summary>
/// The real composition the COSE tests run against: the Secrets, JOSE, EdDSA and COSE libraries over a fake clock.
/// </summary>
internal sealed class CoseTestHost : IAsyncDisposable
{
    public static DateTimeOffset StartTime { get; } = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    public CoseTestHost(
        Action<CoseOptions>? configureOptions = null,
        QueueRandomSource? randomSource = null,
        IEnumerable<CoseAlgorithm>? extraAlgorithms = null
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<TimeProvider>(TimeProvider);
        if (randomSource is not null)
            serviceCollection.AddSingleton<ICoseRandomSource>(randomSource);

        var jose = serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddEdDsa();
        var cose = configureOptions is null
            ? jose.AddCoseLibrary()
            : jose.AddCoseLibrary(configureOptions);
        foreach (var algorithm in extraAlgorithms ?? [])
            cose.AddCoseAlgorithm(algorithm);

        Services = serviceCollection.BuildServiceProvider();
    }

    public FakeTimeProvider TimeProvider { get; } = new(StartTime);

    public ServiceProvider Services { get; }

    public ICoseSerializer Serializer => Services.GetRequiredService<ICoseSerializer>();

    public ICoseKeyConverter KeyConverter => Services.GetRequiredService<ICoseKeyConverter>();

    public ICoseAlgorithmCatalog Algorithms => Services.GetRequiredService<ICoseAlgorithmCatalog>();

    public ISecretKeyFactory KeyFactory => Services.GetRequiredService<ISecretKeyFactory>();

    public ISecretKeyTypeCatalog SecretKeyTypes =>
        Services.GetRequiredService<ISecretKeyTypeCatalog>();

    public ValueTask DisposeAsync() => Services.DisposeAsync();
}
