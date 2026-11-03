// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Buffers;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Verifiers;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.Server.Tests;

/// <summary>
/// Provides a base class for tests that need a real secret generator, serializer and protector provider.
/// </summary>
public abstract class SecretProtectionTestBase : BaseTests
{
    /// <summary>
    /// Gets the service providers that the test has built. They are disposed with the test.
    /// </summary>
    protected List<ServiceProvider> ServiceProviders { get; } = [];

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The providers own the master keys that the protectors use, so they are disposed before the mocks that
            // back them are verified.
            foreach (var serviceProvider in ServiceProviders)
            {
                serviceProvider.Dispose();
            }
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Builds a service provider with the real secret services whose master key store holds no key for the owner.
    /// </summary>
    /// <param name="owner">The owner whose first master key the ring creates when its protector is first requested.</param>
    /// <returns>The service provider, which is disposed with the test.</returns>
    protected ServiceProvider CreateSecretServices(SecretOwner owner)
    {
        // The members that take a span (ImportOneWaySharedSecret, Unprotect) cannot be set up on a Moq mock, so a test of code
        // that calls them uses the real services, built through the public registration. Only the master key store,
        // which would otherwise be the database, is a strict mock that expects the ring to create the first master key
        // of the owner and to save it.
        var store = CreateStrictMock<IMasterKeyStore>();
        store
            .Setup(x => x.FindActiveAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedMasterKey?>((PersistedMasterKey?)null))
            .Verifiable();
        store
            .Setup(x => x.GetLatestVersionAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(0))
            .Verifiable();
        store
            .Setup(x => x.AddAsync(It.IsAny<PersistedMasterKey>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var storeManager = CreateStrictMock<IStoreManager>();
        storeManager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(store.Object).Verifiable();
        storeManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        storeManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        var storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IStoreManager>(storeManager.Object))
            .Verifiable();

        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddPersistenceLibrary();
        serviceCollection.AddSingleton(storeManagerFactory.Object);
        serviceCollection.Configure<SecretProtectionOptions>(options =>
        {
            options.RootKeyProvider = KeyWrapperNames.Ephemeral;
            options.Recovery = new RecoveryKeyOptions { Disabled = true };
        });

        // A verifier records its own work factor, so a low one exercises the same path faster.
        serviceCollection.Configure<Pbkdf2SecretVerifierOptions>(options =>
            options.Iterations = 1_000
        );

        var serviceProvider = serviceCollection.BuildServiceProvider();
        ServiceProviders.Add(serviceProvider);
        return serviceProvider;
    }

    /// <summary>
    /// Gets the protector of the owner from the real services.
    /// </summary>
    /// <param name="services">The service provider that <see cref="CreateSecretServices"/> built.</param>
    /// <param name="owner">The owner whose protector is returned.</param>
    /// <returns>The protector of the owner's master key.</returns>
    protected static async Task<ISecretProtector> GetProtectorAsync(
        ServiceProvider services,
        SecretOwner owner
    )
    {
        var provider = services.GetRequiredService<ISecretProtectorProvider>();
        return await provider.GetProtectorAsync(owner, [], CancellationToken.None);
    }

    /// <summary>
    /// Asserts that a stored secret is the expected one, protected under the owner's master key: it is a
    /// <c>symmetric_raw</c> envelope, and the real serializer opens it with the owner's protector to the UTF-8 bytes of
    /// the expected text.
    /// </summary>
    /// <param name="services">The service provider that <see cref="CreateSecretServices"/> built.</param>
    /// <param name="owner">The owner whose protector opens the secret.</param>
    /// <param name="secret">The stored secret to assert on.</param>
    /// <param name="expectedSecretId">The id that the secret is expected to carry.</param>
    /// <param name="expectedSecretText">The text whose UTF-8 bytes the secret is expected to hold.</param>
    protected static async Task AssertSecretIsProtectedUnderTenantAsync(
        ServiceProvider services,
        SecretOwner owner,
        PersistedSecret secret,
        string expectedSecretId,
        string expectedSecretText
    )
    {
        Assert.Equal(expectedSecretId, secret.SecretId);
        Assert.Equal(SecretEncodings.SymmetricRaw, secret.SecretEncoding);
        Assert.StartsWith("nce1.", secret.EncodedValue, StringComparison.Ordinal);

        var protector = await GetProtectorAsync(services, owner);
        var serializer = services.GetRequiredService<ISecretSerializer>();
        var secretKey = serializer.DeserializeSecret(protector, secret);

        var symmetric = Assert.IsAssignableFrom<SymmetricSecretKey>(secretKey);
        var writer = new ArrayBufferWriter<byte>();
        symmetric.ExportPrivateKey(ref writer);
        Assert.Equal(
            SecureEncoding.UTF8.GetBytes(expectedSecretText),
            writer.WrittenSpan.ToArray()
        );
    }

    /// <summary>
    /// Asserts that a stored secret is the one-way verifier of the expected text, protected under the owner's master
    /// key: it is a <c>verifier</c> envelope that the real serializer opens with the owner's protector to a PBKDF2
    /// verifier that confirms the expected text and nothing else.
    /// </summary>
    /// <param name="services">The service provider that <see cref="CreateSecretServices"/> built.</param>
    /// <param name="owner">The owner whose protector opens the secret.</param>
    /// <param name="secret">The stored secret to assert on.</param>
    /// <param name="expectedSecretId">The id that the secret is expected to carry.</param>
    /// <param name="expectedSecretText">The text whose UTF-8 bytes the verifier is expected to confirm.</param>
    // See ADR-1391.
    protected static async Task AssertSecretIsVerifierUnderTenantAsync(
        ServiceProvider services,
        SecretOwner owner,
        PersistedSecret secret,
        string expectedSecretId,
        string expectedSecretText
    )
    {
        Assert.Equal(expectedSecretId, secret.SecretId);
        Assert.Equal(SecretEncodings.Verifier, secret.SecretEncoding);
        Assert.StartsWith("nce1.", secret.EncodedValue, StringComparison.Ordinal);

        var protector = await GetProtectorAsync(services, owner);
        var serializer = services.GetRequiredService<ISecretSerializer>();
        var verifier = Assert.Single(serializer.DeserializeVerifiers(protector, [secret]));

        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, verifier.Scheme);
        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync(
                SecureEncoding.UTF8.GetBytes(expectedSecretText),
                CancellationToken.None
            )
        );
        Assert.Equal(
            SecretVerificationResult.Failed,
            await verifier.VerifyAsync(
                SecureEncoding.UTF8.GetBytes(expectedSecretText + "x"),
                CancellationToken.None
            )
        );
    }
}
