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

namespace NCode.Identity.Secrets.Persistence.Protection;

public class SecretProtectionFormatTests
{
    private const string KnownKeyId = "00112233-4455-6677-8899-aabbccddeeff";

    // The big-endian bytes of KnownKeyId, written out by hand.
    private static readonly byte[] KnownKeyIdBytes =
    [
        0x00,
        0x11,
        0x22,
        0x33,
        0x44,
        0x55,
        0x66,
        0x77,
        0x88,
        0x99,
        0xaa,
        0xbb,
        0xcc,
        0xdd,
        0xee,
        0xff,
    ];

    private static byte[] CreateMasterKey() =>
        [.. Enumerable.Range(0, SecretProtectionFormat.MasterKeySizeBytes).Select(i => (byte)i)];

    private static byte[] LengthPrefixed(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        byte[] length =
        [
            (byte)(bytes.Length >> 24),
            (byte)(bytes.Length >> 16),
            (byte)(bytes.Length >> 8),
            (byte)bytes.Length,
        ];
        return [.. length, .. bytes];
    }

    // Writes the associated data with the span-based writer into an oversized destination that is filled with
    // non-zero bytes, asserts that the writer reports the size that GetAssociatedDataSize predicts, and returns the
    // bytes that were written.
    private static byte[] CreateAssociatedData(
        byte role,
        Guid masterKeyId,
        SecretOwner owner,
        string purpose,
        string itemId
    )
    {
        var context = new SecretProtectionContext(purpose, itemId);

        var size = SecretProtectionFormat.GetAssociatedDataSize(owner, context);

        var destination = new byte[size + 3];
        Array.Fill(destination, (byte)0xFF);
        var written = SecretProtectionFormat.WriteAssociatedData(
            role,
            masterKeyId,
            owner,
            context,
            destination
        );

        Assert.Equal(size, written);

        return destination[..written];
    }

    // Writes the root context with the span-based writer into an oversized destination that is filled with non-zero
    // bytes, asserts that the writer reports the size that GetRootContextSize predicts, and returns the bytes that were
    // written.
    private static byte[] CreateRootContext(Guid masterKeyId, SecretOwner owner)
    {
        var size = SecretProtectionFormat.GetRootContextSize(owner);

        var destination = new byte[size + 3];
        Array.Fill(destination, (byte)0xFF);
        var written = SecretProtectionFormat.WriteRootContext(masterKeyId, owner, destination);

        Assert.Equal(size, written);

        return destination[..written];
    }

    // Computes the key check into an oversized destination that is filled with non-zero bytes, and returns the bytes
    // that were written as base64url text.
    private static string ComputeKeyCheck(
        byte[] masterKey,
        Guid masterKeyId,
        SecretOwner owner,
        int version
    )
    {
        var destination = new byte[SecretProtectionFormat.KeyCheckSizeBytes + 3];
        Array.Fill(destination, (byte)0xFF);
        SecretProtectionFormat.ComputeKeyCheck(masterKey, masterKeyId, owner, version, destination);

        return Base64Url.EncodeToString(
            destination.AsSpan(0, SecretProtectionFormat.KeyCheckSizeBytes)
        );
    }

    private static byte[] DeriveKeyEncryptionKey(byte[] masterKey, Guid masterKeyId, string purpose)
    {
        var destination = new byte[SecretProtectionFormat.DataKeySizeBytes];
        SecretProtectionFormat.DeriveKeyEncryptionKey(masterKey, masterKeyId, purpose, destination);
        return destination;
    }

    #region Constant Tests

    [Fact]
    public void HeaderSizeBytes_IsSumOfFixedFields()
    {
        const int expected =
            SecretProtectionFormat.KeyIdSizeBytes
            + SecretProtectionFormat.NonceSizeBytes
            + SecretProtectionFormat.DataKeySizeBytes
            + SecretProtectionFormat.TagSizeBytes
            + SecretProtectionFormat.NonceSizeBytes
            + SecretProtectionFormat.TagSizeBytes;

        Assert.Equal(expected, SecretProtectionFormat.HeaderSizeBytes);
    }

    #endregion

    #region CreateAssociatedData Tests

    [Fact]
    public void CreateAssociatedData_KnownInput_MatchesSpecifiedLayout()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var owner = new SecretOwner("Tenant", "Acme-ONE");

        byte[] expected =
        [
            .. "ncode-identity/secret-protection/v1"u8,
            0x00,
            SecretProtectionFormat.RoleDataKeyWrap,
            .. KnownKeyIdBytes,
            .. LengthPrefixed("tenant"),
            .. LengthPrefixed("acme-one"),
            .. LengthPrefixed("Test.Purpose"),
            .. LengthPrefixed("item-one"),
        ];

        var actual = CreateAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            masterKeyId,
            owner,
            "Test.Purpose",
            "Item-ONE"
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CreateAssociatedData_DifferentRole_Differs()
    {
        var masterKeyId = Guid.NewGuid();
        var owner = SecretOwner.ForTenant("tenant-1");

        var wrap = CreateAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            masterKeyId,
            owner,
            "purpose",
            "item"
        );
        var payload = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "purpose",
            "item"
        );

        Assert.NotEqual(wrap, payload);
    }

    [Fact]
    public void CreateAssociatedData_PayloadRole_WritesZeroMasterKeyId()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var owner = SecretOwner.ForTenant("tenant-1");

        var associatedData = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "purpose",
            "item"
        );

        // The label, its zero terminator, and the role byte precede the master key identifier.
        const int keyIdOffset = 35 + 1 + 1;
        var keyIdField = associatedData.AsSpan(keyIdOffset, SecretProtectionFormat.KeyIdSizeBytes);
        Assert.Equal(new byte[SecretProtectionFormat.KeyIdSizeBytes], keyIdField.ToArray());
    }

    [Fact]
    public void CreateAssociatedData_PayloadRoleDifferentMasterKeyId_IsEqual()
    {
        var owner = SecretOwner.ForTenant("tenant-1");

        var first = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            Guid.NewGuid(),
            owner,
            "purpose",
            "item"
        );
        var second = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            Guid.NewGuid(),
            owner,
            "purpose",
            "item"
        );

        Assert.Equal(first, second);
    }

    [Fact]
    public void CreateAssociatedData_DataKeyWrapDifferentMasterKeyId_Differs()
    {
        var owner = SecretOwner.ForTenant("tenant-1");

        var first = CreateAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            Guid.NewGuid(),
            owner,
            "purpose",
            "item"
        );
        var second = CreateAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            Guid.NewGuid(),
            owner,
            "purpose",
            "item"
        );

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateAssociatedData_OwnerIdDifferentCase_IsEqual()
    {
        var masterKeyId = Guid.NewGuid();

        var lower = CreateAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            masterKeyId,
            new SecretOwner("tenant", "tenant-abc"),
            "purpose",
            "item"
        );
        var upper = CreateAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            masterKeyId,
            new SecretOwner("TENANT", "TENANT-ABC"),
            "purpose",
            "item"
        );

        Assert.Equal(lower, upper);
    }

    [Fact]
    public void CreateAssociatedData_ItemIdDifferentCase_IsEqual()
    {
        var masterKeyId = Guid.NewGuid();
        var owner = SecretOwner.ForTenant("tenant-1");

        var lower = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "purpose",
            "item-abc"
        );
        var upper = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "purpose",
            "ITEM-ABC"
        );

        Assert.Equal(lower, upper);
    }

    [Fact]
    public void CreateAssociatedData_DifferentItemId_Differs()
    {
        var masterKeyId = Guid.NewGuid();
        var owner = SecretOwner.ForTenant("tenant-1");

        var first = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "purpose",
            "item-1"
        );
        var second = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "purpose",
            "item-2"
        );

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateAssociatedData_PurposeDifferentCase_Differs()
    {
        var masterKeyId = Guid.NewGuid();
        var owner = SecretOwner.ForTenant("tenant-1");

        var lower = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "purpose",
            "item"
        );
        var upper = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "PURPOSE",
            "item"
        );

        Assert.NotEqual(lower, upper);
    }

    [Fact]
    public void CreateAssociatedData_NonAsciiIds_LowerCasesInvariantly()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var owner = new SecretOwner("Tenant", "ÄÖÜ-ONE");

        byte[] expected =
        [
            .. "ncode-identity/secret-protection/v1"u8,
            0x00,
            SecretProtectionFormat.RolePayload,
            .. new byte[SecretProtectionFormat.KeyIdSizeBytes],
            .. LengthPrefixed("tenant"),
            .. LengthPrefixed("äöü-one"),
            .. LengthPrefixed("Purpose-Ü"),
            .. LengthPrefixed("item-ä"),
        ];

        var actual = CreateAssociatedData(
            SecretProtectionFormat.RolePayload,
            masterKeyId,
            owner,
            "Purpose-Ü",
            "ITEM-Ä"
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteAssociatedData_DestinationTooSmall_Throws()
    {
        var masterKeyId = Guid.NewGuid();
        var owner = SecretOwner.ForTenant("tenant-1");
        var context = new SecretProtectionContext("purpose", "item");
        var size = SecretProtectionFormat.GetAssociatedDataSize(owner, context);

        for (var length = 0; length < size; ++length)
        {
            var destinationLength = length;
            Assert.Throws<ArgumentException>(() =>
                SecretProtectionFormat.WriteAssociatedData(
                    SecretProtectionFormat.RoleDataKeyWrap,
                    masterKeyId,
                    owner,
                    context,
                    new byte[destinationLength]
                )
            );
        }
    }

    [Fact]
    public void WriteAssociatedData_DestinationExactSize_WritesAll()
    {
        var masterKeyId = Guid.NewGuid();
        var owner = SecretOwner.ForTenant("tenant-1");
        var context = new SecretProtectionContext("purpose", "item");
        var expected = CreateAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            masterKeyId,
            owner,
            "purpose",
            "item"
        );
        var destination = new byte[SecretProtectionFormat.GetAssociatedDataSize(owner, context)];

        var written = SecretProtectionFormat.WriteAssociatedData(
            SecretProtectionFormat.RoleDataKeyWrap,
            masterKeyId,
            owner,
            context,
            destination
        );

        Assert.Equal(destination.Length, written);
        Assert.Equal(expected, destination);
    }

    #endregion

    #region DeriveKeyEncryptionKey Tests

    [Fact]
    public void DeriveKeyEncryptionKey_NonAsciiPurpose_MatchesHkdf()
    {
        const string purpose = "Purpose-Ü-日本";
        var masterKeyId = new Guid(KnownKeyId);
        var masterKey = CreateMasterKey();

        byte[] info =
        [
            .. "ncode-identity/secret-protection/v1/kek"u8,
            0x00,
            .. Encoding.UTF8.GetBytes(purpose),
        ];
        var expected = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            masterKey,
            SecretProtectionFormat.DataKeySizeBytes,
            KnownKeyIdBytes,
            info
        );

        var actual = DeriveKeyEncryptionKey(masterKey, masterKeyId, purpose);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DeriveKeyEncryptionKey_KnownInput_MatchesHkdf()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var masterKey = CreateMasterKey();

        byte[] info = [.. "ncode-identity/secret-protection/v1/kek"u8, 0x00, .. "Test.Purpose"u8];
        var expected = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            masterKey,
            SecretProtectionFormat.DataKeySizeBytes,
            KnownKeyIdBytes,
            info
        );

        var actual = DeriveKeyEncryptionKey(masterKey, masterKeyId, "Test.Purpose");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DeriveKeyEncryptionKey_SameInputs_AreEqual()
    {
        var masterKeyId = Guid.NewGuid();
        var masterKey = CreateMasterKey();

        var first = DeriveKeyEncryptionKey(masterKey, masterKeyId, "purpose");
        var second = DeriveKeyEncryptionKey(masterKey, masterKeyId, "purpose");

        Assert.Equal(first, second);
    }

    [Fact]
    public void DeriveKeyEncryptionKey_DifferentPurpose_Differs()
    {
        var masterKeyId = Guid.NewGuid();
        var masterKey = CreateMasterKey();

        var first = DeriveKeyEncryptionKey(masterKey, masterKeyId, "purpose-1");
        var second = DeriveKeyEncryptionKey(masterKey, masterKeyId, "purpose-2");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void DeriveKeyEncryptionKey_DifferentMasterKeyId_Differs()
    {
        var masterKey = CreateMasterKey();

        var first = DeriveKeyEncryptionKey(masterKey, Guid.NewGuid(), "purpose");
        var second = DeriveKeyEncryptionKey(masterKey, Guid.NewGuid(), "purpose");

        Assert.NotEqual(first, second);
    }

    #endregion

    #region ComputeKeyCheck Tests

    [Fact]
    public void ComputeKeyCheck_KnownInput_MatchesSpecifiedLayout()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var masterKey = CreateMasterKey();
        var owner = new SecretOwner("Tenant", "Acme-ONE");

        byte[] data =
        [
            .. "ncode-identity/secret-protection/v1/key-check"u8,
            0x00,
            .. KnownKeyIdBytes,
            .. LengthPrefixed("tenant"),
            .. LengthPrefixed("acme-one"),
            0x00,
            0x00,
            0x01,
            0x02,
        ];
        var expected = Base64Url.EncodeToString(HMACSHA256.HashData(masterKey, data));

        var actual = ComputeKeyCheck(masterKey, masterKeyId, owner, 258);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ComputeKeyCheck_DifferentOwner_Differs()
    {
        var masterKeyId = Guid.NewGuid();
        var masterKey = CreateMasterKey();

        var first = ComputeKeyCheck(masterKey, masterKeyId, SecretOwner.ForTenant("tenant-1"), 1);
        var second = ComputeKeyCheck(masterKey, masterKeyId, SecretOwner.ForTenant("tenant-2"), 1);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeKeyCheck_OwnerIdDifferentCase_IsEqual()
    {
        var masterKeyId = Guid.NewGuid();
        var masterKey = CreateMasterKey();

        var lower = ComputeKeyCheck(
            masterKey,
            masterKeyId,
            new SecretOwner("tenant", "tenant-abc"),
            1
        );
        var upper = ComputeKeyCheck(
            masterKey,
            masterKeyId,
            new SecretOwner("TENANT", "TENANT-ABC"),
            1
        );

        Assert.Equal(lower, upper);
    }

    [Fact]
    public void ComputeKeyCheck_DifferentVersion_Differs()
    {
        var masterKeyId = Guid.NewGuid();
        var masterKey = CreateMasterKey();
        var owner = SecretOwner.ForTenant("tenant-1");

        var first = ComputeKeyCheck(masterKey, masterKeyId, owner, 1);
        var second = ComputeKeyCheck(masterKey, masterKeyId, owner, 2);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ComputeKeyCheck_DestinationTooSmall_Throws()
    {
        var masterKey = CreateMasterKey();
        var owner = SecretOwner.ForTenant("tenant-1");

        Assert.Throws<ArgumentException>(() =>
            SecretProtectionFormat.ComputeKeyCheck(
                masterKey,
                Guid.NewGuid(),
                owner,
                1,
                new byte[SecretProtectionFormat.KeyCheckSizeBytes - 1]
            )
        );
    }

    #endregion

    #region CreateRootContext Tests

    [Fact]
    public void CreateRootContext_KnownInput_MatchesSpecifiedLayout()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var owner = new SecretOwner("Tenant", "Acme-ONE");

        byte[] expected =
        [
            .. "ncode-identity/secret-protection/v1/root"u8,
            0x00,
            .. KnownKeyIdBytes,
            .. LengthPrefixed("tenant"),
            .. LengthPrefixed("acme-one"),
        ];

        var actual = CreateRootContext(masterKeyId, owner);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WriteRootContext_DestinationTooSmall_Throws()
    {
        var masterKeyId = Guid.NewGuid();
        var owner = SecretOwner.ForTenant("tenant-1");
        var size = SecretProtectionFormat.GetRootContextSize(owner);

        for (var length = 0; length < size; ++length)
        {
            var destinationLength = length;
            Assert.Throws<ArgumentException>(() =>
                SecretProtectionFormat.WriteRootContext(
                    masterKeyId,
                    owner,
                    new byte[destinationLength]
                )
            );
        }
    }

    #endregion

    #region TryReadMasterKeyId Tests

    [Fact]
    public void TryReadMasterKeyId_ValidEnvelope_ReturnsKeyId()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var envelope = new byte[SecretProtectionFormat.HeaderSizeBytes];
        KnownKeyIdBytes.CopyTo(envelope, 0);
        var protectedValue = SecretProtectionFormat.Prefix + Base64Url.EncodeToString(envelope);

        var result = SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var actual);

        Assert.True(result);
        Assert.Equal(masterKeyId, actual);
    }

    [Fact]
    public void TryReadMasterKeyId_NotPrefixed_ReturnsFalse()
    {
        var envelope = new byte[SecretProtectionFormat.HeaderSizeBytes];
        KnownKeyIdBytes.CopyTo(envelope, 0);
        var protectedValue = Base64Url.EncodeToString(envelope);

        var result = SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var actual);

        Assert.False(result);
        Assert.Equal(Guid.Empty, actual);
    }

    [Fact]
    public void TryReadMasterKeyId_Truncated_ReturnsFalse()
    {
        var envelope = new byte[SecretProtectionFormat.HeaderSizeBytes - 1];
        KnownKeyIdBytes.CopyTo(envelope, 0);
        var protectedValue = SecretProtectionFormat.Prefix + Base64Url.EncodeToString(envelope);

        var result = SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var actual);

        Assert.False(result);
        Assert.Equal(Guid.Empty, actual);
    }

    [Theory]
    [InlineData("!!!!")]
    [InlineData("A")]
    public void TryReadMasterKeyId_InvalidBase64Url_ReturnsFalse(string encoded)
    {
        var protectedValue = SecretProtectionFormat.Prefix + encoded;

        var result = SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var actual);

        Assert.False(result);
        Assert.Equal(Guid.Empty, actual);
    }

    [Fact]
    public void TryReadMasterKeyId_ReadsHeaderWithoutDecodingPayload()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var envelope = new byte[SecretProtectionFormat.HeaderSizeBytes + 20];
        KnownKeyIdBytes.CopyTo(envelope, 0);
        var encoded = Base64Url.EncodeToString(envelope);

        // Everything after the first 30 characters is replaced by characters that are not valid base64url text.
        var protectedValue =
            SecretProtectionFormat.Prefix + encoded[..30] + new string('!', encoded.Length - 30);

        var result = SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var actual);

        Assert.True(result);
        Assert.Equal(masterKeyId, actual);
    }

    [Fact]
    public void TryReadMasterKeyId_InvalidHeaderCharacters_ReturnsFalse()
    {
        var envelope = new byte[SecretProtectionFormat.HeaderSizeBytes];
        KnownKeyIdBytes.CopyTo(envelope, 0);
        var encoded = Base64Url.EncodeToString(envelope);
        var protectedValue = SecretProtectionFormat.Prefix + "!" + encoded[1..];

        var result = SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var actual);

        Assert.False(result);
        Assert.Equal(Guid.Empty, actual);
    }

    [Fact]
    public void TryReadMasterKeyId_ValidEnvelopeAsSpan_ReturnsKeyId()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var envelope = new byte[SecretProtectionFormat.HeaderSizeBytes + 20];
        KnownKeyIdBytes.CopyTo(envelope, 0);
        var protectedValue = SecretProtectionFormat.Prefix + Base64Url.EncodeToString(envelope);

        var result = SecretProtectionFormat.TryReadMasterKeyId(
            protectedValue.AsSpan(),
            out var actual
        );

        Assert.True(result);
        Assert.Equal(masterKeyId, actual);
    }

    #endregion

    #region WriteMasterKeyId and ReadMasterKeyId Tests

    [Fact]
    public void WriteMasterKeyId_KnownGuid_WritesBigEndianBytes()
    {
        var masterKeyId = new Guid(KnownKeyId);
        var destination = new byte[SecretProtectionFormat.KeyIdSizeBytes];

        SecretProtectionFormat.WriteMasterKeyId(masterKeyId, destination);

        Assert.Equal(KnownKeyIdBytes, destination);
    }

    [Fact]
    public void WriteMasterKeyId_DestinationTooShort_Throws()
    {
        var masterKeyId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() =>
            SecretProtectionFormat.WriteMasterKeyId(
                masterKeyId,
                new byte[SecretProtectionFormat.KeyIdSizeBytes - 1]
            )
        );
    }

    [Fact]
    public void WriteThenRead_RoundTripsGuid()
    {
        var masterKeyId = Guid.NewGuid();
        var buffer = new byte[SecretProtectionFormat.KeyIdSizeBytes];

        SecretProtectionFormat.WriteMasterKeyId(masterKeyId, buffer);
        var actual = SecretProtectionFormat.ReadMasterKeyId(buffer);

        Assert.Equal(masterKeyId, actual);
    }

    #endregion
}
