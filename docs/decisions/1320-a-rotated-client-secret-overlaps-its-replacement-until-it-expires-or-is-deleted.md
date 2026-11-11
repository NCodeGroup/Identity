# 1320. A rotated client secret overlaps its replacement until it expires or is deleted

- **Status:** Accepted (amended by [ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md): the replacement is issued through `IClientSecretIssuer` in the form the client's method needs, and both `ISecretGenerator` shared-secret generators, `GenerateSharedSecretVerifier` and `GenerateReversibleSharedSecret`, enforce the entropy floor; by
  [ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md):
  a stored key's expiry is honored wherever the key is used, not only when a client authenticates)
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0146](project-template/0146-secrets-are-never-logged-compared-in-constant-time-and-shown-once.md)

## Context

A secret's key material is immutable once generated, so rotating a secret means creating a new one and deleting the
old one ([ADR-1011](1011-secret-management-api.md)). Template ADR-0146 rule 7 rotates a secret by create, switch,
delete: the holder switches to the replacement while the old secret still works, then the old secret is deleted.

A rotation that removes the rotated secret in the same change breaks every caller that holds it until it is
reconfigured. A rotation that keeps it needs a bound, or a forgotten delete leaves a stale credential accepted
forever, and that bound only works when a credential past its `ExpiresWhen` stops authenticating. Signing-key
rollover and the expiry of every other stored key, a tenant's, a server's or a resource server's, are tracked in issue
[#166](https://github.com/NCodeGroup/identity/issues/166); a client credential's expiry is the part rotation needs.

A client's shared secret stores the UTF-8 length of its base64url string as `KeySizeBits`, not its entropy
([ADR-1110](1110-shared-secret-vs-cryptographic-key-storage.md)), so copying the stored size as the replacement's
entropy grows the secret on every rotation. Nothing sets a floor on a generated secret's entropy (template ADR-0146
rule 4).

## Decision

`POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate` adds a replacement and keeps the rotated
secret. Both authenticate until the rotated secret expires or is deleted. This supersedes
[ADR-1183](1183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md), which replaced the secret in one change.

- **Rotation overlaps.** The server generates a replacement of the rotated secret's type, `use`, and `alg`, with the
  expiry the request names (`RotateSecretRequest.expiresWhen`), and adds it beside the rotated secret. The operator
  deletes the rotated secret with the existing `DELETE` once the holder has switched.
- **A grace expiry bounds the overlap.** The optional `RotateSecretRequest.rotatedExpiresWhen` sets the rotated
  secret's expiry to the earlier of its current `ExpiresWhen` and this value: rotation never extends it, and a past
  value stops it at once. Omitted, the rotated secret keeps its expiry. The Management console sends seven days.
- **A client credential past its expiry stops authenticating.** `DefaultOpenIdClientResolver` drops every persisted
  client secret whose `ExpiresWhen` is not after now before it deserializes the rest, so an expired secret or
  by-value key stops authenticating through every client authentication method. Only client authentication filters
  by expiry. A resource server's keys protect the tokens issued for it and are not credentials a caller presents (a
  resource server authenticates through its bound client), so their expiry, like every other stored key's, is issue
  #166's. A dynamic client registration read reports every registered key, expired or not, because it describes what
  is registered.
- **A generated shared secret has at least 256 bits of entropy.** `SharedSecretPolicy.MinimumEntropyBits` is the
  floor. `ISecretGenerator.GenerateSharedSecret` throws below it, the client secret create endpoint answers `400`,
  and dynamic client registration uses the constant. Server, tenant, and resource-server symmetric keys are
  cryptographic keys, not shared secrets presented by an external party, and keep their own descriptor floor.
- **The replacement keeps the rotated secret's entropy.** Its entropy is
  `max(256, (KeySizeBits / 8 * 3 / 4) * 8)`, which inverts the base64url length of a generated secret exactly, so a
  secret does not grow with each rotation.
- **Authorized and audited as the calls it performs.** The caller must be allowed to create a secret for the client
  and to update the rotated secret; rotation performs a create and an update and never deletes. The audit trail
  records the replacement's `Created` and, only when the expiry changed, the rotated secret's `Updated`, without
  material. An `If-Match` header, when sent, must carry the rotated secret's concurrency token. The response is
  `201 Created` with the replacement's `CreatedSecretResource`, the only place its material appears
  ([ADR-1050](1050-secret-material-revealed-once-on-create.md)).
- **Only a shared secret is rotated.** A key is the client's to hold; rotating one answers `400`, and the client
  imports the new key and deletes the old one.

Storage is unchanged. A `client_secret_jwt` secret is a key the server itself uses, which template ADR-0146 rule 6
allows to be stored protected by a key held outside the store
([ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). Verify-only secrets
remain reversible until issue [#163](https://github.com/NCodeGroup/identity/issues/163) hashes them; ADR-1110 records
that conflict.

## Options considered

- **Keep the one-call replacement.** Rejected: it breaks every caller at once and contradicts rule 7. Auth0 and
  Entra ID both let the old secret work while the holder switches.
- **Delete the rotated secret after a server-side grace job.** Rejected: expiry enforcement already ends the overlap
  with no job to schedule, run, or fail.
- **Require a grace expiry on every rotation.** Rejected: an operator who deletes the rotated secret by hand has no use
  for one, and the rotated secret's own expiry is already a bound.

## Consequences

- A client secret rotates without an outage, and a forgotten delete is bounded by the rotated secret's expiry.
- An expired secret stays listed until it is deleted; it only stops authenticating.
- Every client credential, including a by-value key, stops authenticating at its expiry, not only a rotated secret.
- The Management console rotates with a replacement that expires a year out and keeps the current secret seven days.
- A client that creates a shared secret with fewer than 256 bits of entropy gets `400`.

## References

- [`ClientApiEndpointHandler.RotateSecretAsync`](../../src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs)
- [`RotateSecretRequest`](../../src/NCode.Identity.OpenId.Management.Abstractions/Contracts/Secrets/RotateSecretRequest.cs)
- [`DefaultOpenIdClientResolver`](../../src/NCode.Identity.OpenId.Authentication/Clients/DefaultOpenIdClientResolver.cs)
- [`SharedSecretPolicy`](../../src/NCode.Identity.Secrets.Persistence.Abstractions/SharedSecretPolicy.cs)
- [ADR-1011](1011-secret-management-api.md), [ADR-1110](1110-shared-secret-vs-cryptographic-key-storage.md),
  [ADR-1183](1183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md),
  [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)
- Issues [#163](https://github.com/NCodeGroup/identity/issues/163) and [#166](https://github.com/NCodeGroup/identity/issues/166)
