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

namespace NCode.Identity.Secrets.Persistence.Protection;

public class SecretProtectorExtensionsTests
{
    private static readonly SecretProtectionContext Context = new("test/purpose", "item-1");

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
}
