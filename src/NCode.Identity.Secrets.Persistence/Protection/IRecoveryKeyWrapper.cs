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

/// <summary>
/// Provides the ability to wrap a master key under the offline recovery public key, so the stored secrets can still
/// be read after the root key is lost. Only the public half of the recovery key is ever deployed; the private half
/// is kept offline.
/// </summary>
// See ADR-0235.
internal interface IRecoveryKeyWrapper
{
    /// <summary>
    /// Gets the identifier of the recovery public key, which is the base64url-encoded SHA-256 digest of the
    /// SubjectPublicKeyInfo of the key, or <see langword="null"/> when no recovery key is configured.
    /// </summary>
    string? RecoveryKeyId { get; }

    /// <summary>
    /// Gets the number of characters that <see cref="Wrap{TWriter}"/> writes.
    /// </summary>
    /// <returns>
    /// The length of the base64url-encoded wrapped key, or zero when no recovery key is configured.
    /// </returns>
    int GetWrappedLength();

    /// <summary>
    /// Wraps a master key under the recovery public key with RSA-OAEP and SHA-256, and writes the base64url text of
    /// the wrapped key to a writer.
    /// </summary>
    /// <typeparam name="TWriter">The type of the writer that receives the wrapped key.</typeparam>
    /// <param name="masterKey">The master key to wrap.</param>
    /// <param name="destination">
    /// The writer that receives exactly <see cref="GetWrappedLength"/> characters.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no recovery key is configured, or when <paramref name="destination"/> does not provide the
    /// requested space.
    /// </exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">
    /// Thrown when the master key cannot be wrapped under the recovery public key.
    /// </exception>
    void Wrap<TWriter>(ReadOnlySpan<byte> masterKey, ref TWriter destination)
        where TWriter : IBufferWriter<char>, allows ref struct;
}
