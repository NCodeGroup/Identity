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
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffPayloadProtectorTests : BaseTests
{
    private static readonly byte[] Plaintext = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] AssociatedData = [9, 8, 7];

    #region Protect and TryUnprotect Tests

    [Fact]
    public void TryUnprotect_RoundTrip_ReturnsThePlaintext()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var protectedData = BffPayloadProtector.Protect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            Plaintext,
            AssociatedData
        );
        var writer = new ArrayBufferWriter<byte>();

        var result = BffPayloadProtector.TryUnprotect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            protectedData,
            AssociatedData,
            writer
        );

        Assert.True(result);
        Assert.Equal(Plaintext, writer.WrittenSpan.ToArray());
        Assert.Equal(12 + Plaintext.Length + 16, protectedData.Length);
    }

    [Fact]
    public void Protect_InvalidSecret_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            BffPayloadProtector.Protect(
                "short",
                BffKeyDerivation.SessionPayloadPurpose,
                Plaintext,
                AssociatedData
            )
        );
    }

    [Fact]
    public void TryUnprotect_WrongSecret_ReturnsFalse()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var other = BffKeyDerivation.CreateSecret();
        var protectedData = BffPayloadProtector.Protect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            Plaintext,
            AssociatedData
        );
        var writer = new ArrayBufferWriter<byte>();

        var result = BffPayloadProtector.TryUnprotect(
            other,
            BffKeyDerivation.SessionPayloadPurpose,
            protectedData,
            AssociatedData,
            writer
        );

        Assert.False(result);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void TryUnprotect_WrongPurpose_ReturnsFalse()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var protectedData = BffPayloadProtector.Protect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            Plaintext,
            AssociatedData
        );
        var writer = new ArrayBufferWriter<byte>();

        var result = BffPayloadProtector.TryUnprotect(
            secret,
            BffKeyDerivation.LoginPayloadPurpose,
            protectedData,
            AssociatedData,
            writer
        );

        Assert.False(result);
    }

    [Fact]
    public void TryUnprotect_ChangedAssociatedData_ReturnsFalse()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var protectedData = BffPayloadProtector.Protect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            Plaintext,
            AssociatedData
        );
        var writer = new ArrayBufferWriter<byte>();

        var result = BffPayloadProtector.TryUnprotect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            protectedData,
            [9, 8, 6],
            writer
        );

        Assert.False(result);
    }

    [Fact]
    public void TryUnprotect_FlippedCiphertextByte_ReturnsFalse()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var protectedData = BffPayloadProtector.Protect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            Plaintext,
            AssociatedData
        );
        protectedData[12] ^= 0x01;
        var writer = new ArrayBufferWriter<byte>();

        var result = BffPayloadProtector.TryUnprotect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            protectedData,
            AssociatedData,
            writer
        );

        Assert.False(result);
        Assert.Equal(0, writer.WrittenCount);
    }

    [Fact]
    public void TryUnprotect_TruncatedInput_ReturnsFalse()
    {
        var secret = BffKeyDerivation.CreateSecret();
        var protectedData = BffPayloadProtector.Protect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            Plaintext,
            AssociatedData
        );
        var writer = new ArrayBufferWriter<byte>();

        var tooShort = BffPayloadProtector.TryUnprotect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            protectedData.AsSpan(0, 27),
            AssociatedData,
            writer
        );
        var truncated = BffPayloadProtector.TryUnprotect(
            secret,
            BffKeyDerivation.SessionPayloadPurpose,
            protectedData.AsSpan(0, protectedData.Length - 1),
            AssociatedData,
            writer
        );

        Assert.False(tooShort);
        Assert.False(truncated);
    }

    #endregion
}
