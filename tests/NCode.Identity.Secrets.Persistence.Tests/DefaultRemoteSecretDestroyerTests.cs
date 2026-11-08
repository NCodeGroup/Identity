// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Persistence;

public class DefaultRemoteSecretDestroyerTests : IDisposable
{
    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IRemoteKeyProvider> _provider;
    private readonly Mock<ILogger<DefaultRemoteSecretDestroyer>> _logger = new(MockBehavior.Loose);
    private readonly DefaultRemoteSecretDestroyer _destroyer;

    public DefaultRemoteSecretDestroyerTests()
    {
        _provider = _mocks.Create<IRemoteKeyProvider>();
        _provider.SetupGet(x => x.Name).Returns(RemoteKeyProviderNames.AwsKms).Verifiable();

        // The logger is loose, and every test that expects an entry verifies it by event identifier.
        _logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        _destroyer = new DefaultRemoteSecretDestroyer(
            new DefaultRemoteKeyProviderCatalog([_provider.Object], []),
            new FakeTimeProvider(),
            _logger.Object
        );
    }

    public void Dispose()
    {
        _mocks.Verify();
        GC.SuppressFinalize(this);
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

    private static async ValueTask FailAsync()
    {
        await Task.Yield();
        throw new InvalidOperationException("kms is down");
    }

    private static string Reference(string provider, string keyId) =>
        new RemoteKeyReference(provider, keyId).ToString();

    private void ExpectLog(int eventId, Type? exceptionType)
    {
        _logger
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

    #region DestroyAsync

    [Fact]
    public async Task DestroyAsync_RemoteGenerated_DestroysTheKey()
    {
        _provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());

        await _destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AwsKms, "key-a")),
        ]);
    }

    [Fact]
    public async Task DestroyAsync_NoSecrets_CompletesWithoutCallingTheProvider()
    {
        await _destroyer.DestroyAsync([]);
    }

    [Fact]
    public async Task DestroyAsync_NotRemoteOrNotGenerated_SkipsTheSecret()
    {
        var reference = Reference(RemoteKeyProviderNames.AwsKms, "key-a");

        await _destroyer.DestroyAsync([
            CreateSecret("secret-local", reference, SecretEncodings.Pkcs8),
            CreateSecret("secret-public", reference, SecretEncodings.Spki),
            CreateSecret("secret-supplied", reference, origin: SecretOrigins.Supplied),
        ]);

        _provider.Verify(
            x => x.DestroyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never()
        );
    }

    [Fact]
    public async Task DestroyAsync_DestroyFails_LogsAndContinuesWithTheNextSecret()
    {
        _provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Returns(() => FailAsync())
            .Verifiable(Times.Once());
        _provider
            .Setup(x => x.DestroyKeyAsync("key-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroyFailed, typeof(InvalidOperationException));

        await _destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AwsKms, "key-a")),
            CreateSecret("secret-b", Reference(RemoteKeyProviderNames.AwsKms, "key-b")),
        ]);

        _logger.Verify();
    }

    [Fact]
    public async Task DestroyAsync_DestroyThrowsSynchronously_LogsAndDoesNotThrow()
    {
        _provider
            .Setup(x => x.DestroyKeyAsync("key-a", It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("kms is down"))
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroyFailed, typeof(InvalidOperationException));

        await _destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AwsKms, "key-a")),
        ]);

        _logger.Verify();
    }

    [Fact]
    public async Task DestroyAsync_MalformedReference_LogsAndContinuesWithTheNextSecret()
    {
        _provider
            .Setup(x => x.DestroyKeyAsync("key-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroySkipped, null);

        await _destroyer.DestroyAsync([
            CreateSecret("secret-a", "not-a-reference"),
            CreateSecret("secret-b", Reference(RemoteKeyProviderNames.AwsKms, "key-b")),
        ]);

        _logger.Verify();
    }

    [Fact]
    public async Task DestroyAsync_UnregisteredProvider_LogsAndContinuesWithTheNextSecret()
    {
        _provider
            .Setup(x => x.DestroyKeyAsync("key-b", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
        ExpectLog(EventIds.RemoteKeyDestroySkipped, null);

        await _destroyer.DestroyAsync([
            CreateSecret("secret-a", Reference(RemoteKeyProviderNames.AzureKeyVault, "key-a")),
            CreateSecret("secret-b", Reference(RemoteKeyProviderNames.AwsKms, "key-b")),
        ]);

        _logger.Verify();
    }

    #endregion
}
