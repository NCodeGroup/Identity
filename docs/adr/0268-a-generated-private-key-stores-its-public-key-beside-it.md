# 268. A generated private key stores its public key beside it

- **Status:** Accepted (amends [ADR-0058](0058-asymmetric-public-key-material-read-path.md))
- **Date:** 2026-10-26
- **Deciders:** NCode Group

## Context

Reading a generated private key's public key — the management `GET …/secrets/{secretId}/public-key` endpoints on
servers, tenants, resource servers and clients, and the `jwks` of the RFC 7591/7592 registration response — unprotected
the private key under the owner's master key
([ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)) only to emit its public
half. A read of public data therefore loaded the owner's master keys, possibly reached the key management service, and
held private key material in memory ([ADR-0058](0058-asymmetric-public-key-material-read-path.md)). Issue
[#176](https://github.com/NCodeGroup/Identity/issues/176) asks for the public half to be stored.

## Decision

The SubjectPublicKeyInfo (SPKI) DER of every generated asymmetric key is stored beside it in
`PersistedSecret.PublicKey`, so the public-key read paths read only public data and need no `ISecretProtector`.

1. **Shape.** `PersistedSecret.PublicKey` is a `string?` holding base64url SPKI DER, at most
   `SecretMaxLengths.PublicKey` (4000) characters; ML-DSA-87's SPKI is about 3,490 and RSA-16384's about 2,800.
2. **When it is set.** Only for a generated private asymmetric key (`SecretEncoding` `pkcs8`). It is `null` for symmetric
   secrets and for public-only secrets (`spki`, `x509`), whose `EncodedValue` already is public material: storing it
   twice would duplicate it, and an `x509` secret would lose its certificate.
3. **Who produces it.** The descriptor that generates the key, through the new
   `required string? GeneratedSecretKeyMaterial.PublicKey`, because only it holds the unprotected key. `required` forces
   every descriptor, including a third-party one, to decide.
4. **One predicate.** `PersistedSecret.HasPublicKey()` is `SecretEncodings.IsPublic(SecretEncoding) || PublicKey is not null`.
5. **Reading.** `ISecretSerializer.DeserializePublicSecret` reads the public half of any secret that has one: its own
   material when its encoding is public, otherwise `PublicKey` as `spki`. It throws `InvalidOperationException` when
   there is none. `ISecretPublicKeyConverter.Convert(PersistedSecret)` replaces `Convert(ISecretProtector,
   PersistedSecret)` and returns `null` (the endpoints answer `404`) when the key type exports no public key or
   `HasPublicKey()` is false.
6. **Storage.** The `AddSecretPublicKey` migration only adds a nullable column (expand-only). The `UpdateSecretAsync`
   handlers copy `PublicKey` into the rebuilt `PersistedSecret`.

Registration creation keeps its protector because it generates the client secret; only the response drops it.

## Options considered

- **Derive on read (the previous path).** Loads the owner's master keys and unprotects the private key to emit public
  data. Rejected: it is the problem.
- **Store a JWK or PEM.** A JWK carries `kid`, `use` and `alg`, which can change after the key is stored, and PEM is a
  second format. SPKI is what every descriptor already deserializes. Rejected.
- **Backfill existing rows at startup.** Needs every owner's master key at startup. Rejected: the family is pre-release
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- **Store it for public-only secrets too.** Duplicates `EncodedValue` and loses an `x509` certificate. Rejected.

## Consequences

- The four public-key endpoints and the registration response acquire no protector, and never touch private key material.
- A private key generated before this change has `PublicKey = null`: its public-key endpoint answers `404` and the
  registration `jwks` omits it. Rotating the key exposes its public key.
- A symmetric secret is no longer unprotected just to return `null`.
- Breaking: `ISecretPublicKeyConverter.Convert` drops its protector parameter, and `GeneratedSecretKeyMaterial` has a
  required member.

## References

- [ADR-0058](0058-asymmetric-public-key-material-read-path.md) — the public-key read path this amends.
- [ADR-0235](0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) — the key hierarchy.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — pre-release posture, no backfill.
- [#176](https://github.com/NCodeGroup/Identity/issues/176) — the issue.
