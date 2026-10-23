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
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the ability to protect and unprotect values for a single <see cref="SecretOwner"/>. Both operations are
/// synchronous because the master keys of the owner are already loaded when the protector is obtained, and both write
/// their result to a caller-supplied buffer writer so that the caller decides how the memory is allocated.
/// </summary>
// See ADR-0228.
[PublicAPI]
public interface ISecretProtector
{
    /// <summary>
    /// Gets the owner whose master keys protect the values.
    /// </summary>
    SecretOwner Owner { get; }

    /// <summary>
    /// Gets the exact number of characters that <see cref="Protect{TWriter}"/> writes for a plaintext of a given
    /// length.
    /// </summary>
    /// <param name="plaintextLength">The length of the plaintext in bytes.</param>
    /// <returns>The number of characters in the protected value.</returns>
    int GetProtectedLength(int plaintextLength);

    /// <summary>
    /// Gets an upper bound on the number of bytes that <see cref="Unprotect{TWriter}"/> writes for a protected value
    /// of a given length.
    /// </summary>
    /// <param name="protectedLength">The length of the protected value in characters.</param>
    /// <returns>
    /// The largest plaintext length in bytes that a protected value of the given length can hold, or zero when no
    /// protected value has that length.
    /// </returns>
    int GetMaxPlaintextLength(int protectedLength);

    /// <summary>
    /// Protects a value so that it can only be unprotected for the same owner and the same
    /// <see cref="SecretProtectionContext"/>, and writes the protected value to a destination. This method is
    /// synchronous because the master keys of the owner are already loaded.
    /// </summary>
    /// <remarks>
    /// Exactly <see cref="GetProtectedLength"/> characters are written to <paramref name="destination"/>. The
    /// protected value is not secret, so the destination does not need to be a sensitive buffer.
    /// </remarks>
    /// <typeparam name="TWriter">The type of the buffer writer that receives the protected value.</typeparam>
    /// <param name="plaintext">The value to protect.</param>
    /// <param name="context">The purpose of the value and the identifier of the item that the value belongs to.</param>
    /// <param name="destination">The buffer writer that receives the protected value, as characters.</param>
    void Protect<TWriter>(
        ReadOnlySpan<byte> plaintext,
        SecretProtectionContext context,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<char>, allows ref struct;

    /// <summary>
    /// Unprotects a value that was protected by <see cref="Protect{TWriter}"/> and writes the plaintext to a
    /// destination. This method is synchronous because the master keys of the owner are already loaded.
    /// </summary>
    /// <remarks>
    /// The destination is advanced only after the value has been authenticated. A caller that unprotects a secret
    /// passes a sensitive buffer writer so that the plaintext is zeroed when the buffer is released.
    /// </remarks>
    /// <typeparam name="TWriter">The type of the buffer writer that receives the plaintext.</typeparam>
    /// <param name="protectedValue">The protected value to unprotect.</param>
    /// <param name="context">The purpose of the value and the identifier of the item that the value belongs to.</param>
    /// <param name="destination">The buffer writer that receives the plaintext, as bytes.</param>
    /// <exception cref="CryptographicException">
    /// Thrown when the value was not protected for this owner and context, or when the value was altered.
    /// </exception>
    void Unprotect<TWriter>(
        ReadOnlySpan<char> protectedValue,
        SecretProtectionContext context,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<byte>, allows ref struct;
}
