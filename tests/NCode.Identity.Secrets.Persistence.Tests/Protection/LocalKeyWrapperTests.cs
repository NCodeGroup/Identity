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

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class LocalKeyWrapperTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        $"{nameof(LocalKeyWrapperTests)}-{Guid.NewGuid():N}"
    );

    public LocalKeyWrapperTests()
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

    private static byte[] CreateKey() =>
        RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes);

    private static byte[] CreateContext() => "root-context"u8.ToArray();

    private static LocalKeyWrapper CreateProvider(LocalRootKeyOptions options) =>
        new(Microsoft.Extensions.Options.Options.Create(options));

    private string WriteKeyFile(string content)
    {
        var path = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.key");
        File.WriteAllText(path, content, SecureEncoding.UTF8);
        return path;
    }

    #region WrapAsync and UnwrapAsync Tests

    [Fact]
    public async Task WrapThenUnwrap_InlineKey_RoundTrips()
    {
        using var provider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(CreateKey()) }
        );
        var key = CreateKey();
        var context = CreateContext();
        var destination = new byte[key.Length];

        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);
        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(KeyWrapperNames.Local, wrappedKey.WrapperName);
        Assert.Equal(key.Length, written);
        Assert.Equal(key, destination);
    }

    [Fact]
    public async Task Wrap_InlineKey_StoresNonceCiphertextTagUnderFingerprint()
    {
        var rootKey = CreateKey();
        using var provider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(rootKey) }
        );
        var key = CreateKey();
        var context = CreateContext();

        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);

        Assert.Equal(Base64Url.EncodeToString(SHA256.HashData(rootKey)), wrappedKey.KeyId);
        var wrapped = Base64Url.DecodeFromChars(wrappedKey.Ciphertext);
        Assert.Equal(12 + key.Length + 16, wrapped.Length);
        var nonce = wrapped[..12];
        var ciphertext = wrapped[12..^16];
        var tag = wrapped[^16..];
        var plaintext = new byte[key.Length];
        using var aes = new AesGcm(rootKey, 16);
        aes.Decrypt(nonce, ciphertext, tag, plaintext, context);
        Assert.Equal(key, plaintext);
    }

    [Fact]
    public async Task WrapThenUnwrap_KeyPath_RoundTrips()
    {
        var path = WriteKeyFile(Base64Url.EncodeToString(CreateKey()) + Environment.NewLine);
        using var provider = CreateProvider(new LocalRootKeyOptions { KeyPath = path });
        var key = CreateKey();
        var context = CreateContext();
        var destination = new byte[key.Length];

        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);
        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(key.Length, written);
        Assert.Equal(key, destination);
    }

    [Fact]
    public async Task WrapThenUnwrap_KeyFileWithTrailingNewline_RoundTrips()
    {
        var path = WriteKeyFile(Base64Url.EncodeToString(CreateKey()) + "\r\n");
        using var provider = CreateProvider(new LocalRootKeyOptions { KeyPath = path });
        var key = CreateKey();
        var context = CreateContext();
        var destination = new byte[key.Length];

        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);
        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(key.Length, written);
        Assert.Equal(key, destination);
    }

    [Fact]
    public async Task WrapThenUnwrap_KeyFileWithUtf8Bom_RoundTrips()
    {
        var path = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.key");
        File.WriteAllText(
            path,
            Base64Url.EncodeToString(CreateKey()) + Environment.NewLine,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true, throwOnInvalidBytes: true)
        );
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, File.ReadAllBytes(path)[..3]);
        using var provider = CreateProvider(new LocalRootKeyOptions { KeyPath = path });
        var key = CreateKey();
        var context = CreateContext();
        var destination = new byte[key.Length];

        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);
        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(key.Length, written);
        Assert.Equal(key, destination);
    }

    [Fact]
    public async Task Wrap_CiphertextLength_EqualsGetWrappedLength()
    {
        using var provider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(CreateKey()) }
        );
        var key = CreateKey();

        var wrappedKey = await provider.WrapAsync(key, CreateContext(), CancellationToken.None);

        Assert.Equal(AesGcmKeyWrap.FingerprintLength, wrappedKey.KeyId.Length);
        Assert.Equal(AesGcmKeyWrap.GetWrappedLength(key.Length), wrappedKey.Ciphertext.Length);
    }

    [Fact]
    public async Task Unwrap_DestinationTooSmall_Throws()
    {
        using var provider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(CreateKey()) }
        );
        var context = CreateContext();
        var wrappedKey = await provider.WrapAsync(CreateKey(), context, CancellationToken.None);
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes - 1];

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Unwrap_WrappedByPreviousKey_RoundTrips()
    {
        var previousRootKey = CreateKey();
        var key = CreateKey();
        var context = CreateContext();
        using var previousProvider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(previousRootKey) }
        );
        var wrappedKey = await previousProvider.WrapAsync(key, context, CancellationToken.None);
        var previousPath = WriteKeyFile(Base64Url.EncodeToString(previousRootKey));
        using var provider = CreateProvider(
            new LocalRootKeyOptions
            {
                Key = Base64Url.EncodeToString(CreateKey()),
                PreviousKeyPaths = [previousPath],
            }
        );
        var destination = new byte[key.Length];

        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(key.Length, written);
        Assert.Equal(key, destination);
    }

    [Fact]
    public async Task Unwrap_UnknownFingerprint_Throws()
    {
        var context = CreateContext();
        using var previousProvider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(CreateKey()) }
        );
        var wrappedKey = await previousProvider.WrapAsync(
            CreateKey(),
            context,
            CancellationToken.None
        );
        using var provider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(CreateKey()) }
        );
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes];

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None)
        );

        Assert.Contains(nameof(LocalRootKeyOptions.PreviousKeyPaths), exception.Message);
    }

    #endregion

    #region WrapAsync Tests

    [Fact]
    public async Task Wrap_NotConfigured_Throws()
    {
        using var provider = CreateProvider(new LocalRootKeyOptions());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.WrapAsync(CreateKey(), CreateContext(), CancellationToken.None)
        );

        Assert.Contains(LocalRootKeyOptions.DefaultSectionName, exception.Message);
    }

    [Fact]
    public async Task Wrap_BothKeyAndKeyPath_Throws()
    {
        var path = WriteKeyFile(Base64Url.EncodeToString(CreateKey()));
        using var provider = CreateProvider(
            new LocalRootKeyOptions { Key = Base64Url.EncodeToString(CreateKey()), KeyPath = path }
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.WrapAsync(CreateKey(), CreateContext(), CancellationToken.None)
        );

        Assert.Contains(LocalRootKeyOptions.DefaultSectionName, exception.Message);
    }

    [Fact]
    public async Task Wrap_KeyOfWrongLength_Throws()
    {
        using var provider = CreateProvider(
            new LocalRootKeyOptions
            {
                Key = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16)),
            }
        );

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.WrapAsync(CreateKey(), CreateContext(), CancellationToken.None)
        );

        Assert.Contains(LocalRootKeyOptions.DefaultSectionName, exception.Message);
    }

    [Fact]
    public async Task Wrap_KeyNotBase64Url_Throws()
    {
        using var provider = CreateProvider(new LocalRootKeyOptions { Key = "not base64url!" });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.WrapAsync(CreateKey(), CreateContext(), CancellationToken.None)
        );

        Assert.Contains(LocalRootKeyOptions.DefaultSectionName, exception.Message);
    }

    [Fact]
    public async Task Wrap_KeyFileTooLarge_Throws()
    {
        var path = WriteKeyFile(new string('A', 1025));
        using var provider = CreateProvider(new LocalRootKeyOptions { KeyPath = path });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.WrapAsync(CreateKey(), CreateContext(), CancellationToken.None)
        );

        Assert.Contains(LocalRootKeyOptions.DefaultSectionName, exception.Message);
        Assert.Contains(path, exception.Message);
    }

    [Fact]
    public async Task Wrap_KeyFileUtf16_Throws()
    {
        var path = Path.Combine(_tempDirectory, $"{Guid.NewGuid():N}.key");
        File.WriteAllText(
            path,
            Base64Url.EncodeToString(CreateKey()),
            new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true)
        );
        using var provider = CreateProvider(new LocalRootKeyOptions { KeyPath = path });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.WrapAsync(CreateKey(), CreateContext(), CancellationToken.None)
        );

        Assert.Contains(LocalRootKeyOptions.DefaultSectionName, exception.Message);
        Assert.Contains(path, exception.Message);
        Assert.Contains("UTF-8", exception.Message);
    }

    [Fact]
    public async Task Wrap_KeyPathOfMissingFile_Throws()
    {
        var path = Path.Combine(_tempDirectory, "missing.key");
        using var provider = CreateProvider(new LocalRootKeyOptions { KeyPath = path });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await provider.WrapAsync(CreateKey(), CreateContext(), CancellationToken.None)
        );

        Assert.Contains(LocalRootKeyOptions.DefaultSectionName, exception.Message);
        Assert.Contains(path, exception.Message);
    }

    #endregion
}
