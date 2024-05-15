#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NCode.Identity.DataProtection;

/// <summary>
/// An abstraction that can provide data protection services using the new <c>Span</c> API.
/// </summary>
public interface ISecureDataProtector : ISecureDataProtectionProvider
{
    /// <summary>
    /// Cryptographically protects a piece of plaintext data.
    /// </summary>
    /// <param name="plaintext">The plaintext data to protect.</param>
    /// <returns>The protected form of the plaintext data.</returns>
    byte[] Protect(ReadOnlySpan<byte> plaintext);

    /// <summary>
    /// Cryptographically unprotects a piece of protected data.
    /// </summary>
    /// <param name="protectedBytes">The protected data to unprotect.</param>
    /// <param name="plaintext">Destination for the plaintext data of the protected data.</param>
    /// <param name="bytesWritten">The number of bytes written to <paramref name="plaintext"/>.</param>
    /// <returns><c>true></c> if there was enough room in <paramref name="plaintext"/> to copy all bytes; otherwise, <c>false</c>.</returns>
    bool TryUnprotect(
        byte[] protectedBytes,
        Span<byte> plaintext,
        out int bytesWritten);
}
