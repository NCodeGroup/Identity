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
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class EphemeralKeyWrapperTests
{
    private static byte[] CreateKey() =>
        RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes);

    private static byte[] CreateContext() => "root-context"u8.ToArray();

    #region WrapAsync and UnwrapAsync Tests

    [Fact]
    public async Task WrapThenUnwrap_RoundTrips()
    {
        using var provider = new EphemeralKeyWrapper();
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
    public async Task Unwrap_DestinationLargerThanKey_WritesOnlyTheKey()
    {
        using var provider = new EphemeralKeyWrapper();
        var key = CreateKey();
        var context = CreateContext();
        var destination = new byte[key.Length + 8];
        Array.Fill(destination, (byte)0xFF);
        var wrappedKey = await provider.WrapAsync(key, context, CancellationToken.None);

        var written = await provider.UnwrapAsync(
            wrappedKey,
            context,
            destination,
            CancellationToken.None
        );

        Assert.Equal(key.Length, written);
        Assert.Equal(key, destination[..written]);
        Assert.All(destination[written..], x => Assert.Equal(0xFF, x));
    }

    [Fact]
    public async Task Unwrap_DestinationTooSmall_Throws()
    {
        using var provider = new EphemeralKeyWrapper();
        var context = CreateContext();
        var wrappedKey = await provider.WrapAsync(CreateKey(), context, CancellationToken.None);
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes - 1];

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await provider.UnwrapAsync(wrappedKey, context, destination, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Unwrap_DifferentContext_Throws()
    {
        using var provider = new EphemeralKeyWrapper();
        var wrappedKey = await provider.WrapAsync(
            CreateKey(),
            CreateContext(),
            CancellationToken.None
        );
        var otherContext = "other-context"u8.ToArray();
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes];

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await provider.UnwrapAsync(
                wrappedKey,
                otherContext,
                destination,
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task Unwrap_OtherInstance_Throws()
    {
        using var provider = new EphemeralKeyWrapper();
        using var otherProvider = new EphemeralKeyWrapper();
        var context = CreateContext();
        var wrappedKey = await provider.WrapAsync(CreateKey(), context, CancellationToken.None);
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes];

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await otherProvider.UnwrapAsync(
                wrappedKey,
                context,
                destination,
                CancellationToken.None
            )
        );

        Assert.Contains("another process", exception.Message);
    }

    [Fact]
    public async Task Wrap_SetsProviderNameAndFingerprint()
    {
        using var provider = new EphemeralKeyWrapper();

        var wrappedKey = await provider.WrapAsync(
            CreateKey(),
            CreateContext(),
            CancellationToken.None
        );

        Assert.Equal(KeyWrapperNames.Ephemeral, provider.Name);
        Assert.Equal(KeyWrapperNames.Ephemeral, wrappedKey.WrapperName);
        Assert.Equal(AesGcmKeyWrap.FingerprintLength, wrappedKey.KeyId.Length);
        Assert.Equal(SHA256.HashSizeInBytes, Base64Url.DecodeFromChars(wrappedKey.KeyId).Length);
    }

    [Fact]
    public async Task Wrap_CiphertextLength_EqualsGetWrappedLength()
    {
        using var provider = new EphemeralKeyWrapper();
        var key = CreateKey();

        var wrappedKey = await provider.WrapAsync(key, CreateContext(), CancellationToken.None);

        Assert.Equal(AesGcmKeyWrap.GetWrappedLength(key.Length), wrappedKey.Ciphertext.Length);
    }

    [Fact]
    public async Task Unwrap_TamperedCiphertext_ThrowsAndZeroesDestination()
    {
        using var provider = new EphemeralKeyWrapper();
        var context = CreateContext();
        var wrappedKey = await provider.WrapAsync(CreateKey(), context, CancellationToken.None);
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes];
        Array.Fill(destination, (byte)0xFF);

        var bytes = Base64Url.DecodeFromChars(wrappedKey.Ciphertext);
        bytes[SecretProtectionFormat.NonceSizeBytes] ^= 0x01;
        var tamperedKey = new WrappedKey
        {
            WrapperName = wrappedKey.WrapperName,
            KeyId = wrappedKey.KeyId,
            Ciphertext = Base64Url.EncodeToString(bytes),
        };

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await provider.UnwrapAsync(tamperedKey, context, destination, CancellationToken.None)
        );

        Assert.All(destination, x => Assert.Equal(0, x));
    }

    [Theory]
    [InlineData("")]
    [InlineData("AAAAA")]
    [InlineData("AAAA")]
    public async Task Unwrap_MalformedCiphertext_Throws(string ciphertext)
    {
        using var provider = new EphemeralKeyWrapper();
        var context = CreateContext();
        var wrappedKey = await provider.WrapAsync(CreateKey(), context, CancellationToken.None);
        var malformedKey = new WrappedKey
        {
            WrapperName = wrappedKey.WrapperName,
            KeyId = wrappedKey.KeyId,
            Ciphertext = ciphertext,
        };
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes];

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await provider.UnwrapAsync(malformedKey, context, destination, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Unwrap_IllegalCharacters_Throws()
    {
        using var provider = new EphemeralKeyWrapper();
        var context = CreateContext();
        var wrappedKey = await provider.WrapAsync(CreateKey(), context, CancellationToken.None);
        var malformedKey = new WrappedKey
        {
            WrapperName = wrappedKey.WrapperName,
            KeyId = wrappedKey.KeyId,
            Ciphertext = new string('!', wrappedKey.Ciphertext.Length),
        };
        var destination = new byte[SecretProtectionFormat.MasterKeySizeBytes];

        await Assert.ThrowsAnyAsync<CryptographicException>(async () =>
            await provider.UnwrapAsync(malformedKey, context, destination, CancellationToken.None)
        );
    }

    #endregion
}
