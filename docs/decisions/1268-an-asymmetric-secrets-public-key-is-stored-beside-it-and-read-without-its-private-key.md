# 1268. An asymmetric secret's public key is stored beside it and read without its private key

- **Status:** Accepted
- **Date:** 2026-10-26
- **Deciders:** NCode Group

## Context

An asymmetric secret has two halves. The private half is sensitive and never crosses the API boundary: every secret
read path is metadata-only
([ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md)). The public half is publishable,
and the server already hands tenant signing keys to relying parties through the JWKS endpoint (RFC 7517). An operator
inspecting or wiring up a key in an admin UI still needs one secret's public key to paste into a verifier, pin in a
relying party or compare with a published JWKS entry.

The forces in tension:

- A public key is safe to return on read, so the metadata-only invariant, which protects private material, does not need
  to cover it.
- Two audiences want two forms. A programmatic consumer wants a JWK, the shape the JWKS endpoint emits. A human operator
  wants PEM (`-----BEGIN PUBLIC KEY-----`), the format `openssl` and most JWT tooling accept directly.
- A symmetric secret has no public half.
- Emitting a public key must not need private material. Deriving it from the stored secret unprotects the private key
  under the owner's master key
  ([ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)),
  so a read of public data would load the owner's master keys, possibly reach the key management service, and hold
  private key material in memory.
- The server already has one source of truth for turning a key into a public JWK: the key type's descriptor
  ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)). A
  second serializer would drift from it in curve names and member encoding.

## Decision

**An asymmetric secret's public key is stored beside it, and every public-key read uses only that stored public half, so
no read of public data touches private material. Tenants, clients and resource servers expose it through a read-only
sub-resource of the secret that answers both JWK and PEM.**

### Storage

- `PersistedSecret.PublicKey` is a `string?` holding the base64url SubjectPublicKeyInfo (SPKI) DER of a private
  (`pkcs8`) or remote asymmetric key. It is at most `SecretMaxLengths.PublicKey` (4000) characters, which fits ML-DSA-87
  at about 3,490 and RSA-16384 at about 2,800.
- The descriptor that generates the key sets it through `required string? GeneratedSecretKeyMaterial.PublicKey`, because
  only the generating descriptor holds the unprotected key. `required` makes every descriptor, a third-party one
  included, decide. The public key of a `remote` key comes from its service
  ([ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md)).
- It is `null` for symmetric and verifier secrets and for `spki` and `x509` secrets. The `EncodedValue` of the last two
  is already public, a duplicate would only repeat it, and an `x509` secret would lose its certificate.
- Every update path copies `PublicKey` into the rebuilt record.

### One predicate and one reader

- `PersistedSecret.HasPublicKey()` is the one predicate: `SecretEncodings.IsPublic(SecretEncoding) || PublicKey is not
  null`.
- `ISecretSerializer.DeserializePublicSecret` reads the public half of any secret that has one: its own material when
  the encoding is public, otherwise `PublicKey` read as `spki`. It throws `InvalidOperationException` when there is
  none.
- `ISecretPublicKeyConverter.Convert(PersistedSecret)` needs no `ISecretProtector`, so a public read loads no master key,
  reaches no key management service and holds no private material.

### The endpoint

`GET .../secrets/{secretId}/public-key` on tenants, clients and resource servers answers a `PublicKeyResource`:

- `SecretId` and `SecretType`;
- `Jwk`, a `JsonElement` of public members only, from the descriptor's `ExportPublicKey`, the same seam as the JWKS, so
  the two cannot drift;
- `Pem`, the SubjectPublicKeyInfo in PEM form.

A secret with no public half (a symmetric one, a type that exports none, or `HasPublicKey()` false) answers `404`. The
read is authorized like reading the secret (`Operations.Read` at the owner's node) and introduces no scope.
`SecretResource` and the secret list stay metadata-only.

### Registration

The registration response's `jwks` (RFC 7591 and 7592) reads the stored public halves the same way. Registration
creation keeps its protector only to generate the client secret.

## Options considered

- **Derive on read.** Unprotect the private key under the owner's master key and emit its public half. It loads master
  keys, may reach the key management service and holds private material to answer a public read. Rejected.
- **Store a JWK.** Its `kid`, `use` and `alg` can change after the key is stored, and SPKI is what every descriptor
  already deserializes. Rejected.
- **Store PEM.** A second format beside the SPKI every descriptor reads. Rejected.
- **Duplicate the public half of public-only secrets.** It repeats `EncodedValue` and loses an `x509` certificate.
  Rejected.
- **A `PublicKey` field on `SecretResource`.** It bloats the shared read contract and the list with material only
  sometimes wanted, and couples the metadata view to key export. A distinct sub-resource keeps `SecretResource`
  metadata-only. Rejected.
- **JWK only.** It forces an operator or an `openssl` user to convert to PEM. Rejected.
- **PEM only.** It drops the JWK that lines a management read up with the JWKS document and that a programmatic consumer
  expects. Rejected.
- **A bespoke JWK serializer in the management layer.** It duplicates the descriptor's export and drifts from the JWKS
  endpoint. Rejected.
- **Raw DER or certificate output.** JWK and PEM cover both audiences. Rejected.
- **Store the SPKI beside the secret and read it through the descriptor seam, with JWK and PEM from a sub-resource.**
  Chosen.

## Consequences

- No public-key read, on any owner or in the registration response, acquires a protector or touches private key material.
  The private-material invariant and the one-time symmetric reveal of
  [ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md) hold.
- An operator reads any asymmetric secret's public key as JWK for tooling and PEM for copy-paste.
- The management JWK and the JWKS endpoint share the descriptor's export, so a new asymmetric key type lights up both
  surfaces.
- A symmetric secret is never unprotected to learn it has no public key, and reports `404`, the honest answer.
- `PublicKeyResource` is public surface of `NCode.Identity.OpenId.Management.Abstractions`
  (`NCode.Identity.OpenId.Management.Contracts.Secrets`) and carries no private material.
- `required` on `GeneratedSecretKeyMaterial.PublicKey` is a compile-time check that a new descriptor decides what it
  stores.
- A column of up to 4000 characters per asymmetric secret is the storage cost of the read path.

## References

- [ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md): the metadata-only retrieval
  invariant and the one-time reveal.
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md): the
  key hierarchy a derive-on-read path would load.
- [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md): the
  descriptor seam that exports the public key.
- [ADR-1471](1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md): the remote
  key whose service supplies its public key.
- RFC 7517 (JSON Web Key), RFC 7468 (PEM), RFC 7591 and RFC 7592 (dynamic client registration).
