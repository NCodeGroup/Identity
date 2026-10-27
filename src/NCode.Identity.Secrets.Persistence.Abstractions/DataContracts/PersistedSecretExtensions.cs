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

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Provides extension methods for <see cref="PersistedSecret"/>.
/// </summary>
// See ADR-0268.
[PublicAPI]
public static class PersistedSecretExtensions
{
    /// <param name="secret">The <see cref="PersistedSecret"/> to inspect.</param>
    extension(PersistedSecret secret)
    {
        /// <summary>
        /// Determines whether the secret's public key can be read without a protector.
        /// </summary>
        /// <returns><c>true</c> when the public key can be read without a protector; otherwise, <c>false</c>.</returns>
        [PublicAPI]
        public bool HasPublicKey()
        {
            ArgumentNullException.ThrowIfNull(secret);

            return SecretEncodings.IsPublic(secret.SecretEncoding) || secret.PublicKey is not null;
        }
    }
}
