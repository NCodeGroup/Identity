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

namespace NCode.Identity.OpenId.Management.Contracts.Secrets;

/// <summary>
/// Represents the request body to create a new server secret. The key material is generated server-side; the
/// caller never supplies it. See ADR-0011.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class CreateSecretRequest
{
    /// <summary>
    /// Gets the optional natural identifier (aka <c>kid</c>) to assign to the secret. When <c>null</c> or empty,
    /// the server generates one.
    /// </summary>
    public string? SecretId { get; init; }

    /// <summary>
    /// Gets the type of secret to generate. Supported values are <c>symmetric</c>, <c>rsa</c>, and <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key material to generate.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the intended use for the secret, or <c>null</c> for any compatible use.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
