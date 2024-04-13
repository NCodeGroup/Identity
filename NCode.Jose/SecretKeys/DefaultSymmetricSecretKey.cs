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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a default implementation for the <see cref="SymmetricSecretKey"/> abstraction.
/// </summary>
public class DefaultSymmetricSecretKey : SymmetricSecretKey
{
    /// <inheritdoc />
    public override KeyMetadata Metadata { get; }

    /// <inheritdoc />
    public override int KeySizeBits => KeySizeBytes << 3;

    /// <inheritdoc />
    public override int KeySizeBytes { get; }

    /// <inheritdoc />
    public override ReadOnlySpan<byte> KeyBytes => MemoryOwner.Memory.Span[..KeySizeBytes];

    private IMemoryOwner<byte> MemoryOwner { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSymmetricSecretKey"/> class with the specified cryptographic material.
    /// </summary>
    /// <param name="metadata">The metadata for the secret key.</param>
    /// <param name="bytes">The cryptographic material for the secret key.</param>
    /// <param name="byteCount">The size of the cryptographic material in bytes.</param>
    public DefaultSymmetricSecretKey(KeyMetadata metadata, IMemoryOwner<byte> bytes, int byteCount)
    {
        Metadata = metadata;
        KeySizeBytes = byteCount;
        MemoryOwner = bytes;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!disposing) return;
        MemoryOwner.Dispose();
    }
}
