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

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the ability to produce a data-protected <see cref="PersistedSecret"/>. The <c>Generate…</c> methods
/// create fresh key material server-side so that private key material is never supplied by the caller; the
/// <see cref="ImportSharedSecret"/> method persists a <strong>caller-supplied</strong> shared-secret value in the
/// same symmetric shape as <see cref="GenerateSharedSecret"/>, so the two share a single correct-by-default storage
/// contract that cannot drift (see ADR-0110).
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

    /// <summary>
    /// Generates an opaque, URL-safe shared-secret string (for example an OAuth <c>client_secret</c>) and returns it
    /// as the one-time <see cref="GeneratedSecret.SecretMaterial"/>, alongside a data-protected
    /// <see cref="PersistedSecret"/> whose stored symmetric key material is the <strong>UTF-8 bytes of that string</strong>.
    /// Unlike <see cref="GenerateSecret"/> — which stores raw CSPRNG bytes for cryptographic keys and reveals their
    /// base64url form — this stores exactly what a verifier compares against (the UTF-8 bytes of the presented secret),
    /// so the generated secret authenticates round-trip. The request's <see cref="GenerateSecretRequest.KeySizeBits"/>
    /// sets the random entropy; its <see cref="GenerateSecretRequest.SecretType"/> must be <c>Symmetric</c>.
    /// </summary>
    /// <param name="request">The <see cref="GenerateSecretRequest"/> describing the shared secret to generate.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with the data-protected UTF-8 key material and the one-time opaque
    /// secret string to reveal.</returns>
    GeneratedSecret GenerateSharedSecret(GenerateSecretRequest request);

    /// <summary>
    /// Persists a <strong>caller-supplied</strong> shared-secret value (for example an OAuth <c>client_secret</c> the
    /// operator chose) as a data-protected <see cref="PersistedSecret"/> in the identical shape
    /// <see cref="GenerateSharedSecret"/> produces: a <c>Symmetric</c> secret whose stored key material is exactly the
    /// supplied UTF-8 octets, so a verifier's UTF-8 comparison of the presented secret matches. Because the caller
    /// already holds the value, nothing is revealed — the method returns the ready-to-persist secret directly rather
    /// than a <see cref="GeneratedSecret"/>. The secret's bit length is derived from <paramref name="secretValue"/>.
    /// </summary>
    /// <param name="request">The <see cref="ImportSharedSecretRequest"/> describing the secret's metadata.</param>
    /// <param name="secretValue">The UTF-8 octets of the opaque shared-secret string. Must not be empty.</param>
    /// <returns>The imported <see cref="PersistedSecret"/> with the data-protected UTF-8 key material.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="secretValue"/> is empty.</exception>
    PersistedSecret ImportSharedSecret(
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    );
}
