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
using NCode.Buffers;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class RecoveryKeyWrapperTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"{nameof(RecoveryKeyWrapperTests)}-{Guid.NewGuid():N}"
    );

    public RecoveryKeyWrapperTests()
    {
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static byte[] CreateMasterKey() =>
        RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes);

    private static RecoveryKeyWrapper CreateWrapper(RecoveryKeyOptions recovery) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { Recovery = recovery }
            )
        );

    // Wraps the master key into a writer and asserts that the wrapper writes the length that it reports.
    private static string Wrap(RecoveryKeyWrapper wrapper, byte[] masterKey)
    {
        var writer = new ArrayBufferWriter<char>();
        wrapper.Wrap(masterKey, ref writer);

        Assert.Equal(wrapper.GetWrappedLength(), writer.WrittenCount);
        return new string(writer.WrittenSpan);
    }

    private string WritePemFile(string pem)
    {
        var path = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, pem, SecureEncoding.UTF8);
        return path;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_DisabledAndPath_Throws()
    {
        var recovery = new RecoveryKeyOptions
        {
            PublicKeyPath = Path.Combine(_tempDirectory, "missing.pem"),
            Disabled = true,
        };

        var exception = Assert.Throws<InvalidOperationException>(() => CreateWrapper(recovery));

        Assert.Contains("SecretProtection:Recovery", exception.Message);
    }

    [Fact]
    public void Constructor_Disabled_WrapThrows()
    {
        using var wrapper = CreateWrapper(new RecoveryKeyOptions { Disabled = true });

        Assert.Throws<InvalidOperationException>(() => Wrap(wrapper, CreateMasterKey()));
        Assert.Equal(0, wrapper.GetWrappedLength());
        Assert.Null(wrapper.RecoveryKeyId);
    }

    [Fact]
    public void Constructor_NothingConfigured_WrapThrows()
    {
        using var wrapper = CreateWrapper(new RecoveryKeyOptions());

        Assert.Throws<InvalidOperationException>(() => Wrap(wrapper, CreateMasterKey()));
        Assert.Equal(0, wrapper.GetWrappedLength());
        Assert.Null(wrapper.RecoveryKeyId);
    }

    [Fact]
    public void Constructor_PublicKeyPath_LoadsKey()
    {
        using var privateKey = RSA.Create(3072);
        var path = WritePemFile(privateKey.ExportSubjectPublicKeyInfoPem());
        using var wrapper = CreateWrapper(new RecoveryKeyOptions { PublicKeyPath = path });
        var masterKey = CreateMasterKey();

        var result = Wrap(wrapper, masterKey);

        var unwrapped = privateKey.Decrypt(
            Base64Url.DecodeFromChars(result),
            RSAEncryptionPadding.OaepSHA256
        );
        Assert.Equal(masterKey, unwrapped);
    }

    [Fact]
    public void Constructor_PublicKeyPathMissing_Throws()
    {
        var path = Path.Combine(_tempDirectory, "missing.pem");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CreateWrapper(new RecoveryKeyOptions { PublicKeyPath = path })
        );

        Assert.Contains(path, exception.Message);
    }

    #endregion

    #region FromPem Tests

    [Fact]
    public void FromPem_Rsa2048_Throws()
    {
        using var weakKey = RSA.Create(2048);

        Assert.Throws<InvalidOperationException>(() =>
            RecoveryKeyWrapper.FromPem(weakKey.ExportSubjectPublicKeyInfoPem())
        );
    }

    [Fact]
    public void FromPem_PrivateKeyPem_Throws()
    {
        using var privateKey = RSA.Create(3072);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RecoveryKeyWrapper.FromPem(privateKey.ExportPkcs8PrivateKeyPem())
        );

        Assert.Contains("never deploy the private key", exception.Message);
    }

    #endregion

    #region Wrap Tests

    [Fact]
    public void Wrap_ThenPrivateKeyDecrypts_ReturnsMasterKey()
    {
        using var privateKey = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(privateKey.ExportSubjectPublicKeyInfoPem());
        var masterKey = CreateMasterKey();

        var result = Wrap(wrapper, masterKey);

        var unwrapped = privateKey.Decrypt(
            Base64Url.DecodeFromChars(result),
            RSAEncryptionPadding.OaepSHA256
        );
        Assert.Equal(masterKey, unwrapped);
    }

    [Fact]
    public void GetWrappedLength_Rsa3072_IsBase64UrlLengthOfModulus()
    {
        using var privateKey = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(privateKey.ExportSubjectPublicKeyInfoPem());

        var result = wrapper.GetWrappedLength();

        Assert.Equal(Base64Url.GetEncodedLength(3072 / 8), result);
    }

    #endregion

    #region WrapAsync Tests

    [Fact]
    public async Task WrapAsync_ThenPrivateKeyDecrypts_ReturnsWrappedKey()
    {
        using var privateKey = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(privateKey.ExportSubjectPublicKeyInfoPem());
        var masterKey = CreateMasterKey();

        var wrapped = await wrapper.WrapAsync(
            masterKey,
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None
        );

        Assert.Equal(RecoveryKeyWrapper.WrapperName, wrapped.WrapperName);
        Assert.Equal(wrapper.RecoveryKeyId, wrapped.KeyId);
        Assert.Equal(wrapper.GetWrappedLength(), wrapped.Ciphertext.Length);
        var unwrapped = privateKey.Decrypt(
            Base64Url.DecodeFromChars(wrapped.Ciphertext),
            RSAEncryptionPadding.OaepSHA256
        );
        Assert.Equal(masterKey, unwrapped);
    }

    [Fact]
    public async Task WrapAsync_NothingConfigured_Throws()
    {
        using var wrapper = CreateWrapper(new RecoveryKeyOptions());

        Assert.False(wrapper.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await wrapper.WrapAsync(
                CreateMasterKey(),
                ReadOnlyMemory<byte>.Empty,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task UnwrapAsync_Throws()
    {
        using var privateKey = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(privateKey.ExportSubjectPublicKeyInfoPem());
        var wrapped = await wrapper.WrapAsync(
            CreateMasterKey(),
            ReadOnlyMemory<byte>.Empty,
            CancellationToken.None
        );

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await wrapper.UnwrapAsync(
                wrapped,
                ReadOnlyMemory<byte>.Empty,
                new byte[SecretProtectionFormat.MasterKeySizeBytes],
                CancellationToken.None
            )
        );
    }

    #endregion

    #region RecoveryKeyId Property Tests

    [Fact]
    public void RecoveryKeyId_IsSpkiThumbprint()
    {
        using var privateKey = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(privateKey.ExportSubjectPublicKeyInfoPem());
        var expected = Base64Url.EncodeToString(
            SHA256.HashData(privateKey.ExportSubjectPublicKeyInfo())
        );

        var result = wrapper.RecoveryKeyId;

        Assert.Equal(expected, result);
    }

    #endregion
}
