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

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Moq;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class MasterKeyFactoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 12, 0, 0, TimeSpan.Zero);

    // The mixed case proves that the row keeps the owner as given while the key and the key check normalize it.
    private static readonly SecretOwner Owner = SecretOwner.ForTenant("Tenant-A");

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IRootKeyProviderCatalog> _catalog;
    private readonly Mock<TimeProvider> _timeProvider;
    private readonly EphemeralRootKeyProvider _provider = new();
    private readonly RecoveryKeyWrapper _disabledRecovery = new(
        Microsoft.Extensions.Options.Options.Create(new SecretProtectionOptions())
    );

    public MasterKeyFactoryTests()
    {
        _catalog = _mocks.Create<IRootKeyProviderCatalog>();
        _timeProvider = _mocks.Create<TimeProvider>();
    }

    public void Dispose()
    {
        _disabledRecovery.Dispose();
        _provider.Dispose();
        _mocks.Verify();
    }

    private MasterKeyFactory CreateFactory(IRecoveryKeyWrapper recovery) =>
        new(_catalog.Object, recovery, _timeProvider.Object);

    // Reports a wrapped length of eight characters but writes seven, so the factory must reject it.
    private sealed class ShortRecoveryKeyWrapper : IRecoveryKeyWrapper
    {
        public string? RecoveryKeyId => "short";

        public int GetWrappedLength() => 8;

        public void Wrap<TWriter>(ReadOnlySpan<byte> masterKey, ref TWriter destination)
            where TWriter : IBufferWriter<char>, allows ref struct
        {
            "1234567".CopyTo(destination.GetSpan(7));
            destination.Advance(7);
        }
    }

    // CreateAsync selects the configured provider and reads the clock.
    private void ExpectCreate()
    {
        _catalog.Setup(x => x.GetConfigured()).Returns(_provider).Verifiable();
        _timeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
    }

    // UnwrapAsync looks the provider up by the name that the row records.
    private void ExpectGet(IKeyWrapper provider) =>
        _catalog.Setup(x => x.Get(KeyWrapperNames.Ephemeral)).Returns(provider).Verifiable();

    private static PersistedMasterKey CopyRow(
        PersistedMasterKey row,
        string? ownerId = null,
        int? version = null,
        string? keyCheck = null
    ) =>
        new()
        {
            KeyId = row.KeyId,
            OwnerType = row.OwnerType,
            OwnerId = ownerId ?? row.OwnerId,
            Version = version ?? row.Version,
            CreatedWhen = row.CreatedWhen,
            RootKeyProvider = row.RootKeyProvider,
            RootKeyId = row.RootKeyId,
            RootWrappedKey = row.RootWrappedKey,
            KeyCheck = keyCheck ?? row.KeyCheck,
        };

    private static byte[] CreateRootContext(Guid keyId, SecretOwner owner)
    {
        var context = new byte[SecretProtectionFormat.GetRootContextSize(owner)];
        var written = SecretProtectionFormat.WriteRootContext(keyId, owner, context);

        Assert.Equal(context.Length, written);
        return context;
    }

    private static string ComputeKeyCheck(
        ReadOnlySpan<byte> material,
        Guid keyId,
        SecretOwner owner,
        int version
    )
    {
        var keyCheck = new byte[SecretProtectionFormat.KeyCheckSizeBytes];
        SecretProtectionFormat.ComputeKeyCheck(material, keyId, owner, version, keyCheck);

        return Base64Url.EncodeToString(keyCheck);
    }

    #region CreateAsync Tests

    [Fact]
    public async Task CreateThenUnwrap_RoundTripsMaterial()
    {
        ExpectCreate();
        ExpectGet(_provider);
        var factory = CreateFactory(_disabledRecovery);

        var created = await factory.CreateAsync(Owner, version: 1, CancellationToken.None);
        using var createdKey = created.Key;
        using var unwrapped = await factory.UnwrapAsync(created.Row, CancellationToken.None);

        // A key that is all zeros would also round-trip, so the check against zeros proves that zeroing the temporary
        // material did not touch the key.
        Assert.NotEqual(
            new byte[SecretProtectionFormat.MasterKeySizeBytes],
            createdKey.Material.ToArray()
        );
        Assert.Equal(createdKey.Material.ToArray(), unwrapped.Material.ToArray());
        Assert.Equal(created.Row.KeyId, unwrapped.KeyId);
        Assert.Equal(Owner.Normalize(), unwrapped.Owner);
        Assert.Equal(1, unwrapped.Version);
    }

    [Fact]
    public async Task Create_MapsOwnerVersionAndTimes()
    {
        ExpectCreate();
        var factory = CreateFactory(_disabledRecovery);

        var created = await factory.CreateAsync(Owner, version: 3, CancellationToken.None);
        using var key = created.Key;
        var row = created.Row;

        Assert.Equal(key.KeyId, row.KeyId);
        Assert.Equal(Owner.OwnerType, row.OwnerType);
        Assert.Equal(Owner.OwnerId, row.OwnerId);
        Assert.Equal(3, row.Version);
        Assert.Equal(Now, row.CreatedWhen);
        Assert.Null(row.RetiredWhen);
        Assert.Equal(ComputeKeyCheck(key.Material, row.KeyId, Owner, version: 3), row.KeyCheck);
    }

    [Fact]
    public async Task Create_RecordsProviderAndRecovery()
    {
        using var recoveryKey = RSA.Create(3072);
        using var recovery = RecoveryKeyWrapper.FromPem(
            recoveryKey.ExportSubjectPublicKeyInfoPem()
        );
        ExpectCreate();
        var factory = CreateFactory(recovery);

        var created = await factory.CreateAsync(Owner, version: 1, CancellationToken.None);
        using var key = created.Key;
        var row = created.Row;
        var context = CreateRootContext(row.KeyId, Owner);
        var probe = await _provider.WrapAsync(
            new byte[SecretProtectionFormat.MasterKeySizeBytes],
            context,
            CancellationToken.None
        );
        var rootDestination = new byte[SecretProtectionFormat.MasterKeySizeBytes];
        var rootWritten = await _provider.UnwrapAsync(
            new WrappedKey
            {
                WrapperName = row.RootKeyProvider,
                KeyId = row.RootKeyId,
                Ciphertext = row.RootWrappedKey,
            },
            context,
            rootDestination,
            CancellationToken.None
        );

        Assert.Equal(KeyWrapperNames.Ephemeral, row.RootKeyProvider);
        Assert.Equal(probe.KeyId, row.RootKeyId);
        Assert.Equal(SecretProtectionFormat.MasterKeySizeBytes, rootWritten);
        Assert.Equal(key.Material.ToArray(), rootDestination);
        Assert.Equal(recovery.RecoveryKeyId, row.RecoveryKeyId);
        Assert.NotNull(row.RecoveryWrappedKey);
        var recoveryUnwrapped = recoveryKey.Decrypt(
            Base64Url.DecodeFromChars(row.RecoveryWrappedKey),
            RSAEncryptionPadding.OaepSHA256
        );
        Assert.Equal(key.Material.ToArray(), recoveryUnwrapped);
    }

    [Fact]
    public async Task Create_RecoveryDisabled_LeavesRecoveryNull()
    {
        ExpectCreate();
        var factory = CreateFactory(_disabledRecovery);

        var created = await factory.CreateAsync(Owner, version: 1, CancellationToken.None);
        using var key = created.Key;

        Assert.Null(created.Row.RecoveryKeyId);
        Assert.Null(created.Row.RecoveryWrappedKey);
    }

    [Fact]
    public async Task Create_RecoveryWrapperWritesFewerCharacters_Throws()
    {
        ExpectCreate();
        var factory = CreateFactory(new ShortRecoveryKeyWrapper());

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await factory.CreateAsync(Owner, version: 1, CancellationToken.None)
        );
    }

    #endregion

    #region UnwrapAsync Tests

    [Fact]
    public async Task Unwrap_KeyCheckForOtherOwner_Throws()
    {
        ExpectCreate();
        var factory = CreateFactory(_disabledRecovery);
        var created = await factory.CreateAsync(Owner, version: 1, CancellationToken.None);
        using var key = created.Key;
        var otherRow = CopyRow(created.Row, ownerId: "other-tenant");

        // The provider ignores the context of the other owner, so that the key check is the only defense that remains.
        var originalContext = CreateRootContext(created.Row.KeyId, Owner);
        var provider = _mocks.Create<IKeyWrapper>();
        provider
            .Setup(x =>
                x.UnwrapAsync(
                    It.IsAny<WrappedKey>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<Memory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    WrappedKey wrapped,
                    ReadOnlyMemory<byte> _,
                    Memory<byte> destination,
                    CancellationToken token
                ) => _provider.UnwrapAsync(wrapped, originalContext, destination, token)
            )
            .Verifiable();
        ExpectGet(provider.Object);

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await factory.UnwrapAsync(otherRow, CancellationToken.None)
        );

        Assert.Contains("key-check", exception.Message);
        Assert.Contains(created.Row.KeyId.ToString(), exception.Message);
        Assert.Contains("other-tenant", exception.Message);
    }

    [Fact]
    public async Task Unwrap_RootContextForOtherOwner_Throws()
    {
        ExpectCreate();
        ExpectGet(_provider);
        var factory = CreateFactory(_disabledRecovery);
        var created = await factory.CreateAsync(Owner, version: 1, CancellationToken.None);
        using var key = created.Key;
        var otherRow = CopyRow(created.Row, ownerId: "other-tenant");

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await factory.UnwrapAsync(otherRow, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Unwrap_OtherVersion_Throws()
    {
        ExpectCreate();
        ExpectGet(_provider);
        var factory = CreateFactory(_disabledRecovery);
        var created = await factory.CreateAsync(Owner, version: 1, CancellationToken.None);
        using var key = created.Key;
        var otherRow = CopyRow(created.Row, version: 2);

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await factory.UnwrapAsync(otherRow, CancellationToken.None)
        );

        Assert.Contains("key-check", exception.Message);
        Assert.Contains(created.Row.KeyId.ToString(), exception.Message);
        Assert.Contains(Owner.ToString(), exception.Message);
    }

    [Fact]
    public async Task Unwrap_WrongKeyLength_Throws()
    {
        var row = new PersistedMasterKey
        {
            KeyId = Guid.NewGuid(),
            OwnerType = Owner.OwnerType,
            OwnerId = Owner.OwnerId,
            Version = 1,
            CreatedWhen = Now,
            RootKeyProvider = KeyWrapperNames.Ephemeral,
            RootKeyId = "root-key-id",
            RootWrappedKey = "root-wrapped-key",
            KeyCheck = "key-check",
        };
        var expectedContext = CreateRootContext(row.KeyId, Owner);
        var material = RandomNumberGenerator.GetBytes(
            SecretProtectionFormat.MasterKeySizeBytes - 1
        );
        var provider = _mocks.Create<IKeyWrapper>();
        provider
            .Setup(x =>
                x.UnwrapAsync(
                    It.Is<WrappedKey>(wrapped =>
                        wrapped.WrapperName == KeyWrapperNames.Ephemeral
                        && wrapped.KeyId == "root-key-id"
                        && wrapped.Ciphertext == "root-wrapped-key"
                    ),
                    It.Is<ReadOnlyMemory<byte>>(context =>
                        Enumerable.SequenceEqual(context.ToArray(), expectedContext)
                    ),
                    It.IsAny<Memory<byte>>(),
                    CancellationToken.None
                )
            )
            .Returns(
                (
                    WrappedKey _,
                    ReadOnlyMemory<byte> _,
                    Memory<byte> destination,
                    CancellationToken _
                ) =>
                {
                    material.CopyTo(destination);
                    return ValueTask.FromResult(material.Length);
                }
            )
            .Verifiable();
        ExpectGet(provider.Object);
        var factory = CreateFactory(_disabledRecovery);

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await factory.UnwrapAsync(row, CancellationToken.None)
        );

        Assert.Contains(row.KeyId.ToString(), exception.Message);
    }

    [Fact]
    public async Task Unwrap_OverlongKey_ThrowsCryptographicException()
    {
        var row = new PersistedMasterKey
        {
            KeyId = Guid.NewGuid(),
            OwnerType = Owner.OwnerType,
            OwnerId = Owner.OwnerId,
            Version = 1,
            CreatedWhen = Now,
            RootKeyProvider = KeyWrapperNames.Ephemeral,
            RootKeyId = "root-key-id",
            RootWrappedKey = "root-wrapped-key",
            KeyCheck = "key-check",
        };
        var material = RandomNumberGenerator.GetBytes(
            SecretProtectionFormat.MasterKeySizeBytes + 1
        );
        var provider = _mocks.Create<IKeyWrapper>();
        provider
            .Setup(x =>
                x.UnwrapAsync(
                    It.IsAny<WrappedKey>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<Memory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    WrappedKey _,
                    ReadOnlyMemory<byte> _,
                    Memory<byte> destination,
                    CancellationToken _
                ) =>
                {
                    material.CopyTo(destination);
                    return ValueTask.FromResult(material.Length);
                }
            )
            .Verifiable();
        ExpectGet(provider.Object);
        var factory = CreateFactory(_disabledRecovery);

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await factory.UnwrapAsync(row, CancellationToken.None)
        );

        Assert.Contains(row.KeyId.ToString(), exception.Message);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AAAA")]
    [InlineData("!!!!!!!!!!" + "!!!!!!!!!!" + "!!!!!!!!!!" + "!!!!!!!!!!" + "!!!")]
    public async Task Unwrap_MalformedKeyCheck_Throws(string keyCheck)
    {
        ExpectCreate();
        ExpectGet(_provider);
        var factory = CreateFactory(_disabledRecovery);
        var created = await factory.CreateAsync(Owner, version: 1, CancellationToken.None);
        using var key = created.Key;
        var otherRow = CopyRow(created.Row, keyCheck: keyCheck);

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await factory.UnwrapAsync(otherRow, CancellationToken.None)
        );

        Assert.Contains("key-check", exception.Message);
    }

    #endregion
}
