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
/// Describes a single secret type the server understands and the capabilities it exposes, so that management
/// clients can drive their UI (for example which types can be created, or which expose an exportable public key)
/// from server discovery rather than hard-coded assumptions (ADR-0067).
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SecretTypeCapabilityResource
{
    /// <summary>
    /// Gets the secret-type discriminator (for example <c>symmetric</c>, <c>rsa</c>, or <c>ecc</c>).
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets a value indicating whether this secret type uses asymmetric cryptography.
    /// </summary>
    public required bool IsAsymmetric { get; init; }

    /// <summary>
    /// Gets a value indicating whether secrets of this type expose exportable public-key material (JWK and PEM).
    /// </summary>
    public required bool SupportsPublicKeyExport { get; init; }
}
