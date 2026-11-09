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
/// <see cref="BindRemoteSecretAsync"/> records a key that a key management service already holds.
/// A shared secret (for example an OAuth <c>client_secret</c>) is stored in one of two forms: as a one-way verifier
/// (<see cref="SecretEncodings.Verifier"/>) when it is only ever compared, or as its UTF-8 octets
/// (<see cref="SecretEncodings.SymmetricRaw"/>) when the server also uses it as a key, as <c>client_secret_jwt</c>
/// uses it as an HMAC key. Every secret records its <see cref="PersistedSecret.Origin"/>.
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
    /// <paramref name="protector"/>. The secret cannot be read back from what is stored.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="SharedSecretRequest"/> describing the shared secret to generate, whose
    /// <see cref="SharedSecretRequest.EntropyBits"/> sets the random entropy.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with the protected verifier and the one-time opaque secret string to
    /// reveal.</returns>
    /// <exception cref="System.ArgumentException">The entropy is not a multiple of 8 or is below
    /// <see cref="SharedSecretPolicy.MinimumEntropyBits"/>.</exception>
    GeneratedSecret GenerateSharedSecretVerifier(
        ISecretProtector protector,
        SharedSecretRequest request
    );

    /// <summary>
    /// Generates an opaque, URL-safe shared-secret string that the server also uses as a key (for example the
    /// <c>client_secret</c> of a <c>client_secret_jwt</c> client, its HMAC key) and returns it as the one-time
    /// <see cref="GeneratedSecret.SecretMaterial"/>, alongside a <see cref="PersistedSecret"/> whose stored symmetric
    /// key material is the <strong>UTF-8 bytes of that string</strong>, protected under the owner's master key with the
    /// specified <paramref name="protector"/>.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="SharedSecretRequest"/> describing the shared secret to generate, whose
    /// <see cref="SharedSecretRequest.EntropyBits"/> sets the random entropy.</param>
    /// <returns>The <see cref="GeneratedSecret"/> with the protected UTF-8 key material and the one-time opaque
    /// secret string to reveal.</returns>
    /// <exception cref="System.ArgumentException">The entropy is not a multiple of 8 or is below
    /// <see cref="SharedSecretPolicy.MinimumEntropyBits"/>.</exception>
    GeneratedSecret GenerateReversibleSharedSecret(
        ISecretProtector protector,
        SharedSecretRequest request
    );

    /// <summary>
    /// Persists a <strong>caller-supplied</strong> shared-secret value that is only ever compared (for example the
    /// <c>client_secret</c> an operator chose for a <c>client_secret_basic</c> client) as a <see cref="PersistedSecret"/>
    /// that stores only its one-way verifier, made by the scheme configured for supplied secrets (by default the
    /// PBKDF2-HMAC-SHA256 <see cref="SecretVerifierSchemes.Pbkdf2Sha256"/>), protected under the owner's master key
    /// with the specified <paramref name="protector"/>. Nothing is revealed, because the caller already holds the
    /// value. The secret's bit length is derived from <paramref name="secretValue"/>.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="SharedSecretRequest"/> describing the secret's metadata.</param>
    /// <param name="secretValue">The UTF-8 octets of the opaque shared-secret string. Must not be empty.</param>
    /// <returns>The imported <see cref="PersistedSecret"/> with the protected verifier.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="secretValue"/> is empty.</exception>
    PersistedSecret ImportSharedSecretVerifier(
        ISecretProtector protector,
        SharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    );

    /// <summary>
    /// Persists a <strong>caller-supplied</strong> shared-secret value that the server also uses as a key (for example
    /// the <c>client_secret</c> an operator chose for a <c>client_secret_jwt</c> client) as a <c>Symmetric</c>
    /// <see cref="PersistedSecret"/> whose stored key material is exactly the supplied UTF-8 octets, protected under the
    /// owner's master key with the specified <paramref name="protector"/>, in the shape
    /// <see cref="GenerateReversibleSharedSecret(ISecretProtector, SharedSecretRequest)"/> produces. The secret's bit
    /// length is derived from <paramref name="secretValue"/>.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret.</param>
    /// <param name="request">The <see cref="SharedSecretRequest"/> describing the secret's metadata.</param>
    /// <param name="secretValue">The UTF-8 octets of the opaque shared-secret string. Must not be empty.</param>
    /// <returns>The imported <see cref="PersistedSecret"/> with the protected UTF-8 key material.</returns>
    /// <exception cref="System.ArgumentException"><paramref name="secretValue"/> is empty.</exception>
    PersistedSecret ImportReversibleSharedSecret(
        ISecretProtector protector,
        SharedSecretRequest request,
        ReadOnlySpan<byte> secretValue
    );

    /// <summary>
    /// Stores the one-way verifier of a shared secret that is stored as its UTF-8 octets, as a new
    /// <see cref="PersistedSecret"/> under the specified identifier that keeps the secret's use, algorithm, dates, size
    /// and origin. The verifier is made by the scheme configured for the secret's
    /// <see cref="PersistedSecret.Origin"/>: the scheme for generated secrets when the server generated it, and the
    /// scheme for supplied secrets otherwise. The secret that is converted is left as it is, for the caller to remove.
    /// </summary>
    /// <param name="protector">The <see cref="ISecretProtector"/> for the owner of the secret, which unprotects the
    /// stored octets and protects the verifier.</param>
    /// <param name="secret">The shared secret stored as its UTF-8 octets
    /// (<see cref="SecretEncodings.SymmetricRaw"/>).</param>
    /// <param name="secretId">The natural identifier to assign to the verifier.</param>
    /// <returns>The <see cref="PersistedSecret"/> with the protected verifier.</returns>
    /// <exception cref="System.InvalidOperationException"><paramref name="secret"/> is not a symmetric secret stored
    /// as its octets.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The stored octets cannot be
    /// unprotected.</exception>
    PersistedSecret ConvertToSharedSecretVerifier(
        ISecretProtector protector,
        PersistedSecret secret,
        string secretId
    );

    /// <summary>
    /// Creates a key in the key management service that the request names, reads its public half, and destroys
    /// the key when anything after its creation fails, and returns a <see cref="PersistedSecret"/> that stores the
    /// public key and a reference to the key. The secret takes the use of its algorithm row. The private key never
    /// leaves the service, so nothing is protected under a master key. The returned secret's
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is left unset for the store to assign.
    /// </summary>
    /// <param name="request">The <see cref="GenerateRemoteSecretRequest"/> describing the secret to generate.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="PersistedSecret"/> with the <see cref="SecretEncodings.Remote"/> encoding.</returns>
    /// <exception cref="RemoteSecretRefusedException">The secret type does not support remote keys, the provider
    /// is not registered, no algorithm row matches the request, or the key the service created does not
    /// have the requested size or curve. No key is created for the first three; for the last, and for
    /// any other failure after the service creates the key, the key is destroyed.</exception>
    /// <exception cref="System.InvalidOperationException">The secret protection options name no deployment, which every
    /// created key carries. No key is created.</exception>
    // See ADR-1471.
    ValueTask<PersistedSecret> GenerateRemoteSecretAsync(
        GenerateRemoteSecretRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Reads the public half of a key that the key management service the request names already holds, checks
    /// that it is a key of the type, size and curve of the request's algorithm row, and returns a
    /// <see cref="PersistedSecret"/> that stores the public key and a reference to the key, with the use of the row,
    /// <see cref="PersistedSecret.Origin"/> <see cref="SecretOrigins.Supplied"/> and no
    /// <see cref="PersistedSecret.ManagedBy"/>, so the server never destroys the key. The returned secret's
    /// <see cref="PersistedSecret.ConcurrencyToken"/> is left unset for the store to assign.
    /// </summary>
    /// <param name="request">The <see cref="BindRemoteSecretRequest"/> describing the key to bind.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="PersistedSecret"/> with the <see cref="SecretEncodings.Remote"/> encoding.</returns>
    /// <exception cref="RemoteSecretRefusedException">The secret type does not support remote keys, the provider is
    /// not registered, no algorithm row matches the request, or the public half the service returns cannot
    /// be read as a key of the secret type or does not have the algorithm's size or curve.</exception>
    /// <exception cref="System.ArgumentException">The request's key identifier is empty, or the provider does not read
    /// it.</exception>
    /// <exception cref="System.Security.Cryptography.CryptographicException">The provider refuses the
    /// key.</exception>
    // See ADR-1476.
    ValueTask<PersistedSecret> BindRemoteSecretAsync(
        BindRemoteSecretRequest request,
        CancellationToken cancellationToken
    );
}
