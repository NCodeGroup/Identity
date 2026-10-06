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
using NCode.Identity.OpenId.Management.Contracts.Secrets;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Validates the intrinsic coherence of a secret-create request — the <c>secretType</c>,
/// <c>keySizeBits</c>, <c>use</c>, and <c>algorithm</c> parameters must form a compatible combination.
/// Register a custom implementation to replace or extend the default validation.
/// </summary>
/// <remarks>
/// This validator asserts only the parameter-level coherence that is independent of the owning
/// server/tenant/client; authorization and existence preconditions remain the responsibility of the
/// calling endpoint.
/// </remarks>
[PublicAPI]
public interface ISecretValidator
{
    /// <summary>
    /// Validates that the specified <see cref="CreateSecretRequest"/> describes a coherent secret.
    /// </summary>
    /// <param name="request">The <see cref="CreateSecretRequest"/> to validate.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed check, or <c>null</c> when valid.</returns>
    ManagementError? ValidateCreate(CreateSecretRequest request);
}
