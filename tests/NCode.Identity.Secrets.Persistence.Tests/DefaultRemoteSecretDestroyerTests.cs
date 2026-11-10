// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultRemoteSecretDestroyerTests : BaseTests
{
    private Mock<IRemoteKeyProvider> Provider { get; }
    private Mock<ILogger<DefaultRemoteSecretDestroyer>> Logger { get; }
    private DefaultRemoteSecretDestroyer Destroyer { get; }

    public DefaultRemoteSecretDestroyerTests()
    {
        Provider = CreateStrictMock<IRemoteKeyProvider>();
        Logger = CreateStrictMock<ILogger<DefaultRemoteSecretDestroyer>>();
        Provider.SetupGet(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();

        var source = CreateStrictMock<IRemoteKeyDescriptorSource>();
        source
            .SetupGet(x => x.Providers)
            .Returns([new RemoteKeyProviderDescriptor { Name = RemoteKeyProviderNames.AwsKms }])
            .Verifiable();
        source.SetupGet(x => x.Algorithms).Returns([]).Verifiable();

        Destroyer = new DefaultRemoteSecretDestroyer(
            new DefaultRemoteKeyProviderCatalog(
                [Provider.Object],
                new DefaultRemoteKeyDescriptorCatalog([source.Object])
            ),
            new FakeTimeProvider(),
            Logger.Object
        );
    }

    private static PersistedSecret CreateSecret(
        string secretId,
        string encodedValue,
        string secretEncoding = SecretEncodings.Remote,
        string origin = SecretOrigins.Generated
    ) =>
        new()
        {
            SecretId = secretId,
            Use = SecretKeyUses.Signature,
            Algorithm = "RS256",
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ActivatesWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch,
            SecretType = SecretTypes.Rsa,
            SecretEncoding = secretEncoding,
            Origin = origin,
            KeySizeBits = 2048,
            EncodedValue = encodedValue,
        };

    private static async ValueTask FailAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        throw new InvalidOperationException("kms is down");
    }

    private static string Reference(string provider, string keyId) =>
        new RemoteKeyReference(provider, keyId).ToString();

    private void ExpectLog(int eventId, Type? exceptionType)
    {
        Logger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        Logger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(id => id.Id == eventId),
                    It.IsAny<It.IsAnyType>(),
                    It.Is<Exception?>(exception =>
                        exceptionType == null
                            ? exception == null
                            : exception != null && exception.GetType() == exceptionType
                    ),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()
                )
            )
            .Verifiable(Times.Once());
    }

    private void ExpectErrorLog(int eventId, Type exceptionType)
    {
        Logger.Setup(x => x.IsEnabled(LogLevel.Error)).Returns(true).Verifiable();
        Logger
            .Setup(x =>
                x.Log(
                    LogLevel.Error,
                    It.Is<EventId>(id => id.Id == eventId),
                    It.IsAny<It.IsAnyType>(),
                    It.Is<Exception?>(exception =>
                        exception != null && exception.GetType() == exceptionType
                    ),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()
                )
            )
            .Verifiable(Times.Once());
    }

    #region DestroyAsync

    [Fact]
    public async Task DestroyAsync_RemoteGenerated_DestroysTheKey()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());

        await Destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AwsKms, "key-a")),
        ]);
    }

    [Fact]
    public async Task DestroyAsync_NoSecrets_CompletesWithoutCallingTheProvider()
    {
        await Destroyer.DestroyAsync([]);
    }

    [Fact]
    public async Task DestroyAsync_NotRemoteOrNotGenerated_SkipsTheSecret()
    {
        var reference = Reference(RemoteKeyProviderNames.AwsKms, "key-a");

        Provider
            .Setup(x => x.DestroyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Never());

        await Destroyer.DestroyAsync([
            CreateSecret("secret-local", reference, SecretEncodings.Pkcs8),
            CreateSecret("secret-public", reference, SecretEncodings.Spki),
            CreateSecret("secret-supplied", reference, origin: SecretOrigins.Supplied),
        ]);
    }

    [Fact]
    public async Task DestroyAsync_DestroyFails_LogsAndContinuesWithTheNextSecret()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Returns(
                (string _, CancellationToken cancellationToken) => FailAsync(cancellationToken)
            )
            .Verifiable(Times.Once());
        Provider
            .Setup(x => x.DestroyKeyAsync("key-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroyFailed, typeof(InvalidOperationException));

        await Destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AwsKms, "key-a")),
            CreateSecret("secret-b", Reference(RemoteKeyProviderNames.AwsKms, "key-b")),
        ]);
    }

    // A provider may throw before it returns its task, and this test covers that path on purpose.
    [Fact]
    public async Task DestroyAsync_DestroyThrowsSynchronously_LogsAndDoesNotThrow()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("kms is down"))
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroyFailed, typeof(InvalidOperationException));

        await Destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AwsKms, "key-a")),
        ]);
    }

    [Fact]
    public async Task DestroyAsync_MalformedReference_LogsAndContinuesWithTheNextSecret()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync("key-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroySkipped, null);

        await Destroyer.DestroyAsync([
            CreateSecret("secret-a", "not-a-reference"),
            CreateSecret("secret-b", Reference(RemoteKeyProviderNames.AwsKms, "key-b")),
        ]);
    }

    [Fact]
    public async Task DestroyAsync_UnregisteredProvider_LogsAndContinuesWithTheNextSecret()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync("key-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroySkipped, null);

        await Destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AzureKeyVault, "key-a")),
            CreateSecret("secret-b", Reference(RemoteKeyProviderNames.AwsKms, "key-b")),
        ]);
    }

    #endregion

    #region DestroyKeyAsync

    [Fact]
    public async Task DestroyKeyAsync_RegisteredProvider_DestroysAndReturnsTrue()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());

        var destroyed = await Destroyer.DestroyKeyAsync(
            new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "key-a")
        );

        Assert.True(destroyed);
    }

    [Fact]
    public async Task DestroyKeyAsync_UnregisteredProvider_ReturnsFalse()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Never());
        ExpectLog(EventIds.RemoteKeyOrphanDestroySkipped, null);

        var destroyed = await Destroyer.DestroyKeyAsync(
            new RemoteKeyReference(RemoteKeyProviderNames.AzureKeyVault, "key-a")
        );

        Assert.False(destroyed);
    }

    [Fact]
    public async Task DestroyKeyAsync_ProviderFails_ReturnsFalse()
    {
        Provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Returns(
                (string _, CancellationToken cancellationToken) => FailAsync(cancellationToken)
            )
            .Verifiable(Times.Once());
        ExpectErrorLog(EventIds.RemoteKeyOrphanDestroyFailed, typeof(InvalidOperationException));

        var destroyed = await Destroyer.DestroyKeyAsync(
            new RemoteKeyReference(RemoteKeyProviderNames.AwsKms, "key-a")
        );

        Assert.False(destroyed);
    }

    #endregion
}
