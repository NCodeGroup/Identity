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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts.Capabilities;

/// <summary>
/// Describes a single signature or key-management algorithm the server supports, so management clients can drive
/// the create-secret form's algorithm, use, and key-size inputs from server discovery rather than hard-coded
/// defaults (ADR-0067).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class AlgorithmCapabilityResource
{
    /// <summary>
    /// Gets the algorithm's <c>JWA</c> code (for example <c>RS256</c> or <c>RSA-OAEP</c>).
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the key use the algorithm implies (<c>sig</c> for signature algorithms, <c>enc</c> for key-management
    /// algorithms). See <see cref="NCode.Identity.Secrets.SecretKeyUses"/>.
    /// </summary>
    public required string Use { get; init; }

    /// <summary>
    /// Gets the secret-type discriminators (for example <c>rsa</c> or <c>ecc</c>) whose key material is compatible
    /// with this algorithm, so clients can pair the algorithm picker with the secret-type picker.
    /// </summary>
    public required IReadOnlyList<string> SecretTypes { get; init; }

    /// <summary>
    /// Gets the legal key sizes, in bits, for this algorithm as one or more inclusive ranges.
    /// </summary>
    public required IReadOnlyList<KeyBitSizesResource> KeyBitSizes { get; init; }
}
