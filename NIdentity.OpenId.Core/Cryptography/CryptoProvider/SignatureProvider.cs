#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography.CryptoProvider;

public abstract class SignatureProvider : IDisposable
{
    private SecretKey SecretKey { get; }

    protected SignatureAlgorithmDescriptor AlgorithmDescriptor { get; }

    protected SignatureProvider(SecretKey secretKey, SignatureAlgorithmDescriptor descriptor)
    {
        SecretKey = secretKey;
        AlgorithmDescriptor = descriptor;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        // nothing
    }

    public abstract bool TrySign(ReadOnlySpan<byte> input, Span<byte> signature, out int bytesWritten);

    public virtual bool Verify(ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature)
    {
        var hashByteLength = AlgorithmDescriptor.HashBitLength;
        if (signature.Length != hashByteLength)
            return false;

        var expected = hashByteLength <= BinaryUtility.StackAllocMax ?
            stackalloc byte[hashByteLength] :
            new byte[hashByteLength];

        if (!TrySign(input, expected, out var bytesWritten))
            return false;

        if (bytesWritten != hashByteLength)
            return false;

        return signature.SequenceEqual(expected);
    }
}
