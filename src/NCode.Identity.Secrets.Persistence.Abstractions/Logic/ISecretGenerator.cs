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
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Logic;

/// <summary>
/// Provides the ability to generate a new <see cref="PersistedSecret"/> whose key material is created
/// server-side and data-protected, so that private key material is never supplied by, or returned to, the caller.
/// </summary>
[PublicAPI]
public interface ISecretGenerator
{
    /// <summary>
    /// Generates fresh key material for the requested secret type and size, data-protects it, and returns a
    /// <see cref="GeneratedSecret"/> whose <see cref="GeneratedSecret.Secret"/> is ready to be persisted. The
    /// returned secret's <see cref="PersistedSecret.ConcurrencyToken"/> is left unset for the store to assign.
    /// </summary>
    /// <param name="request">The <see cref="GenerateSecretRequest"/> describing the secret to generate.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with data-protected key material and, for symmetric secrets,
    /// the one-time unprotected material to reveal.</returns>
    GeneratedSecret GenerateSecret(GenerateSecretRequest request);
}
