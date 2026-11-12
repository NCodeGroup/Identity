# 1403. A stored secret has an active window inside its validity window, honored on every use

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group

## Context

A JSON Web Key Set serves two parties at once: the server, which signs and encrypts with one key per algorithm, and
the verifiers, which must hold every key that signed a token they may still accept. The rollover of a signing key
(OpenID Connect Core 1.0 §10.1.1, RFC 7517 §5) therefore has four instants, not two: the key is published before it
signs, so a verifier that caches the set holds it before the first token it signs; it signs for a while; it stops
signing while the tokens it signed are still valid; and it leaves the set once every one of them has expired, clock
skew included. The same holds for a key the tenant decrypts with: clients encrypt to the key they fetched, so the key
must be published before the server prefers it and must decrypt after the server stops preferring it.

Two instants cannot express a window inside a window, and a rule enforced in one place is no rule for the others. The
credential selector would sign with any key that fits the algorithm, the JSON Web Token validator would try any key
the header selects, and the JSON Web Key Set would publish every key the tenant holds, if only a client's
authentication honored expiry ([ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md)).
The server validates the tokens it issued under one policy that admits only the keys in their validity window
([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).

## Decision

**Every stored secret has an active window, from `ActivatesWhen` to `RetiresWhen`, inside its validity window, from
`CreatedWhen` to `ExpiresWhen`. A key signs or encrypts only in its active window; it verifies, decrypts and is
published only in its validity window. The four instants are data on the secret, their order is one rule, and every
use of a key applies the windows.**

1. **Four instants on the record.** `PersistedSecret` requires `CreatedWhen`, `ActivatesWhen`, `RetiresWhen` and
   `ExpiresWhen`, and `SecretEntity` stores them. `KeyMetadata` carries `ActivatesWhen`, `RetiresWhen` and
   `ExpiresWhen`, each optional: a key built in code without them has no bound. The secret serializer copies the
   instants from the record onto the key's metadata, so the runtime judges the same instants the store holds.
2. **One rule orders them.** `SecretLifecycle` is the rule: created, then activates, then retires, then expires, where
   two neighbors may coincide. `Resolve` fills the instants a request leaves out, activation from creation and
   retirement from expiry, and throws on a request out of order; `TryValidate` reports it. `ISecretGenerator` and
   `ISecretImporter` apply it before any material is generated or imported, and the management secret validator
   (`DefaultSecretValidator`) applies it to every create, import and update, answering `400` with the pair out of
   order. `PersistedSecret` is a record, so every change to a stored secret's metadata is a `with` copy that keeps
   every other member. A management update that omits an instant keeps the stored one, the retirement bounded by the
   new expiry (`ResolveUpdate`), so a retired or upcoming key stays so unless the update names the instant. An update
   whose new expiry precedes the stored activation is answered `400`: a key that resumes signing is a deliberate act,
   never the side effect of extending an expiry.
3. **A secret with no lifecycle of its own is active for its whole validity.** A shared secret is a credential its
   holder presents, so it activates when it is created and retires when it expires. `SharedSecretRequest` names no
   inner instants, and a client secret create that names an activation or a retirement is answered `400`, because an
   instant the secret cannot honor is refused rather than dropped. A key created or imported through the management
   API without naming its instants behaves the same.
4. **The active window is judged where a key is selected.** `KeyMetadata.IsActive(now)` holds when the activation, if
   any, has passed and neither the retirement nor the expiry, if any, has. `ICredentialSelector` signs and encrypts
   only with an active key: a key published ahead of its activation, a retired key and an expired key are never
   selected, whatever algorithm they fit, so the selection of a successor is the clock passing the hand-over instant
   on every replica, with no coordination. The tenant's `ISecretKeyCollection` (`DefaultSecretKeyCollection`) orders
   its keys by expiry, latest first, and the selector takes the first active key that fits, so of two active keys the
   one that expires last is preferred.
5. **The validity window is judged where a key verifies, decrypts and is published.** `KeyMetadata.IsExpired(now)`
   holds when the expiry, if any, has passed. The JSON Web Token validator never tries a key expired beyond the clock
   skew of the validation parameters, the same tolerance the token's own lifetime gets: a token signed by, or
   encrypted to, an expired key is refused as a key not found, while a retired key serves what it signed until it
   expires. A certificate key expires with its certificate. The JSON Web Key Set (`DefaultJwksEndpointHandler`)
   publishes every key in its validity window, the upcoming and the retired ones included, and never an expired one.
   A client credential past its expiry stops authenticating through the same expiry.
6. **One window rule serves the stored secret and the runtime key.** `KeyLifecycle` is the single window rule that the
   `KeyMetadata` and `PersistedSecret` extensions call, so a stored secret and its runtime key never disagree.
   `SecretLifecycle` and `KeyLifecycle` live in the abstractions as a deliberate dependency-free exception, as
   `PersistedSecretExtensions.HasPublicKey` is ([ADR-1268](1268-an-asymmetric-secrets-public-key-is-stored-beside-it-and-read-without-its-private-key.md)).
7. **The windows are shown wherever a secret is.** `PersistedSecret.GetLifecycleState(now)` names where a key stands
   with the open vocabulary `SecretLifecycleStates`: `upcoming`, `active`, `retired` and `expired`. `SecretResource`
   and `CreatedSecretResource` carry `activatesWhen` and `retiresWhen` beside `createdWhen` and `expiresWhen`. The
   management console's `SecretTable` shows the three instants, derives the same state from them
   (`getSecretLifecycleState`) and flags a key that is upcoming, retired or expired.

The rule applies to every owner's secrets: a tenant's keys, a client's keys and shared secrets, a server's keys and a
resource server's keys. Only the keys the server provisions for a tenant move through the windows on their own
([ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md)); every other
secret keeps the instants its creator gave it.

## Options considered

- **Two instants, with the overlap expressed by two keys.** Rejected: a key published ahead of its use would sign at
  once, because nothing tells the selector to wait, and a retired key would keep signing, because nothing tells it to
  stop; the overlap needs the two inner instants.
- **A state column (`upcoming`, `active`, `retired`) that a job advances.** Rejected: the state changes at an instant
  every replica's clock knows, so a job that writes it only lags the clock, and a job that falls behind makes a key
  sign past its retirement. The instants are the state, and the state name is derived.
- **Nullable inner instants on the record, read as the outer ones when absent.** Rejected: every reader would carry
  the fallback, and a record that lacks what the serving code writes is malformed
  ([ADR-1395](1395-grant-payloads-have-one-versioned-serializer-and-a-stored-record-missing-written-members-fails-closed.md)).
  The requests stay optional, the record does not.
- **Judge expiry at validation without tolerance.** Rejected: a token's `exp` is judged with the clock skew, so a key
  whose expiry is judged without it refuses a token the same validator accepts by its own lifetime.
- **Filter expired keys out of the tenant's key collection at poll time.** Rejected: the collection is read once per
  poll, so a key expiring between polls stays in use for up to the poll interval; the instants are judged at each use.
- **Keep the lifecycle rule out of the abstractions packages.** Rejected: the rule guards the data contract's own
  invariant and has one implementation every path calls. Its callers, the secret generator and importer, the
  management validator and the tenant key rollover, are implementation packages that reference only abstractions
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)),
  so a pure judgment on the contract's own members, with no dependency and no logging, belongs beside it.
- **Chosen: four instants on the secret, one ordering rule, and the windows judged at every use.** It is the only
  option that lets a verifier hold a successor before it signs and a retired key until its tokens expire.

## Consequences

- A key that is not yet active, or retired, never signs or encrypts, and an expired key never verifies, decrypts, is
  published or authenticates, on every path and for every owner.
- A verifier that caches a key set holds a successor for the whole activation delay before the first token it signs,
  and a retired key for the whole grace after the last.
- Every persisted secret carries the four instants, and the host database's `Secrets` table stores them.
- A key built in code without instants has no bound and no window.
- Tests cover the ordering rule, the selector's preference among active keys, the validator's skew tolerance, the
  JSON Web Key Set's contents and the management update's `400` cases.

## References

- [`PersistedSecret`](../../src/NCode.Identity.Secrets.Persistence.Abstractions/DataContracts/PersistedSecret.cs),
  [`SecretLifecycle`](../../src/NCode.Identity.Secrets.Persistence.Abstractions/SecretLifecycle.cs),
  [`SecretLifecycleStates`](../../src/NCode.Identity.Secrets.Persistence.Abstractions/SecretLifecycleStates.cs)
- [`KeyMetadata`](../../src/NCode.Identity.Secrets.Abstractions/Keys/KeyMetadata.cs),
  [`KeyMetadataExtensions`](../../src/NCode.Identity.Secrets.Abstractions/Keys/KeyMetadataExtensions.cs)
- [`DefaultCredentialSelector`](../../src/NCode.Identity.Jose/Credentials/DefaultCredentialSelector.cs),
  [`DefaultJwtValidator`](../../src/NCode.Identity.Jwt/Validation/DefaultJwtValidator.cs),
  [`DefaultJwksEndpointHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Jwks/DefaultJwksEndpointHandler.cs)
- [`DefaultSecretValidator`](../../src/NCode.Identity.OpenId.Management/Endpoints/Secrets/DefaultSecretValidator.cs)
- [OpenID Connect Core 1.0 §10.1.1](https://openid.net/specs/openid-connect-core-1_0.html#RotateSigKeys),
  [RFC 7517 §5](https://datatracker.ietf.org/doc/html/rfc7517#section-5),
  [RFC 8725 §3.1](https://datatracker.ietf.org/doc/html/rfc8725#section-3.1)
- [ADR-1154](1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md),
  [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md),
  [ADR-1404](1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md)
