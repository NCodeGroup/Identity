# 183. A client secret is rotated by replacing it in one change

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

A secret's key material is immutable once generated, so rotating a secret means creating a new one and deleting the
old one ([ADR-0011](0011-secret-management-api.md)). The material of a created client secret is revealed once, in the
create response ([ADR-0050](0050-secret-material-revealed-once-on-create.md)). Rotating a client secret therefore took
two calls, and an operator who lost the second call between them left the client with two live secrets, the old one
still accepted. Auth0 rotates a client secret in one call.

A secret's `expiresWhen` is recorded but not enforced: nothing rejects a secret past its expiry (backlog `S8`). A
rotation that kept the old secret "until it expires" would keep it forever.

## Decision

`POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate` replaces a client secret in one change.

- **The replacement matches the rotated secret.** The server generates a new shared secret with the rotated secret's
  type, size, `use`, and `alg`, and the expiry the request names (`RotateSecretRequest.expiresWhen`). It is checked
  as a created secret is, and generated as a client's shared secret is, as the UTF-8 bytes of an opaque string
  ([ADR-0110](0110-shared-secret-vs-cryptographic-key-storage.md)).
- **One unit of work.** The replacement is added and the rotated secret removed in one save, so the client never has
  both or neither. The rotated secret stops being accepted at once. A client that needs the two to overlap creates the
  replacement and deletes the rotated secret in two calls, as before.
- **Authorized as the two calls it replaces.** The caller must be allowed to create a secret for the client and to
  delete the rotated secret, so rotation grants nothing those calls do not. An `If-Match` header, when sent, must carry
  the rotated secret's concurrency token.
- **Revealed once.** The response is `201 Created` with the replacement's `CreatedSecretResource`, the only place its
  material appears. The audit trail records the replacement's creation and the rotated secret's deletion, without
  material.
- **Only a shared secret is rotated.** A key the client signs or decrypts with is the client's to hold: it is imported
  or published at the client's `jwks_uri`, and the server has no material to replace it with. Rotating a key answers
  `400`; the client imports the new key and deletes the old one.

## Options considered

- **Keep the rotated secret until a grace period ends.** Rejected for now: expiry is not enforced, so the grace period
  would never end. Overlapping validity belongs with key lifecycle (backlog `S8`); until then the two-call path gives
  an operator the overlap explicitly.
- **Rotate keys too, generating a key pair of the same type.** Rejected: the server would hold the private key of a
  credential the client owns, and the persisted secret does not record an elliptic curve to repeat.
- **Default the replacement's expiry to the rotated secret's lifetime.** Rejected: the rotated secret's expiry may be
  past or arbitrary; a created secret names its expiry, and a replacement does too.

## Consequences

- A client secret is rotated in one call, and the rotated secret is never left behind.
- A client using the rotated secret fails authentication from the moment of rotation until it is reconfigured.
- Rotating a tenant's, a server's, or a resource server's secret, and showing a rotate action in the Management
  console, are not part of this endpoint.

## References

- [`ClientApiEndpointHandler.RotateSecretAsync`](../../src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs)
- [`RotateSecretRequest`](../../src/NCode.Identity.OpenId.Management.Abstractions/Contracts/Secrets/RotateSecretRequest.cs)
- [ADR-0011](0011-secret-management-api.md), [ADR-0050](0050-secret-material-revealed-once-on-create.md),
  [ADR-0110](0110-shared-secret-vs-cryptographic-key-storage.md)
