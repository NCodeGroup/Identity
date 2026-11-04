# 1403. A stored secret has an active window inside its validity window, and every use of a key honors them

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Amends:** [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md),
  whose expiry enforcement is one rule of this record: a stored key's expiry is honored wherever the key is used, not
  only when a client authenticates; [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md),
  whose one policy admits only the keys in their validity window

## Context

A JSON Web Key Set serves two parties at once: the server, which signs and encrypts with one key per algorithm, and
the verifiers, which must hold every key that signed a token they may still accept. The rollover of a signing key
(OpenID Connect Core 1.0 §10.1.1, RFC 7517 §5) therefore has four instants, not two: the key is published before it
signs, so a verifier that caches the set holds it before the first token it signs; it signs for a while; it stops
signing while the tokens it signed are still valid; and it leaves the set once every one of them has expired, clock
skew included. The same holds for a key the tenant decrypts with: clients encrypt to the key they fetched, so the key
must be published before the server prefers it and must decrypt after the server stops preferring it.

A `PersistedSecret` carries `CreatedWhen` and `ExpiresWhen`, and `KeyMetadata` carries `ExpiresWhen`, which only a
client's authentication honors ([ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md)):
the credential selector signs with any key that fits the algorithm, the JSON Web Token validator tries any key the
header selects, and the JSON Web Key Set publishes every key the tenant holds. Two instants cannot express a window
inside a window, and a rule enforced in one place is no rule for the others.

## Decision

**Every stored secret has an active window, from `ActivatesWhen` to `RetiresWhen`, inside its validity window, from
`CreatedWhen` to `ExpiresWhen`. A key signs or encrypts only in its active window; it verifies, decrypts and is
published only in its validity window. The four instants are data on the secret, their order is one rule, and every
use of a key applies the windows.**

1. **Four instants on the record.** `PersistedSecret` carries `CreatedWhen`, `ActivatesWhen`, `RetiresWhen` and
   `ExpiresWhen`, every one required, and `SecretEntity` stores them. `KeyMetadata` carries `ActivatesWhen`,
   `RetiresWhen` and `ExpiresWhen`, each optional: a key built in code without them has no bound. The secret
   serializer copies the instants onto the key's metadata, so the runtime judges the same instants the store holds.
2. **One rule orders them.** `SecretLifecycle` is the rule: created, then activates, then retires, then expires, where
   two neighbors may coincide. `Resolve` fills the instants a request leaves out, activation from creation and
   retirement from expiry, and throws on a request out of order; `TryValidate` reports it. `ISecretGenerator` and
   `ISecretImporter` apply it before any material is generated or imported, and the management secret validator
   applies it to every create, import and update, answering `400` with the pair out of order. A request names the
   instants through `GenerateSecretRequest`, `SecretKeyImportRequest`, `CreateSecretRequest`, `ImportSecretRequest`
   and `UpdateSecretRequest`, each optional, and a management update that omits one resolves it as a create does.
3. **A secret with no lifecycle of its own is active for its whole validity.** A shared secret is a credential its
   holder presents, so it activates when it is created and retires when it expires; `SharedSecretRequest` names no
   other instants, and a rotation that shortens a rotated secret's expiry bounds its retirement with it. A key an
   operator imports or generates through the management API without naming its instants is the same.
4. **The active window is judged where a key is selected.** `KeyMetadata.IsActive(now)` holds when the activation,
   if any, has passed and neither the retirement nor the expiry, if any, has. `ICredentialSelector` signs and encrypts
   only with a key that is active: a key published ahead of its activation, a retired key and an expired key are
   never selected, whatever algorithm they fit, so the selection of the successor is the clock passing the hand-over
   instant on every replica, with no coordination. The selector orders the keys it is given, so of two active keys
   the one that expires last is preferred.
5. **The validity window is judged where a key is used to verify, to decrypt and to publish.**
   `KeyMetadata.IsExpired(now)` holds when the expiry, if any, has passed. The JSON Web Token validator never tries an
   expired key, with the clock skew of the validation parameters as tolerance, the same tolerance the token's own
   lifetime gets: a token signed by, or encrypted to, a key that has expired is refused as a key not found, while a
   retired key serves what it signed until it expires. A certificate key expires with its certificate, so one that
   expired beyond the skew is never tried, and the certificate lifetime requirement judges the certificates of the
   keys that are. The JSON Web Key Set publishes every key in its validity window, the upcoming and the retired ones
   included, and never an expired one. A client credential past its expiry stops authenticating through the same
   expiry.
6. **The windows are shown wherever a secret is.** `SecretResource` and `CreatedSecretResource` carry
   `activatesWhen` and `retiresWhen` beside `createdWhen` and `expiresWhen`, so a console shows where a key stands;
   `PersistedSecret.GetLifecycleState(now)` names it with the open vocabulary `SecretLifecycleStates`: `upcoming`,
   `active`, `retired` and `expired`.

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
  ([ADR-1395](1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md)). The
  requests stay optional, the record does not.
- **Judge expiry at validation without tolerance.** Rejected: a token's `exp` is judged with the clock skew, so a key
  whose expiry is judged without it refuses a token the same validator accepts by its own lifetime.
- **Filter expired keys out of the tenant's key collection.** Rejected: the collection is read once per poll, so a
  key expiring between polls stays in use for up to the poll interval; the instants are judged at each use.

## Consequences

- A key that is not yet active, or retired, never signs or encrypts, and an expired key never verifies, decrypts, is
  published or authenticates, on every path and for every owner.
- A verifier that caches a key set holds a successor for the whole activation delay before the first token it signs,
  and a retired key for the whole grace after the last.
- The host database's `Secrets` table holds two more instants, and every persisted secret carries them; the host
  database context's `InitialCreate` holds them
  ([ADR-1394](1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md)).
- A key built in code without instants has no bound and no window.

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
- Issue [#166](https://github.com/NCodeGroup/Identity/issues/166)
