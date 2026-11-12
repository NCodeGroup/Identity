# 1161. Every owner's public keys have one source and are imported under one key policy

- **Status:** Accepted
- **Date:** 2026-11-10
- **Deciders:** NCode Group

## Context

A client's public keys are the keys the server verifies the client's signatures with (`private_key_jwt`, signed request
objects, CIBA requests) and encrypts its ID tokens and UserInfo responses to
([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md)). They
enter the server at four points: the JSON Web Key Set a client registers by value at dynamic client registration (RFC
7591, [ADR-1192](1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md)),
the set it publishes at its `jwks_uri`
([ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md)),
a key imported for it through the management API ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)), and
the metadata an administrator updates on a stored key. Tenants and resource servers import and update keys through the
same management API. The forces are:

- **One by-value store.** Keys are secrets, data-protected at rest and managed as resources, not untyped metadata. A
  second copy of a key in plaintext settings would be stored, echoed and never used.
- **Open key-type vocabulary.** A client's `OKP` (EdDSA) or `AKP` (ML-DSA) key must be accepted wherever the server
  registers those key types ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **A `kid` is a publisher's name, not a global resource identifier.** Clients in any tenants may use the same `kid`,
  and key selection depends on a `kid` being unique among one owner's keys. OpenID Connect Dynamic Client Registration
  1.0 and RFC 7517 §4.5 make the `kid` optional, and a client that registers a key without one still needs a name it
  can derive.
- **Metadata rules must hold on every path.** A key's `use`, `key_ops` and `alg` must agree with each other and with
  the key, and no private material may be stored, whoever owns the key and however it arrives.
- **Two sources at once is forbidden.** OpenID Connect Dynamic Client Registration 1.0 §2 forbids registering both
  `jwks` and `jwks_uri`.

## Decision

**An owner's public keys have exactly one source and are imported under one policy, applied by one service wherever a
key enters.**

1. **One source.** A client's public keys are either the keys stored for it (registered by value in `jwks` or imported
   through the management API) or, when it has a `jwks_uri`, the keys it publishes there, never both. `IClientKeyResolver`
   returns the client's shared (symmetric) secrets plus exactly one of the two, and a fetch that fails or a set that is
   refused leaves the client with no public keys. A settings write that adds a `jwks_uri` while public keys are stored,
   and a key import or generation while a `jwks_uri` is set, are `409 Conflict`, decided by `IClientKeySourceChecker`.
   A registration update (RFC 7592 §2.2) replaces the client's keys: the keys of its `jwks` replace every stored public
   key, none remain when it publishes a `jwks_uri` instead, and shared secrets are kept.
2. **The stored keys are the single by-value store.** There is no `jwks` setting. The data-protected stored secrets
   hold the keys, dynamic registration imports the keys of a `jwks` as the client's secrets, and every registration
   response renders `jwks` by exporting the client's stored public keys.
3. **A `kid` names the key, separate from the secret identifier.** A stored key carries an optional `KeyId`, the
   publisher's `kid`; a key without one is selected by its secret identifier. Clients in any tenants may share a
   `kid`. A management import keeps the JWK's `kid`, and a PEM or certificate import is selected by its secret
   identifier.
4. **A registered key without a `kid` is named by its thumbprint.** In a registered set a key whose `kid` is absent,
   `null` or empty is named by the SHA-256 thumbprint of its public members (RFC 7638), base64url encoded, stored as
   `KeyId` and rendered as `kid`. Any holder of the key computes the same value. A declared `kid` is kept; one that is
   not a string is left to member validation, and a key of a `kty` the server has no rules for is refused as
   unsupported and never named. Two keys of one name, declared or by thumbprint, are refused as a duplicate `kid`,
   and one refused key refuses the set.
5. **One policy, behind a public seam.** `IClientKeyImporter`, public in `NCode.Identity.OpenId.Authentication.Abstractions`
   with `TryImportRegisteredKeySet`, `TryImportPublishedKeySet` and `TryImportKey`, applies one policy:
   - `kty` names a registered key type that imports public material;
   - no `d`, `p`, `q`, `dp`, `dq`, `qi`, `oth`, `k` or `priv` member is present;
   - `kid`, `use` and `alg` are strings, and `use` is `sig` or `enc`;
   - `key_ops` holds distinct operations of one use, agreeing with `use` (RFC 7517 §4.3);
   - `alg` is a registered algorithm of the key's use that accepts its type and size
     (`IAlgorithmCollection.ValidateKeyAlgorithm`);
   - a request may repeat a JWK's `kid`, `use` or `alg` but not contradict it.
6. **Registered and published sets differ only in what they ignore.** A registered set is all or nothing, and a key
   the server cannot use is `invalid_client_metadata`. A published set may also serve other parties, so, as RFC 7517 §5
   directs, a key of an unknown type or algorithm, without a `kid`, malformed or self-contradictory is ignored; the
   set is refused whole for private or symmetric material, or for two usable keys with one `kid`.
7. **The vocabularies stay open.** `ISecretKeyTypeDescriptor.JwkKeyType` states the `kty` of each key type and
   `ISecretKeyTypeCatalog.TryGetByJwkKeyType` resolves it; the catalog refuses two key types that claim one `kty`. So
   `OKP`, `AKP` and any registered key type are accepted at every entry point.
8. **A certificate travels with its key.** A JWK's first `x5c` certificate (RFC 7517 §4.7) is kept once it is shown to
   carry the key's public key; an empty, malformed or mismatched `x5c` is refused. A `self_signed_tls_client_auth`
   client can therefore register or publish its certificates (RFC 8705 §2.2). A JWT signed with such a key is refused
   outside the certificate's validity, compared with the UTC clock and the usual skew.
9. **Every owner's keys use the same policy.** The tenant, resource-server and client import endpoints call
   `TryImportKey`; a refusal is `400 Bad Request` with its reason. `BaseApiEndpointHandler.KeyIdConflictOrDefault`
   answers `409 Conflict` when an import's `kid` (else its secret identifier) matches an existing key of that owner,
   tenant remote secrets included. Every owner's update calls `ISecretValidator.ValidateUpdate`, which checks `use`
   and `alg` against the stored key's type and size with `ValidateKeyAlgorithm`, and keeps the `KeyId`.
10. **Every key reports its `kid`.** `SecretResource` and `CreatedSecretResource` carry a required `KeyId`, mirrored in
    the `@ncode/api` contract and shown in the console's **Key ID** column.

## Options considered

- **A `jwks` setting as the by-value store, read at runtime.** Rejected: the management import is a second by-value
  path, so two stores would remain, and keys would sit in plaintext settings instead of the data-protected secret
  store.
- **Merging stored keys with the keys at the `jwks_uri`.** Rejected: the registration specification forbids both, and a
  merge lets a stale stored key outlive the rotation the client performs at its `jwks_uri`.
- **A setting rule instead of an importer.** Rejected: a setting rule is static and cannot reach the key-type catalog
  or the registered algorithms, and it would leave the management import, the `jwks_uri` fetch and key updates
  unchecked.
- **Refusing a published set for one unusable key.** Rejected: a `jwks_uri` may list keys for other parties or
  algorithms this server does not support, and RFC 7517 §5 says to ignore them. Only private material or an ambiguous
  `kid` makes the set unsafe.
- **An unnamed key, or a server-invented `kid`.** Rejected: a stored key without a name falls back to an opaque
  secret identifier that a client never sees, and an invented name is one the client cannot derive. The thumbprint is
  derivable by anyone who holds the key.
- **Per-owner checks in each handler.** Rejected: handlers drift apart, and a key's validity does not depend on who
  owns it.
- **A nullable `KeyId` on the contracts.** Rejected: every caller would repeat the fallback to the secret identifier.
- **A unique database index on `kid`.** Rejected: the owner is not on the secret row, so the constraint would need the
  owner denormalized onto it; the writes that add a key check uniqueness, which suffices for low-concurrency
  administrative writes.

## Consequences

- An owner has exactly one set of public keys, every consumer sees the same set, and a key carrying private material is
  never stored, echoed or used.
- Clients may register `OKP` and `AKP` keys wherever the server registers those key types, and two clients may use the
  same `kid`.
- A registration whose `jwks` has a key without a `kid` succeeds, and the response shows the key with its thumbprint as
  `kid`. Registering the same key twice, or beside a key that declares its thumbprint as `kid`, is
  `invalid_client_metadata`.
- A key's thumbprint is its name for as long as the key keeps the members the thumbprint covers; a client that names
  its keys itself is unaffected.
- A key imported as a JSON Web Key keeps its `kid` whoever owns it, so a token signed with it carries the `kid` its
  publisher chose, and a key whose metadata contradicts it is refused at import and update.
- A "you chose wrong" smell: a second by-value store of keys, or an owner whose import path skips `IClientKeyImporter`.
- The importer's tests cover each rule of the policy, the thumbprint naming and its duplicates, the single-source
  `409` responses and the update validation for every owner.

## References

- `IClientKeyImporter`, `IClientKeyResolver` and `IClientKeySourceChecker` in `NCode.Identity.OpenId.Authentication.Abstractions`
- `BaseApiEndpointHandler.KeyIdConflictOrDefault` and `ISecretValidator.ValidateUpdate`
- [ADR-1014](1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md): opaque secret identifiers
- [RFC 7517](https://www.rfc-editor.org/rfc/rfc7517): JSON Web Key (§4.3 `key_ops`, §4.5 `kid`, §4.7 `x5c`, §5 JWK Set)
- [RFC 7638](https://www.rfc-editor.org/rfc/rfc7638): JWK thumbprint
- [RFC 7591](https://www.rfc-editor.org/rfc/rfc7591) §2 and [RFC 7592](https://www.rfc-editor.org/rfc/rfc7592) §2.2: client metadata and update
- [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705) §2.2: self-signed certificate mutual-TLS client authentication
- [OpenID Connect Dynamic Client Registration 1.0](https://openid.net/specs/openid-connect-registration-1_0.html) §2
