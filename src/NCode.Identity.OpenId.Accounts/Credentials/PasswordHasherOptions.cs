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
/// Contains options for the default PBKDF2 password hasher.
/// </summary>
[PublicAPI]
public sealed class PasswordHasherOptions
{
    /// <summary>
    /// Gets or sets the number of PBKDF2 iterations. The default is <c>600000</c>, the OWASP recommendation for
    /// PBKDF2-HMAC-SHA256. A stored hash produced with fewer iterations than this is flagged for rehashing on the next
    /// successful verification.
    /// </summary>
    public int Iterations { get; set; } = 600_000;

    /// <summary>
    /// Gets or sets the length, in bytes, of the randomly-generated salt. The default is <c>16</c>.
    /// </summary>
    public int SaltByteCount { get; set; } = 16;

    /// <summary>
    /// Gets or sets the length, in bytes, of the derived hash. The default is <c>32</c>.
    /// </summary>
    public int HashByteCount { get; set; } = 32;
}
