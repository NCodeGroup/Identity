// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class SecretProtectorExtensionsTests
{
    private static SecretProtectionContext Context { get; } = new("test/purpose", "item-1");

    // Moq cannot set up a method whose writer is a ref struct, so the protector is a stub that writes a fixed output.
    private sealed class FixedOutputProtector(int protectedLength, string output) : ISecretProtector
    {
        public SecretOwner Owner => SecretOwner.ForTenant("tenant-1");

        public int GetProtectedLength(int plaintextLength) => protectedLength;

        public int GetMaxPlaintextLength(int protectedLength) => throw new NotSupportedException();

        public void Protect<TWriter>(
            ReadOnlySpan<byte> plaintext,
            SecretProtectionContext context,
            ref TWriter destination
        )
            where TWriter : IBufferWriter<char>, allows ref struct
        {
            output.CopyTo(destination.GetSpan(output.Length));
            destination.Advance(output.Length);
        }

        public void Unprotect<TWriter>(
            ReadOnlySpan<char> protectedValue,
            SecretProtectionContext context,
            ref TWriter destination
        )
            where TWriter : IBufferWriter<byte>, allows ref struct =>
            throw new NotSupportedException();

        public void Rewrap<TWriter>(
            ReadOnlySpan<char> protectedValue,
            SecretProtectionContext context,
            ref TWriter destination
        )
            where TWriter : IBufferWriter<char>, allows ref struct
        {
            output.CopyTo(destination.GetSpan(output.Length));
            destination.Advance(output.Length);
        }
    }

    #region ProtectToString Tests

    [Fact]
    public void ProtectToString_ProtectorFillsLength_ReturnsWrittenCharacters()
    {
        var protector = new FixedOutputProtector(protectedLength: 5, output: "abcde");

        var result = protector.ProtectToString([1, 2, 3], Context);

        Assert.Equal("abcde", result);
    }

    [Fact]
    public void ProtectToString_ProtectorWritesFewerCharacters_Throws()
    {
        var protector = new FixedOutputProtector(protectedLength: 5, output: "abcd");

        Assert.Throws<InvalidOperationException>(() =>
            protector.ProtectToString([1, 2, 3], Context)
        );
    }

    [Fact]
    public void ProtectToString_NullProtector_Throws()
    {
        ISecretProtector protector = null!;

        Assert.Throws<ArgumentNullException>(() => protector.ProtectToString([1, 2, 3], Context));
    }

    #endregion

    #region RewrapToString Tests

    [Fact]
    public void RewrapToString_ProtectorFillsLength_ReturnsWrittenCharacters()
    {
        var protector = new FixedOutputProtector(protectedLength: 5, output: "vwxyz");

        var result = protector.RewrapToString("abcde", Context);

        Assert.Equal("vwxyz", result);
    }

    [Fact]
    public void RewrapToString_ProtectorWritesFewerCharacters_Throws()
    {
        var protector = new FixedOutputProtector(protectedLength: 5, output: "abcd");

        Assert.Throws<InvalidOperationException>(() => protector.RewrapToString("abcde", Context));
    }

    [Fact]
    public void RewrapToString_NullProtector_Throws()
    {
        ISecretProtector protector = null!;

        Assert.Throws<ArgumentNullException>(() => protector.RewrapToString("abcde", Context));
    }

    #endregion
}
