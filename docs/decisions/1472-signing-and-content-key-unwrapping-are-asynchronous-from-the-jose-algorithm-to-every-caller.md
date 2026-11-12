# 1472. Signing and content-key unwrapping are asynchronous from the JOSE algorithm to every caller

- **Status:** Accepted
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A signature or a content-key unwrap can be performed by a remote service
([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)): the private key is
in a key management service, and signing or decrypting is a network call that can wait, fail and be cancelled. A
primitive that fills a caller's span and returns cannot make that call without blocking its thread on a task, and
`RS0030` bans blocking on a task.

The encode path is span-based. `JoseSerializer` builds a JWS signing input from the encoded header and payload, and a
span cannot be held across an `await`. Every caller above it (the JWT encoder, DPoP proofs, SD-JWT issuance and
presentation, COSE signing and CWT issuance) reaches the signature algorithm through that path, so a signature that is
asynchronous at the bottom is asynchronous at every caller. The decode path is the mirror: a JWE decode reaches the key
management algorithm, and a remote decryption key makes the unwrap asynchronous at every decoder above it.

## Decision

**Signing and content-key unwrapping are asynchronous from the JOSE algorithm to every caller, and nothing synchronous
signs.**

1. **The signature seam is `SignatureAlgorithm.SignAsync`.** It takes the secret key, the signing input as
   `ReadOnlyMemory<byte>`, the destination as `Memory<byte>` and a `CancellationToken`, and returns a `ValueTask<int>`
   of the bytes written. For an `IRemoteSecretKey` it checks compatibility with the algorithm (throwing
   `JoseInvalidAlgorithmException`), awaits the key's `SignAsync`, and throws a `JoseException` unless the signature's
   length equals `GetSignatureSizeBytes` and `Verify` accepts it with the key's stored public key. For a local key it
   returns the count from `TrySign` as a completed `ValueTask`, with no state machine. `TrySign` stays the synchronous
   primitive for local material: a contributed algorithm implements `TrySign` and inherits `SignAsync`, and one that can
   sign remotely overrides it.
2. **`JoseEncoder` encodes only asynchronously.** Its members are `EncodeAsync` over a payload as `ReadOnlyMemory<byte>`,
   a `string`, a `ReadOnlyMemory<char>` or a `T`, each returning a `ValueTask<string>` or, with an `IBufferWriter<char>`
   first, writing to it. Nothing synchronous signs. `JoseSerializer` builds each JWS signing input into a pooled buffer
   held across the `await` (the `b64: false` form included), so no span crosses it. The JSON serializations compute
   every signature first, then write the JSON. A JWE encode completes synchronously behind its `ValueTask`.
3. **Every caller awaits with a `CancellationToken`.** `IJwtEncoder.EncodeAsync`, `IDPoPProofService.CreateProofAsync`,
   `ISdJwtIssuer.IssueAsync`, `ISdJwtHolder.CreatePresentationAsync`, `ICoseSerializer.EncodeSign1Async` and
   `EncodeSignAsync`, `ICwtIssuer.IssueAsync` and the protocol client's `DPoPProofKey.CreateProofAsync` each return a
   `ValueTask`.
4. **The unwrap seam is `KeyManagementAlgorithm.UnwrapKeyAsync`.** It takes the secret key, the header as a
   `JsonElement`, the encrypted content key as `ReadOnlyMemory<byte>`, the content key destination as `Memory<byte>` and
   a `CancellationToken`, and returns a `ValueTask<int>`. The default refuses a remote key and otherwise wraps
   `TryUnwrapKey`, which refuses a remote key itself. RSA-OAEP delegates to the remote key's `DecryptAsync`. `ECDH-ES`
   delegates only the agreement (the `epk` as SPKI) and runs the Concat KDF locally, and `ECDH-ES+A*KW` also unwraps
   with AES locally.
5. **Decoding is asynchronous.** `IJoseSerializer.DecodeAsync` and `DeserializeAsync` are the decode members. A JWS
   decode completes synchronously behind its `ValueTask`, and `VerifyJws` stays synchronous. Every binary segment is
   decoded before the unwrap, so a malformed token never reaches a service, and RSA-OAEP refuses an encrypted key of the
   wrong size before any call.
6. **A failed unwrap substitutes a random content key; a failed transport does not.** A `CryptographicException`,
   `JoseException` or `ArgumentException` from an unwrap substitutes a random content key, as RFC 7516 section 11.5
   requires ([ADR-1282](1282-the-jose-library-implements-neither-none-nor-rsa1-5-and-a-failed-jwe-decryption-reveals-no-cause.md)). Any other exception (transport,
   `IOException`, cancellation) propagates and ends the decode of a JSON-serialized JWE.
7. **The JWT validator decrypts asynchronously.** When the header's `kid` matches an eligible decryption key it tries
   only those keys, so a well-addressed token costs at most one service call per key.
8. **COSE MAC and encryption stay synchronous.** Their keys are symmetric and always local, so no remote key reaches
   them.

## Options considered

- **Synchronous twins beside the asynchronous members.** A synchronous `Encode` would sign local keys and fail remote
  ones, and each caller would choose between two paths that behave differently by key. One asynchronous path serves
  every key.
- **Blocking on the asynchronous call.** A synchronous method over `SignAsync` blocks a thread per call on the network,
  risks deadlock under a synchronization context, and is banned (`RS0030`).
- **Signing before encoding.** Computing the signature ahead of the encoder keeps the encoder synchronous, but the
  signing input is the encoder's own output, so the caller would duplicate the header and payload encoding.
- **A signing callback on the credentials.** A delegate carried by the signing credentials moves the choice between
  local and remote signing into every credentials object and bypasses the algorithm's checks and its verification of the
  signature.
- **Verifying a remote decryption another way than the JWE tag.** The authentication tag is what proves the unwrapped
  key; another check would add a second oracle.
- **Asynchronous signing and unwrapping from the algorithm to every caller.** Chosen: one path serves local and remote
  keys.

## Consequences

- A remote signature honors the caller's cancellation and bound.
- A local key pays no allocation for the asynchronous seam, because `SignAsync` returns a completed `ValueTask` from
  `TrySign`.
- Each signature holds one pooled copy of its signing input across the `await`, returned to the pool when the signature
  is written.
- A caller that only authenticates or encrypts with COSE does not await.
- A well-addressed encrypted token costs at most one service call per decryption key.

## References

- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md): the remote key
  that signs and decrypts through these seams.
- [ADR-1282](1282-the-jose-library-implements-neither-none-nor-rsa1-5-and-a-failed-jwe-decryption-reveals-no-cause.md): the random content key on a failed unwrap.
- [ADR-1154](1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md),
  [ADR-1176](1176-dpop-proofs-are-created-and-validated-by-a-jose-service.md),
  [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md),
  [ADR-1410](1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md): the
  decisions whose members are asynchronous.
- RFC 7516 section 11.5.
