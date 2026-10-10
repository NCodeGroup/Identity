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

using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Converts a persisted secret into its public key material (JWK and PEM) for the public-key read endpoints,
/// returning <c>null</c> for a symmetric secret that has no public key to expose.
/// </summary>
internal interface ISecretPublicKeyConverter
{
    /// <summary>
    /// Converts the specified persisted secret into its <see cref="PublicKeyResource"/>, or returns <c>null</c> when
    /// the secret is symmetric and therefore has no public key material.
    /// </summary>
    /// <param name="secret">The persisted secret to convert.</param>
    /// <returns>The <see cref="PublicKeyResource"/> for an asymmetric secret, or <c>null</c> when the secret has no
    /// public key material.</returns>
    PublicKeyResource? Convert(PersistedSecret secret);
}
