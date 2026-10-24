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
using System.Collections.Frozen;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultSecretProtectorTests
{
    private const string OtherPurpose = SecretProtectionPurposes.DataProtectionKeyRing;
    private const string ItemId = "Item-One";
    private const string OtherItemId = "Item-Two";

    private static readonly SecretOwner TenantOwner = SecretOwner.ForTenant("tenant-one");
    private static readonly SecretOwner OtherOwner = SecretOwner.ForTenant("tenant-two");

    private static readonly SecretProtectionContext Context = new(
        SecretProtectionPurposes.PersistedSecret,
        ItemId
    );

    private static byte[] CreateMaterial() =>
        RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes);

    private static byte[] CreatePlaintext() => RandomNumberGenerator.GetBytes(100);

    private static DefaultSecretProtector CreateProtector(
        SecretOwner owner,
        Guid keyId,
        byte[] material
    )
    {
        var masterKey = MasterKey.Create(keyId, owner, 1, material);
        var keys = new Dictionary<Guid, MasterKey> { [keyId] = masterKey }.ToFrozenDictionary();
        return new DefaultSecretProtector(owner, masterKey, keys);
    }

    private static DefaultSecretProtector CreateProtector() =>
        CreateProtector(TenantOwner, Guid.NewGuid(), CreateMaterial());

    private static byte[] Unprotect(
        DefaultSecretProtector protector,
        string protectedValue,
        SecretProtectionContext context
    )
    {
        var writer = new ArrayBufferWriter<byte>();
        protector.Unprotect(protectedValue, context, ref writer);
        return writer.WrittenSpan.ToArray();
    }

    private static byte[] Decode(string protectedValue) =>
        Base64Url.DecodeFromChars(protectedValue.AsSpan(SecretProtectionFormat.Prefix.Length));

    private static string Encode(ReadOnlySpan<byte> bytes) =>
        SecretProtectionFormat.Prefix + Base64Url.EncodeToString(bytes);

    private static string FlipByte(string protectedValue, int index)
    {
        var bytes = Decode(protectedValue);
        bytes[index] ^= 0x01;
        return Encode(bytes);
    }

    #region Protect Tests

    [Fact]
    public void Protect_ThenUnprotect_RoundTrips()
    {
        var protector = CreateProtector();
        var plaintext = CreatePlaintext();

        var protectedValue = protector.ProtectToString(plaintext, Context);
        var result = Unprotect(protector, protectedValue, Context);

        Assert.Equal(plaintext, result);
    }

    [Fact]
    public void Protect_EmptyPlaintext_RoundTrips()
    {
        var protector = CreateProtector();

        var protectedValue = protector.ProtectToString([], Context);
        var result = Unprotect(protector, protectedValue, Context);

        Assert.Empty(result);
    }

    [Fact]
    public void Protect_SamePlaintextTwice_ProducesDifferentValues()
    {
        var protector = CreateProtector();
        var plaintext = CreatePlaintext();

        var first = protector.ProtectToString(plaintext, Context);
        var second = protector.ProtectToString(plaintext, Context);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Protect_Always_StartsWithPrefix()
    {
        var protector = CreateProtector();

        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);

        Assert.StartsWith(SecretProtectionFormat.Prefix, protectedValue, StringComparison.Ordinal);
    }

    [Fact]
    public void Protect_DefaultContext_Throws()
    {
        var protector = CreateProtector();

        Assert.ThrowsAny<ArgumentException>(() =>
            protector.ProtectToString(CreatePlaintext(), default(SecretProtectionContext))
        );
    }

    [Fact]
    public void ProtectToString_RoundTrips()
    {
        var protector = CreateProtector();
        var plaintext = CreatePlaintext();

        var protectedValue = protector.ProtectToString(plaintext, Context);
        var result = Unprotect(protector, protectedValue, Context);

        Assert.StartsWith(SecretProtectionFormat.Prefix, protectedValue, StringComparison.Ordinal);
        Assert.Equal(plaintext, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(100)]
    public void GetProtectedLength_EqualsWrittenLength(int plaintextLength)
    {
        var protector = CreateProtector();
        var plaintext = RandomNumberGenerator.GetBytes(plaintextLength);
        var writer = new ArrayBufferWriter<char>();

        protector.Protect(plaintext, Context, ref writer);

        Assert.Equal(protector.GetProtectedLength(plaintextLength), writer.WrittenCount);
    }

    #endregion

    #region GetMaxPlaintextLength Tests

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(100)]
    public void GetMaxPlaintextLength_BoundsUnprotectedLength(int plaintextLength)
    {
        var protector = CreateProtector();
        var plaintext = RandomNumberGenerator.GetBytes(plaintextLength);
        var protectedValue = protector.ProtectToString(plaintext, Context);

        var maxLength = protector.GetMaxPlaintextLength(protectedValue.Length);
        var result = Unprotect(protector, protectedValue, Context);

        Assert.True(result.Length <= maxLength);
        Assert.Equal(plaintextLength, maxLength);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(5)] // The prefix only.
    [InlineData(6)] // A remainder of one character is not a valid base64url length.
    [InlineData(9)] // Decodes to fewer bytes than the header.
    [InlineData(50)] // 45 characters after the prefix is not a valid base64url length.
    public void GetMaxPlaintextLength_ImpossibleLength_ReturnsZero(int protectedLength)
    {
        var protector = CreateProtector();

        var result = protector.GetMaxPlaintextLength(protectedLength);

        Assert.Equal(0, result);
    }

    #endregion

    #region Unprotect Tests

    [Fact]
    public void Unprotect_DifferentItemId_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var otherContext = new SecretProtectionContext(Context.Purpose, OtherItemId);

        Assert.Throws<CryptographicException>(() =>
            Unprotect(protector, protectedValue, otherContext)
        );
    }

    [Fact]
    public void Unprotect_DifferentPurpose_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var otherContext = new SecretProtectionContext(OtherPurpose, Context.ItemId);

        Assert.Throws<CryptographicException>(() =>
            Unprotect(protector, protectedValue, otherContext)
        );
    }

    [Fact]
    public void Unprotect_DifferentOwner_Throws()
    {
        var keyId = Guid.NewGuid();
        var material = CreateMaterial();
        var protector = CreateProtector(TenantOwner, keyId, material);
        var otherProtector = CreateProtector(OtherOwner, keyId, material);
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);

        Assert.Throws<CryptographicException>(() =>
            Unprotect(otherProtector, protectedValue, Context)
        );
    }

    [Fact]
    public void Unprotect_TamperedPayload_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var tampered = FlipByte(protectedValue, SecretProtectionFormat.HeaderSizeBytes);

        Assert.Throws<CryptographicException>(() => Unprotect(protector, tampered, Context));
    }

    [Fact]
    public void Unprotect_TamperedWrappedKey_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var tampered = FlipByte(
            protectedValue,
            SecretProtectionFormat.KeyIdSizeBytes + SecretProtectionFormat.NonceSizeBytes
        );

        Assert.Throws<CryptographicException>(() => Unprotect(protector, tampered, Context));
    }

    [Fact]
    public void Unprotect_UnknownMasterKey_Throws()
    {
        var protector = CreateProtector();
        var otherProtector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);

        Assert.Throws<CryptographicException>(() =>
            Unprotect(otherProtector, protectedValue, Context)
        );
    }

    [Fact]
    public void Unprotect_NotPrefixed_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var notPrefixed = protectedValue[SecretProtectionFormat.Prefix.Length..];

        Assert.Throws<CryptographicException>(() => Unprotect(protector, notPrefixed, Context));
    }

    [Fact]
    public void Unprotect_Truncated_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var truncated = Encode(
            Decode(protectedValue).AsSpan(0, SecretProtectionFormat.HeaderSizeBytes - 1)
        );

        Assert.Throws<CryptographicException>(() => Unprotect(protector, truncated, Context));
    }

    [Fact]
    public void Unprotect_ImpossibleBase64UrlLength_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var impossible = protectedValue + "A";

        Assert.Equal(1, (impossible.Length - SecretProtectionFormat.Prefix.Length) % 4);
        Assert.Throws<CryptographicException>(() => Unprotect(protector, impossible, Context));
    }

    [Fact]
    public void Unprotect_InvalidBase64UrlCharacters_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var invalid = string.Concat(protectedValue.AsSpan(0, protectedValue.Length - 1), "!");

        Assert.Throws<CryptographicException>(() => Unprotect(protector, invalid, Context));
    }

    [Fact]
    public void Unprotect_TamperedPayload_DoesNotAdvanceDestination()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);
        var tampered = FlipByte(protectedValue, SecretProtectionFormat.HeaderSizeBytes);
        var writer = new ArrayBufferWriter<byte>();

        try
        {
            protector.Unprotect(tampered, Context, ref writer);
        }
        catch (CryptographicException)
        {
            // Expected: the payload does not authenticate.
        }

        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void Unprotect_RetiredKey_Succeeds()
    {
        var retiredKeyId = Guid.NewGuid();
        var retiredMaterial = CreateMaterial();
        var plaintext = CreatePlaintext();
        var retiredProtector = CreateProtector(TenantOwner, retiredKeyId, retiredMaterial);
        var protectedValue = retiredProtector.ProtectToString(plaintext, Context);

        var retiredKey = MasterKey.Create(retiredKeyId, TenantOwner, 1, retiredMaterial);
        var activeKey = MasterKey.Create(Guid.NewGuid(), TenantOwner, 2, CreateMaterial());
        var keys = new Dictionary<Guid, MasterKey>
        {
            [retiredKey.KeyId] = retiredKey,
            [activeKey.KeyId] = activeKey,
        }.ToFrozenDictionary();
        var protector = new DefaultSecretProtector(TenantOwner, activeKey, keys);

        var result = Unprotect(protector, protectedValue, Context);

        Assert.Equal(plaintext, result);
    }

    [Fact]
    public void Unprotect_ItemIdDifferentCase_Succeeds()
    {
        var protector = CreateProtector();
        var plaintext = CreatePlaintext();
        var protectedValue = protector.ProtectToString(plaintext, Context);
        var otherCaseContext = new SecretProtectionContext(
            Context.Purpose,
            Context.ItemId.ToUpperInvariant()
        );

        var result = Unprotect(protector, protectedValue, otherCaseContext);

        Assert.Equal(plaintext, result);
    }

    [Fact]
    public void Unprotect_DefaultContext_Throws()
    {
        var protector = CreateProtector();
        var protectedValue = protector.ProtectToString(CreatePlaintext(), Context);

        Assert.ThrowsAny<ArgumentException>(() =>
            Unprotect(protector, protectedValue, default(SecretProtectionContext))
        );
    }

    #endregion
}
