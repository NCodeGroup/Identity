// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Moq;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

public class SecretSerializerExtensionsTests : BaseTests
{
    private static PersistedSecret Secret { get; } =
        new PersistedSecretBuilder
        {
            ExpiresWhen = DateTimeOffset.UnixEpoch,
            RetiresWhen = DateTimeOffset.UnixEpoch,
            SecretType = SecretTypes.Rsa,
            SecretEncoding = SecretEncodings.Spki,
            KeySizeBits = 2048,
            EncodedValue = "AAAA",
        }.Build();

    public static TheoryData<Exception> ReadFailures =>
        new()
        {
            new ArgumentException("argument"),
            new InvalidOperationException("invalid operation"),
            new NotSupportedException("not supported"),
            new CryptographicException("cryptographic"),
            new FormatException("format"),
        };

    private Mock<ISecretSerializer> Serializer { get; }

    public SecretSerializerExtensionsTests()
    {
        Serializer = CreateStrictMock<ISecretSerializer>();
    }

    #region TryDeserializePublicSecret Tests

    [Fact]
    public void TryDeserializePublicSecret_WhenThePublicHalfIsRead_ReturnsIt()
    {
        SecretKey expected = new StubSecretKey();
        Serializer.Setup(x => x.DeserializePublicSecret(Secret)).Returns(expected).Verifiable();

        var result = Serializer.Object.TryDeserializePublicSecret(
            Secret,
            out var publicKey,
            out var error
        );

        Assert.True(result);
        Assert.Same(expected, publicKey);
        Assert.Null(error);
    }

    [Theory]
    [MemberData(nameof(ReadFailures))]
    public void TryDeserializePublicSecret_WhenThePublicHalfCannotBeRead_ReportsWhy(
        Exception failure
    )
    {
        Serializer.Setup(x => x.DeserializePublicSecret(Secret)).Throws(failure).Verifiable();

        var result = Serializer.Object.TryDeserializePublicSecret(
            Secret,
            out var publicKey,
            out var error
        );

        Assert.False(result);
        Assert.Null(publicKey);
        Assert.Equal(failure.Message, error);
    }

    [Fact]
    public void TryDeserializePublicSecret_WhenAnotherExceptionIsThrown_PropagatesIt()
    {
        Serializer
            .Setup(x => x.DeserializePublicSecret(Secret))
            .Throws(new OperationCanceledException())
            .Verifiable();

        Assert.Throws<OperationCanceledException>(() =>
            Serializer.Object.TryDeserializePublicSecret(Secret, out _, out _)
        );
    }

    [Fact]
    public void TryDeserializePublicSecret_WhenThePersistedSecretIsNull_Throws()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            Serializer.Object.TryDeserializePublicSecret(null!, out _, out _)
        );

        Assert.Equal("persistedSecret", exception.ParamName);
    }

    #endregion

    private sealed class StubSecretKey : SecretKey
    {
        public override string KeyType => "stub";

        public override KeyMetadata Metadata { get; } = new() { KeyId = "key-1" };

        public override int KeySizeBits => 0;
    }
}
