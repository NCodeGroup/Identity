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
using System.Text;
using NCode.Buffers;

namespace NCode.Cryptography.Keys;

/// <summary>
/// Provides a <see cref="SecretKey"/> implementation for <c>symmetric</c> cryptographic keys.
/// </summary>
/// <remarks>
/// This class stores the key material in unmanaged memory so that it is pinned (cannot be moved/copied by the GC).
/// </remarks>
public class SymmetricSecretKey : SecretKey
{
    private IMemoryOwner<byte> MemoryOwner { get; }

    /// <inheritdoc />
    public override int KeySizeBits => KeySizeBytes << 3;

    /// <inheritdoc />
    public override int KeySizeBytes => MemoryOwner.Memory.Length;

    /// <summary>
    /// Gets the key material.
    /// </summary>
    public ReadOnlySpan<byte> KeyBytes => MemoryOwner.Memory.Span;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymmetricSecretKey"/> class by coping the specified key bytes.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="keyBytes">The cryptographic material for the secret key.</param>
    public SymmetricSecretKey(string keyId, ReadOnlySpan<byte> keyBytes)
        : base(keyId)
    {
        MemoryOwner = new HeapMemoryManager(keyBytes.Length);
        try
        {
            keyBytes.CopyTo(MemoryOwner.Memory.Span);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SymmetricSecretKey"/> class from the specified password.
    /// </summary>
    /// <param name="keyId">The <c>Key ID (KID)</c> for the secret key.</param>
    /// <param name="password">The password that will be decoded using UTF8 and used as the cryptographic material for the secret key.</param>
    public SymmetricSecretKey(string keyId, ReadOnlySpan<char> password)
        : base(keyId)
    {
        var byteCount = Encoding.UTF8.GetByteCount(password);
        MemoryOwner = new HeapMemoryManager(byteCount);
        try
        {
            Encoding.UTF8.GetBytes(password, MemoryOwner.Memory.Span);
        }
        catch
        {
            Dispose();
            throw;
        }
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
