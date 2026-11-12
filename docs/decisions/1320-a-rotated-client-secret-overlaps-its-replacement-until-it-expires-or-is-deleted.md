# 1320. A rotated client secret overlaps its replacement until it expires or is deleted

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0146](project-template/0146-secrets-are-never-logged-compared-in-constant-time-and-shown-once.md)

## Context

A secret's key material is immutable once generated, so rotating a secret means creating a new one and deleting the
old one ([ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md)). Template ADR-0146
rule 7 rotates a secret by create, switch, delete: the holder switches to the replacement while the old secret still
works, then the old secret is deleted.

A rotation that removes the rotated secret in the same change breaks every caller that holds it until it is
reconfigured. A rotation that keeps it needs a bound, or a forgotten delete leaves a stale credential accepted
forever, and that bound only works when a credential past its `ExpiresWhen` stops authenticating.

A client's shared secret stores the UTF-8 length of its base64url string as `KeySizeBits`, not its entropy, so copying
the stored size as the replacement's entropy would grow the secret on every rotation. Template ADR-0146 rule 4 sets a
floor on a generated secret's entropy, and every generated shared secret has to meet it.

## Decision

`POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate` adds a replacement beside the rotated
secret in one unit of work. Both authenticate until the rotated secret expires or the operator deletes it with
`DELETE`.

- **Rotation overlaps.** The replacement has the rotated secret's type, `use` and `alg` and the expiry that the
  required `RotateSecretRequest.ExpiresWhen` names. It is validated as a created secret is (`ValidateCreate`) and
  issued through `IClientSecretIssuer` in the form the client's authentication method needs
  ([ADR-1391](1391-a-client-secret-is-a-one-way-verifier-unless-its-method-keys-from-its-utf-8-octets.md)). The operator deletes
  the rotated secret with the existing `DELETE` once the holder has switched.
- **A grace expiry bounds the overlap.** The optional `RotateSecretRequest.RotatedExpiresWhen` sets the rotated
  secret's expiry to the earlier of its current expiry and this value, never earlier than its creation, and bounds its
  activation and retirement with it
  ([ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-honored-on-every-use.md)).
  Rotation never extends an expiry, a past value stops the secret at once, and an omitted value keeps its expiry.
- **A client credential past its expiry stops authenticating.** `DefaultOpenIdClientResolver` drops every secret whose
  `ExpiresWhen` is not after now before it deserializes the rest, so every client authentication method honors the
  expiry. An expired secret stays listed until it is deleted.
- **A generated shared secret has at least 256 bits of entropy.** `SharedSecretPolicy.MinimumEntropyBits` is the
  floor. `GenerateSharedSecretVerifier` and `GenerateReversibleSharedSecret` throw below it, the client secret create
  endpoint answers `400`, and dynamic client registration uses the constant. Tenant and resource-server symmetric keys
  are cryptographic keys with their descriptor's own floor.
- **The replacement keeps the rotated secret's entropy.** Its entropy is
  `max(256, KeySizeBits / 8 * 3 / 4 * 8)` (`ClientApiEndpointHandler.GetReplacementEntropyBits`), which inverts the
  base64url length of a generated secret, so a secret does not grow with each rotation.
- **Authorized and audited as the calls it performs.** The caller must be allowed to create a secret for the client
  and to update the rotated secret; rotation performs a create and an update and never a delete. The audit trail
  records the replacement's `Created` and, only when the expiry changed, the rotated secret's `Updated`, without
  material. An `If-Match` header must carry the rotated secret's concurrency token, or the answer is `412`.
- **The response is `201 Created`** with the replacement's `CreatedSecretResource`, the only place its material
  appears, and it is uncacheable
  ([ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md)).
- **Only a shared secret rotates.** A key is the client's to hold; rotating one answers `400`, and the client imports
  the new key and deletes the old one. Tenant and resource-server secrets have no rotate endpoint.
- **The console rotates a shared secret.** It offers a rotate action on each secret of a type that the capabilities
  report as not asymmetric, rotates with a replacement that expires a year out, and keeps the current secret for seven
  days (`rotationGraceDays`).

## Options considered

- **A one-call replacement that removes the rotated secret.** Rejected: it breaks every caller at once and
  contradicts rule 7. Auth0 and Entra ID both let the old secret work while the holder switches.
- **Rotate a key by generating a key pair.** Rejected: the server would hold a private key that the client owns.
- **Default the replacement's expiry to the rotated secret's lifetime.** Rejected: the operator names the replacement's
  expiry, so a required `ExpiresWhen` states the intended lifetime instead of inheriting one.
- **Delete the rotated secret after a server-side grace job.** Rejected: expiry enforcement already ends the overlap
  with no job to schedule, run or fail.
- **Require a grace expiry on every rotation.** Rejected: an operator who deletes the rotated secret by hand has no use
  for one, and the rotated secret's own expiry is already a bound.
- **Overlapping rotation with an optional grace expiry.** Chosen: it follows rule 7 and bounds the overlap with data
  that already exists.

## Consequences

- A client secret rotates without an outage, and a forgotten delete is bounded by the rotated secret's expiry.
- An expired secret stays listed until it is deleted; it only stops authenticating.
- Every client credential, including a by-value key, stops authenticating at its expiry, not only a rotated secret.
- A client that creates a shared secret with fewer than 256 bits of entropy gets `400`.
- A secret keeps its length across rotations.
- Tests cover the overlap, the grace expiry's bounds, the entropy floor and replacement entropy, the `412` on a stale
  `If-Match`, the `400` for a key and the resolver's expiry filter.

## References

- [`ClientApiEndpointHandler.RotateSecretAsync`](../../src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs)
- [`RotateSecretRequest`](../../src/NCode.Identity.OpenId.Management.Abstractions/Contracts/Secrets/RotateSecretRequest.cs)
- [`DefaultOpenIdClientResolver`](../../src/NCode.Identity.OpenId.Authentication/Clients/DefaultOpenIdClientResolver.cs)
- [`SharedSecretPolicy`](../../src/NCode.Identity.Secrets.Persistence.Abstractions/SharedSecretPolicy.cs)
- [`SecretsManager`](../../frontend/apps/mgmt/src/containers/SecretsManager.tsx)
- [ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md),
  [ADR-1391](1391-a-client-secret-is-a-one-way-verifier-unless-its-method-keys-from-its-utf-8-octets.md),
  [ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-honored-on-every-use.md)
