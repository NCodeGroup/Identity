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
    public SecretKey SecretKey { get; }

    public SignatureAlgorithmDescriptor AlgorithmDescriptor { get; }

    protected int HashBitLength => AlgorithmDescriptor.HashBitLength;

    protected int HashByteLength => HashBitLength / 8;

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
        if (signature.Length != HashByteLength)
            return false;

        var expected = GC.AllocateUninitializedArray<byte>(HashByteLength);
        if (!TrySign(input, expected.AsSpan(), out var bytesWritten))
            return false;

        if (bytesWritten != HashByteLength)
            return false;

        return signature.SequenceEqual(expected);
    }
}
