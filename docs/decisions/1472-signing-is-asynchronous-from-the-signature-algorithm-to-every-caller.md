# 1472. Signing is asynchronous from the signature algorithm to every caller

- **Status:** Accepted (amends [ADR-1120](1120-jwt-validation-requirements-and-handlers.md), [ADR-1176](1176-dpop-proofs-are-created-and-validated-by-a-jose-service.md), [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md), [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md), [ADR-1410](1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md); amended by [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md))
- **Date:** 2026-11-08
- **Deciders:** NCode Group

## Context

A signature can come from a remote service
([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)): the
private key is in a key management service, and signing is a network call that can wait, fail and be cancelled. A
signing primitive that fills a caller's span and returns cannot make that call without blocking its thread on a task,
and `RS0030` bans blocking on a task.

The encode path is span-based. `JoseSerializer` builds a JWS signing input from the encoded header and payload, and a
span cannot be held across an `await`. Every caller above it (the JWT encoder, DPoP proofs, SD-JWT issuance and
presentation, COSE signing and CWT issuance) reaches the signature algorithm through that path, so a signature that is
asynchronous at the bottom is asynchronous at every caller.

## Decision

**Signing is asynchronous from `SignatureAlgorithm` to every caller, and nothing synchronous signs.**

1. **The algorithm's seam is `SignatureAlgorithm.SignAsync`.** It takes the secret key, the signing input as
   `ReadOnlyMemory<byte>`, the destination as `Memory<byte>` and a `CancellationToken`, and returns a
   `ValueTask<int>` of the bytes written. For an `IRemoteSecretKey` it throws `JoseInvalidAlgorithmException` when
   the key is not compatible with the algorithm, awaits the key's `SignAsync`, and throws a `JoseException` unless the
   signature's length equals `GetSignatureSizeBytes` and `Verify` accepts it with the key's stored public key. For any
   other key it calls `TrySign` and returns the count as a completed `ValueTask`, with no state machine, and throws a
   `JoseException` when `TrySign` returns false. `TrySign` stays the synchronous primitive for local key material.
2. **`JoseEncoder` encodes only asynchronously.** Its abstract members are `EncodeAsync` for a payload as
   `ReadOnlyMemory<byte>`, a `string`, a `ReadOnlyMemory<char>` or a `T`, each returning a `ValueTask<string>` or, with
   an `IBufferWriter<char>` first, a `ValueTask`. It has no `Encode`. `JoseSerializer` builds each JWS signing input
   into a pooled buffer held across the `await`, so no span crosses it, awaits `SignAsync`, then writes the
   signature. The signing input's bytes are `ASCII(BASE64URL(header) '.' BASE64URL(payload))`, or, with `b64` false,
   `ASCII(BASE64URL(header) '.') || UTF8(payload)`. The JSON serializations compute every signature first, in order,
   then write the JSON. A JWE encode completes synchronously behind its `ValueTask`, because its keys are local.
3. **Every caller awaits.** `IJwtEncoder.EncodeAsync(JwtEncodingParameters, CancellationToken)` and
   `IDPoPProofService.CreateProofAsync(DPoPProofCreationOptions, CancellationToken)` return `ValueTask<string>`.
   `ISdJwtIssuer.IssueAsync` returns `ValueTask<SelectiveDisclosureJwt>` and `ISdJwtHolder.CreatePresentationAsync`
   returns `ValueTask<string>`. `ICoseSerializer.EncodeSign1Async` and `EncodeSignAsync` and `ICwtIssuer.IssueAsync`
   return `ValueTask<byte[]>`. The protocol client's `DPoPProofKey.CreateProofAsync` returns `ValueTask<string>`.
4. **COSE MAC and encryption stay synchronous.** Their keys are symmetric and always local, so no remote key reaches
   them.

## Options considered

- **Asynchronous twins beside the synchronous members.** A synchronous `Encode` would sign local keys and fail remote
  ones, and each caller would choose between two paths that behave differently by key. One asynchronous path serves
  every key.
- **Blocking on the asynchronous call.** A synchronous method over `SignAsync` blocks a thread per signature on a
  network call, risks deadlock under a synchronization context, and is banned (`RS0030`).
- **Signing before encoding.** Computing the signature ahead of the encoder keeps the encoder synchronous, but the
  signing input is the encoder's own output, so the caller would duplicate the header and payload encoding.
- **A callback on the credentials.** A signing delegate carried by `JoseSigningCredentials` moves the choice between
  local and remote signing into every credentials object and bypasses the algorithm's checks and its verification of
  the signature.

## Consequences

- Every caller of a signing member awaits and takes a `CancellationToken`, so a remote signature honors the caller's
  cancellation and bound.
- A local key completes without a state machine: `SignAsync` returns a completed `ValueTask` from `TrySign`, so the
  local path pays no allocation for the asynchronous seam.
- Each signature holds one pooled copy of its signing input across the `await`, returned to the pool when the
  signature is written.
- A signature algorithm that a package contributes implements `TrySign` and inherits `SignAsync`; one that can sign
  remotely overrides it.
- COSE MAC and encryption keep their synchronous members, so a caller that only authenticates or encrypts does not
  await.

## References

- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md): the
  remote key that signs through this seam.
- [ADR-1120](1120-jwt-validation-requirements-and-handlers.md),
  [ADR-1176](1176-dpop-proofs-are-created-and-validated-by-a-jose-service.md),
  [ADR-1272](1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [ADR-1405](1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md),
  [ADR-1410](1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md): the
  decisions whose signing members are asynchronous.
- [#165](https://github.com/NCodeGroup/identity/issues/165)
