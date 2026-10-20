# 161. A client's public keys have one source and are imported under one policy

- **Status:** Accepted (applied to every owner's keys by
  [ADR-0186](0186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md))
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A client's public keys are the keys the server verifies the client's signatures with (`private_key_jwt`, signed
request objects, CIBA requests) and encrypts its ID tokens and UserInfo responses to
([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)). They enter the server at four points:
the JSON Web Key Set a client registers by value at dynamic client registration (RFC 7591,
[ADR-0063](0063-dynamic-client-registration.md)), the set it
publishes at its `jwks_uri` ([ADR-0106](0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md)), a key
imported for it through the management API ([ADR-0104](0104-import-public-asymmetric-secret-material.md)), and the
metadata an administrator updates on a stored key. Each entry point checked its keys differently, and the gaps were
real:

- **Two by-value stores.** Registration stored the `jwks` document verbatim as a client setting, which a registration
  read echoed back, and separately imported its RSA and EC keys as the client's secrets. Nothing at runtime read the
  setting, so a `jwks` written through the management API was stored and never used, and a member the import path
  skipped — an `oct` key's `k`, an `OKP` key's `d` — was kept in plaintext settings and echoed.
- **A closed key-type vocabulary.** Registration and the `jwks_uri` resolver each switched on `kty` over `RSA` and
  `EC`, so a client's `OKP` (EdDSA) or `AKP` (ML-DSA) key was silently dropped although the key-type descriptors import
  both, against [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md).
- **A `kid` was a global resource identifier.** A stored key's `kid` was its secret identifier, which is unique across
  every tenant. Registration used the client's `kid` as that identifier, so two clients — in any tenants — registering
  the same `kid` collided, and a key registered without one was given an invented identifier the client could not
  know. The management import ignored the JWK's `kid` and used an invented one, so the `kid` a client signed with never
  matched. Nothing required a `kid` to be unique among one client's keys, though key selection depends on it.
- **Metadata rules on one path only.** The management API checked `use` against `alg`, the algorithm against the key's
  type, and its key size — but only when generating a key. No path checked a registered key's `use`, `key_ops`, and
  `alg` against each other or the key, or rejected private material beyond the `d` member.
- **Two sources at once.** The resolver merged a client's stored keys with the keys at its `jwks_uri`, while OpenID
  Connect Dynamic Client Registration 1.0 §2 forbids registering both. Only dynamic registration enforced it, and an
  update (RFC 7592) rewrote the client's settings without touching its keys at all.

## Decision

**A client's public keys have exactly one source and are imported under one policy, applied by one service wherever a
client key enters.**

1. **One source.** A client's public keys are either the keys stored for it (registered by value or imported through
   the management API) or, when it has a `jwks_uri`, the keys it publishes there — never both. `IClientKeyResolver`
   returns the client's shared (symmetric) secrets plus exactly one of the two; with a `jwks_uri` set, stored public
   keys are excluded, and a fetch that fails or a set that is refused leaves the client with no public keys (fail
   closed). Every consumer resolves through it, the `self_signed_tls_client_auth` handler included. The writes that would
   create both are refused with `409 Conflict`: a management settings write that adds a `jwks_uri` while public keys are
   stored, and a management import or generation of a public key while a `jwks_uri` is set. A registration update
   replaces all of a client's metadata, its keys included (RFC 7592 §2.2): the keys of its `jwks` replace every stored
   public key, and none remain when it publishes a `jwks_uri` instead; shared secrets are kept.

2. **The stored keys are the single by-value store; `jwks` is a projection.** The `jwks` setting is retired: its
   descriptor, name, key, and rule are removed, and a data migration strips it from every stored settings document.
   Dynamic registration imports the keys of a `jwks` as the client's secrets, and every registration response renders
   `jwks` by exporting the public keys stored for the client. Keys are secrets — data-protected at rest, managed as
   resources — the same model as Auth0's client credentials, not untyped metadata.

3. **A `kid` is the client's name for its key, separate from the resource identifier.** A stored secret carries an
   optional `KeyId`; when it is unset the key is selected by its secret identifier, so every server-generated key is
   unchanged. A key registered as a JSON Web Key keeps its own `kid`, under a newly generated, opaque secret identifier
   ([ADR-0014](0014-server-generated-opaque-resource-ids.md)). Every key of a registered or published JSON Web Key
   Set must have a `kid`, unique within the set; a key the management API imports keeps the JWK's `kid` or, from PEM
   or a certificate, is selected by its secret identifier, and the API refuses a `kid` another of the client's keys
   already uses.

4. **One policy, behind a public seam.** `IClientKeyImporter` (public, in `NCode.Identity.OpenId.Authentication.Abstractions`,
   so the management package reaches it without referencing an implementation package) imports a client key under the
   policy: the key's `kty` names a registered key type that imports public material; the key carries no private or
   symmetric member (`d`, `p`, `q`, `dp`, `dq`, `qi`, `oth`, `k`, `priv`); `kid`, `use`, and `alg` are strings; `use` is
   `sig` or `enc`; `key_ops` is an array of distinct strings whose known operations belong to one use, which agrees
   with `use` (RFC 7517 §4.3); and the declared `alg` is a registered signature or key-management algorithm whose use
   agrees with the key's and which accepts the key's type and size. A request may repeat a JWK's `kid`, `use`, or `alg`
   but not contradict it.

5. **Registered and published sets differ only in what they ignore.** A registered set is all or nothing: a key the
   server cannot use is an error the client sees as `invalid_client_metadata`, since it would be registering a key that
   never works. A published set may also serve other parties, so — as RFC 7517 §5 directs — a key of an unrecognized
   type or algorithm, without a `kid`, malformed, or with metadata that contradicts it is ignored; but a set that
   exposes private or symmetric material, or gives two usable keys one `kid`, is refused as a whole, so a compromised
   or ambiguous set is never used.

6. **The vocabularies stay open.** `ISecretKeyTypeDescriptor.JwkKeyType` states the JSON Web Key `kty` of each key type
   and `ISecretKeyTypeCatalog.TryGetByJwkKeyType` resolves it; the catalog refuses two key types that claim one `kty`.
   The key-to-algorithm rule is `IAlgorithmCollection.ValidateKeyAlgorithm`, read from the registered algorithms, which
   the management API's secret generation uses too. A key type or algorithm a package registers is accepted at every
   entry point without changes to the importer.

## Options considered

- **Keep the `jwks` setting as the by-value store and read it at runtime.** Rejected: the management API's key import
  is a second by-value path, so two stores would remain, and keys would sit in plaintext settings rather than in the
  data-protected secret store.
- **Keep merging stored keys with `jwks_uri` keys.** Rejected: OpenID Connect Dynamic Client Registration 1.0 §2
  forbids both, and a merge lets a stale stored key outlive the rotation the client performs at its `jwks_uri`.
- **Validate the `jwks` setting document with a stricter setting rule.** Rejected: a setting rule is static and cannot
  reach the key-type catalog or the registered algorithms, and it would leave the management import, the `jwks_uri`
  fetch, and key updates unchecked.
- **Refuse a whole published set for any unusable key.** Rejected: a client's `jwks_uri` may list keys for other
  parties or algorithms this server does not support; RFC 7517 §5 says to ignore them. Only material that makes the set
  unsafe (private keys) or ambiguous (a repeated `kid`) refuses it.
- **Allow a registered key without a `kid`.** Rejected: the server would have to invent one the client cannot know, and
  a set of several unnamed keys cannot be rotated predictably (OpenID Connect Core §10.1.1).
- **A unique database index on a client's `kid`.** Rejected for now: secrets are stored in one table and joined to their
  owner, so the constraint would need the owner denormalized onto the secret; the writes that add a client key check
  uniqueness, which is sufficient for these low-concurrency administrative writes.

## Consequences

- A client registers, publishes, or is given exactly one set of public keys, and every consumer sees the same set; a
  key carrying private material is never stored, echoed, or used.
- Clients may register `OKP` (EdDSA) and `AKP` (ML-DSA) keys wherever the server registers those key types.
- Two clients may use the same `kid`; registration no longer fails when they do.
- **Breaking changes**, accepted under the pre-release posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)):
  the `jwks` setting, `OpenIdSettingNames.Jwks`, and `OpenIdSettingKeys.Jwks` are removed (a migration strips the
  setting from stored documents); a registered key without a `kid` is refused; a `jwks_uri` client's stored public
  keys are no longer used; `AsymmetricSecretKeyTypeDescriptor.ExpectedJwkKeyType` becomes the public `JwkKeyType`
  member every descriptor implements; `PersistedSecret`, `SecretEntity`, and `SecretKeyImportRequest` gain `KeyId`
  (a schema migration adds the nullable column).
- A JSON Web Key's certificate is imported with it: the first `x5c` entry (RFC 7517 §4.7) is kept as the key's
  certificate once it is shown to carry the key's public key, and a key whose `x5c` is empty, malformed, or names
  another key is refused. A `self_signed_tls_client_auth` client can therefore register its certificates through
  `jwks` or publish them at a `jwks_uri` (RFC 8705 §2.2). The rest of the chain is not kept, since a self-signed
  certificate is matched by thumbprint, not by chain.
- Tenant, server, and resource-server key imports and updates keep their existing checks; applying the key-to-algorithm
  rule to them is a follow-up recorded in the backlog.

## References

- [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517) — JSON Web Key (§4.3 `key_ops`, §4.5 `kid`, §5 JWK Set)
- [RFC 7591](https://www.rfc-editor.org/rfc/rfc7591) §2 — client metadata (`jwks`, `jwks_uri`)
- [RFC 7592](https://www.rfc-editor.org/rfc/rfc7592) §2.2 — client update replaces the registration
- [OpenID Connect Dynamic Client Registration 1.0](https://openid.net/specs/openid-connect-registration-1_0.html) §2
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html) §10.1.1 — signing key rotation
- [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705) §2.2 — self-signed certificate mutual-TLS client authentication
