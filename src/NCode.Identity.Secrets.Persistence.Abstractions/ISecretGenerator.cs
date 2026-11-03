// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the ability to produce a <see cref="PersistedSecret"/> whose stored value is protected under the owner's
/// master key. The <c>Generate…</c> methods create fresh material server-side, so private key material is never
/// supplied by the caller; the <c>Import…</c> methods persist a <strong>caller-supplied</strong> shared-secret value.
/// A shared secret (for example an OAuth <c>client_secret</c>) is stored in one of two forms, chosen when it is issued:
/// as a one-way verifier (<see cref="SecretEncodings.Verifier"/>) when it is only ever compared, or as its UTF-8 octets
/// (<see cref="SecretEncodings.SymmetricRaw"/>) when the server also uses it as a key, as <c>client_secret_jwt</c>
/// uses it as an HMAC key.
/// </summary>
// See ADR-1110 and ADR-1391.
[PublicAPI]
public interface ISecretGenerator
{
    /// <summary>
    /// Generates fresh key material for the requested secret type and size, protects it under the owner's master key
    /// with the specified <paramref name="protector"/>, and returns a <see cref="GeneratedSecret"/> whose
    /// <see cref="GeneratedSecret.Secret"/> is ready to be persisted. The returned secret's
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is left unset for the store to assign.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="GenerateSecretRequest"/> describing the secret to generate.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with protected key material and, for symmetric secrets,
    /// the one-time unprotected material to reveal.</returns>
    GeneratedSecret GenerateSecret(ISecretProtector protector, GenerateSecretRequest request);

    /// <summary>
    /// Generates an opaque, URL-safe shared-secret string that is only ever compared (for example the
    /// <c>client_secret</c> of a <c>client_secret_basic</c> or <c>client_secret_post</c> client) and returns it as the
    /// one-time <see cref="GeneratedSecret.SecretMaterial"/>, alongside a <see cref="PersistedSecret"/> that stores
    /// only its one-way verifier, made by the scheme configured for generated secrets (by default
    /// <see cref="SecretVerifierSchemes.Sha256"/>), protected under the owner's master key with the specified
    /// <paramref name="protector"/>. The secret cannot be read back from what is stored. The
    /// request's <see cref="GenerateSecretRequest.KeySizeBits"/> sets the random entropy; it must be a multiple of 8
    /// and at least <see cref="SharedSecretPolicy.MinimumEntropyBits"/>; its
    /// <see cref="GenerateSecretRequest.SecretType"/> must be <c>Symmetric</c>.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="GenerateSecretRequest"/> describing the shared secret to generate.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with the protected verifier and the one-time opaque secret string to
    /// reveal.</returns>
    GeneratedSecret GenerateOneWaySharedSecret(
        ISecretProtector protector,
        GenerateSecretRequest request
    );

    /// <summary>
    /// Generates an opaque, URL-safe shared-secret string that the server also uses as a key (for example the
    /// <c>client_secret</c> of a <c>client_secret_jwt</c> client, its HMAC key) and returns it as the one-time
    /// <see cref="GeneratedSecret.SecretMaterial"/>, alongside a <see cref="PersistedSecret"/> whose stored symmetric
    /// key material is the <strong>UTF-8 bytes of that string</strong>, protected under the owner's master key with the
    /// specified <paramref name="protector"/>. The request is checked as
    /// <see cref="GenerateOneWaySharedSecret(ISecretProtector, GenerateSecretRequest)"/> checks it.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="GenerateSecretRequest"/> describing the shared secret to generate.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with the protected UTF-8 key material and the one-time opaque
    /// secret string to reveal.</returns>
    GeneratedSecret GenerateReversibleSharedSecret(
        ISecretProtector protector,
        GenerateSecretRequest request
    );

    /// <summary>
    /// Persists a <strong>caller-supplied</strong> shared-secret value that is only ever compared (for example the
    /// <c>client_secret</c> an operator chose for a <c>client_secret_basic</c> client) as a <see cref="PersistedSecret"/>
    /// that stores only its one-way verifier, made by the scheme configured for supplied secrets (by default the
    /// PBKDF2-HMAC-SHA256 <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/>), protected under the owner's master key
    /// with the specified <paramref name="protector"/>. Nothing is revealed, because the
    /// caller already holds the value. The secret's bit length is derived from <paramref name="secretValue"/>.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="ImportSharedSecretRequest"/> describing the secret's metadata.</param>
    /// <param name="secretValue">The UTF-8 octets of the opaque shared-secret string. Must not be empty.</param>
    /// <returns>The imported <see cref="PersistedSecret"/> with the protected verifier.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="secretValue"/> is empty.</exception>
    PersistedSecret ImportOneWaySharedSecret(
        ISecretProtector protector,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    );

    /// <summary>
    /// Persists a <strong>caller-supplied</strong> shared-secret value that the server also uses as a key (for example
    /// the <c>client_secret</c> an operator chose for a <c>client_secret_jwt</c> client) as a <c>Symmetric</c>
    /// <see cref="PersistedSecret"/> whose stored key material is exactly the supplied UTF-8 octets, protected under the
    /// owner's master key with the specified <paramref name="protector"/>, in the shape
    /// <see cref="GenerateReversibleSharedSecret(ISecretProtector, GenerateSecretRequest)"/> produces. The secret's bit
    /// length is derived from <paramref name="secretValue"/>.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="ImportSharedSecretRequest"/> describing the secret's metadata.</param>
    /// <param name="secretValue">The UTF-8 octets of the opaque shared-secret string. Must not be empty.</param>
    /// <returns>The imported <see cref="PersistedSecret"/> with the protected UTF-8 key material.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="secretValue"/> is empty.</exception>
    PersistedSecret ImportReversibleSharedSecret(
        ISecretProtector protector,
        ImportSharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    );
}
