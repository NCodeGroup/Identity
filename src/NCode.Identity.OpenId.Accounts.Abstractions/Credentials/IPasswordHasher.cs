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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Accounts.Credentials;

/// <summary>
/// Provides a facade for hashing and verifying account passwords, decoupling the account stack from any particular
/// hashing implementation. A default modern implementation ships in the core accounts package; a host may substitute
/// another (such as one backed by ASP.NET Core Identity's <c>PasswordHasher&lt;TUser&gt;</c>) by registering its own.
/// </summary>
/// <remarks>
/// The password is accepted as a <see cref="ReadOnlySpan{T}">ReadOnlySpan&lt;byte&gt;</see> of its UTF-8 bytes so the
/// sensitive material stays in a caller-owned, zeroable buffer and never becomes a managed <see cref="string"/>. The
/// operations are synchronous because password hashing is CPU-bound work with no I/O. An implementation hashes and
/// verifies the Unicode Normalization Form C (NFC) of the password, so the same characters typed with another
/// composition verify. When the NFC form does not verify and differs from the bytes given, it verifies the bytes as
/// given and reports <see cref="PasswordVerificationResult.SuccessRehashNeeded"/> on a match, so a stored hash of the
/// submitted form is replaced. A stored hash it cannot read fails after the work of verifying
/// <see cref="DummyPasswordHash"/>.
/// </remarks>
[PublicAPI]
public interface IPasswordHasher
{
    /// <summary>
    /// Gets a hash, in the format and with the current parameters of <see cref="HashPassword"/>, that no known password
    /// matches. A store verifies a submitted password against it when it holds no hash it may verify for the submitted
    /// user name, so a failed verification takes as long whether or not an account has that user name.
    /// </summary>
    string DummyPasswordHash { get; }

    /// <summary>
    /// Hashes the specified password, returning a self-describing hash string (carrying the algorithm and parameters)
    /// suitable for storage.
    /// </summary>
    /// <param name="password">The UTF-8 bytes of the password to hash.</param>
    /// <returns>The self-describing hash string.</returns>
    string HashPassword(ReadOnlySpan<byte> password);

    /// <summary>
    /// Verifies that the specified password matches the given hash, and indicates whether the stored hash should be
    /// upgraded (rehashed) because its parameters are weaker than the current configuration.
    /// </summary>
    /// <param name="hashedPassword">The previously stored hash string.</param>
    /// <param name="password">The UTF-8 bytes of the password to verify.</param>
    /// <returns>A <see cref="PasswordVerificationResult"/> describing the outcome.</returns>
    PasswordVerificationResult VerifyHashedPassword(
        string hashedPassword,
        ReadOnlySpan<byte> password
    );
}
