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

using System.Buffers;
using System.Security.Cryptography;
using NIdentity.OpenId.Cryptography.Binary;
using NIdentity.OpenId.Cryptography.Descriptors;

namespace NIdentity.OpenId.Cryptography;

public class SharedSecretKey : SecretKey
{
    public static RandomNumberGenerator RandomNumberGenerator { get; } = RandomNumberGenerator.Create();

    public static SharedSecretKey GenerateNewKey(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        var keySizeBytes = GetKeySizeBytes(descriptor, keyBitLengthHint);

        IMemoryOwner<byte> memoryOwner = new HeapMemoryManager(keySizeBytes);
        try
        {
            RandomNumberGenerator.GetBytes(memoryOwner.Memory.Span);
            return new SharedSecretKey(memoryOwner);
        }
        catch
        {
            memoryOwner.Dispose();
            throw;
        }
    }

    private static int GetKeySizeBytes(AlgorithmDescriptor descriptor, int? keyBitLengthHint = default)
    {
        int keySizeBits;
        if (descriptor is ISupportKeySizes supportKeySizes)
        {
            keySizeBits = KeySizesUtility.GetLegalSize(keyBitLengthHint, supportKeySizes.KeySizes);
        }
        else
        {
            const int defaultSizeBits = 128;
            keySizeBits = keyBitLengthHint ?? defaultSizeBits;
        }

        return keySizeBits / BinaryUtility.BitsPerByte;
    }

    private IMemoryOwner<byte> MemoryOwner { get; }

    public int KeyByteLength => MemoryOwner.Memory.Length;

    public ReadOnlySpan<byte> KeyBytes => MemoryOwner.Memory.Span;

    public SharedSecretKey(IMemoryOwner<byte> memoryOwner)
    {
        MemoryOwner = memoryOwner;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MemoryOwner.Dispose();
        }

        base.Dispose(disposing);
    }
}
