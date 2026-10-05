# 0050. Secret material is revealed once, only by the client-secret create endpoint

- **Status:** Accepted
- **Date:** 2026-10-05
- **Deciders:** NCode.Identity maintainers

## Context

[ADR-0011](0011-secret-management-api.md) establishes that the management API mints secrets by
**server-side generation only** and that every _retrieval_ path is metadata-only: `SecretResource`
exposes `Use`, `Algorithm`, `CreatedWhen`, `ExpiresWhen`, `SecretType`, and `KeySizeBits`, but never
the `EncodedValue`. `CreateSecretRequest` accepts metadata only; the caller never supplies key
material.

Secrets are **not a flat collection** — every secret is nested beneath the owner that holds it, and the
API addresses it that way: `.../servers/{serverId}/secrets`, `.../tenants/{tenantId}/secrets`,
`.../clients/{clientId}/secrets`. The owner, fixed by the route, determines _who needs the material_:

- **Server** and **tenant** secrets are the server's **own** signing/encryption keys. The server holds
  the private half inside its data-protection boundary and publishes the public half out-of-band through
  JWKS. No party outside the server ever needs the private material. ADR-0011's "material never leaves
  the server" is exactly right here.
- **Client** secrets are the **only** secrets whose value a party _outside_ the server must possess — a
  confidential client authenticating with `client_secret_basic` / `client_secret_post`, or a shared
  `HS256` key. When the server generates a client secret and no path ever returns its material, the
  secret is unusable: the client can never be configured with the value. This is the gap ADR-0011's
  metadata-only posture leaves for the one owner that genuinely needs reveal.

Because the owner is nested and route-fixed, "does this secret need to be revealed?" is answerable
**deterministically from the endpoint**, before any key is generated — not inferred at runtime from the
secret's type. That is a stronger guarantee than a type check: the server- and tenant-secret endpoints
have no legitimate reveal case _at all_, so they should be structurally incapable of returning material.

The forces in tension:

- A client secret is worthless unless its plaintext reaches exactly one external holder, once.
- A server/tenant private key must **never** cross the API boundary, under any path.
- Any path that returns material on **read** (`GET`/list) is a standing liability — every subsequent
  read, cache, log, and audit record becomes a place the secret can leak from — and defeats the
  metadata-only invariant ADR-0011 depends on.

## Decision

Secret material is returned **exactly once**, in the **create response of the client-secret endpoint
only**. The reveal is scoped **by owner first** — a structural, per-endpoint guarantee — and gated
again by secret type as defense-in-depth. No retrieval path (`GET` one, list, effective-settings
preview) on any owner ever returns material. This refines — it does not reverse — ADR-0011:
server-side generation, no BYOK import, and metadata-only _retrieval_ all stand; only the **client**
create _response_ gains a one-time reveal.

Concretely:

- **Only the client-secret create endpoint can reveal, and it says so in its type.**
  `POST .../clients/{clientId}/secrets` returns a `CreatedSecretResource` — a `SecretResource` plus a
  single, create-only `SecretMaterial` property (the unprotected key bytes, base64url-encoded, the same
  encoding as the persisted `EncodedValue` but **unprotected**). The server- and tenant-secret create
  endpoints (`POST .../servers/{serverId}/secrets`, `POST .../tenants/{tenantId}/secrets`) return the
  material-free `SecretResource`. The guarantee is therefore **structural per owner**: those endpoints
  return a type with no material field, so they cannot reveal material no matter how a handler is
  written or what secret type is generated.
- **Within the client endpoint, reveal is still `symmetric`-only (defense-in-depth).**
  `CreatedSecretResource.SecretMaterial` is populated only when `SecretType == symmetric`. For `rsa` and
  `ecc` it is `null`: even on the one endpoint that can reveal, an asymmetric **private** key is never
  returned. A server-generated client secret is a symmetric shared secret, so this is the expected path;
  asymmetric client authentication (`private_key_jwt`) is client-held key import (BYOK), which ADR-0011
  defers. Returning asymmetric **public** material stays the deliberate, additive future option ADR-0011
  reserves, and would ride the same create-only type if adopted.
- **The server never generates an asymmetric keypair _for_ a client and hands back the private key.**
  That pattern — server-minted private material delivered to an external holder — is explicitly
  excluded, which is why the `symmetric`-only rule above is sufficient rather than arbitrary. Asymmetric
  client authentication is `private_key_jwt`, where the client generates its own keypair and registers
  only the **public** half with the server (client-held BYOK, deferred by ADR-0011). Having the server
  generate the private key would put a freshly minted private key on the wire for a key the server has
  no reason to hold — the precise exposure server-side generation exists to avoid — and it is a workflow
  neither the OAuth/OIDC client-authentication model nor the Auth0-parity baseline (ADR-0025) calls for.
  Server-generated client material is therefore symmetric-only by decision, not merely by current
  implementation.
- **Create authorization is the only gate; the reveal is not separately scoped.** Minting the secret
  already requires `Operations.Create` on the client; the material is surfaced only in that one
  authorized response and is never retrievable afterward, so no additional read-material scope is
  introduced. Transport confidentiality is the existing TLS requirement for the whole management API.
- **The one-time reveal never enters any durable or diagnostic sink.** The plaintext lives only in the
  client create HTTP response. Audit events, logs, traces, and the persisted row continue to carry
  metadata and the **data-protected** `EncodedValue` only — never the unprotected `SecretMaterial` —
  preserving ADR-0011's audit posture and ADR-0049's funnel.
- **Rotation inherits the same semantics.** Rotation stays create-new + delete-old (ADR-0011); a future
  convenience "rotate" endpoint on the client reveals identically, and server/tenant rotation never
  reveals. There is no reveal on `PUT` (metadata-only) or any other verb.

## Options considered

- **Scope reveal by secret type alone, on every owner's create endpoint.** Rejected: it makes a
  server/tenant endpoint's safety depend on a runtime `SecretType != symmetric` check, so a symmetric
  server key — a legitimate `HS256` token-signing key the server holds privately — would wrongly be
  revealed. Owner is the correct primary axis: only the client owner has an external holder. Type
  remains a secondary guard _inside_ the client endpoint.
- **Server-generated asymmetric client keypair with private-key hand-back.** Rejected: it would have the
  server mint a private key purely to transmit it to a client, reintroducing the on-the-wire private-key
  exposure server-side generation exists to prevent, for a key the server has no reason to hold.
  Asymmetric client authentication is `private_key_jwt`, where the client generates its own keypair and
  registers only the public half (client-held BYOK, deferred by ADR-0011). This keeps server-generated
  client material symmetric-only by decision.
- **A single shared create handler/response type for all three owners.** Rejected: it would force the
  reveal field onto the server/tenant responses (even if always `null`), reopening leakage as a
  discipline problem. Distinct return types per owner make the server/tenant endpoints structurally
  material-free.
- **Optional `material` field on the shared `SecretResource`.** Rejected: an optional-and-usually-null
  field on the type every read path returns makes leakage a reviewing-discipline problem rather than a
  structural one. A distinct create-only type, used only by the client create path, makes the guarantee
  hold by construction.
- **A retrievable "get client-secret material" endpoint (Auth0 `read:client_keys`-style).** Rejected for
  the default: a standing read path for plaintext is exactly the liability this decision avoids — it
  reopens the metadata-only invariant and multiplies the leak surface across caches, logs, and audit.
  One-time reveal at creation meets the real need (configure the client once) without a durable
  material-read surface. It remains a possible future additive option behind its own review.
- **No reveal at all (status quo).** Rejected: it leaves client secrets unusable by any external party,
  which makes minting them through the management API pointless.

## Consequences

- A confidential client's shared secret can be minted through the management API and handed to the
  client once, closing the gap where client secrets were generated but unusable — while server and
  tenant private keys remain unreachable by construction, not by convention.
- The reveal capability is legible from the route and the type: only `.../clients/.../secrets` returns
  `CreatedSecretResource`; the server and tenant endpoints return `SecretResource` and cannot carry
  material.
- The metadata-only **retrieval** invariant from ADR-0011 is strengthened: it holds on every read path
  for every owner, and the only place material ever appears is one authorized client create response.
- A lost client secret is unrecoverable by design — recovery is rotation (create-new + delete-old), the
  same auditable path ADR-0011 prescribes. This keeps the system free of a standing material-read
  surface.
- `CreatedSecretResource` is new public surface in `NCode.Identity.OpenId.Management.Abstractions`
  (`NCode.Identity.OpenId.Management.Contracts.Secrets`); `SecretResource` is unchanged, so no read
  contract changes, and the server/tenant create contracts are unchanged.
- Asymmetric **public**-material exposure and a retrievable material endpoint both remain clearly
  bounded future options that this decision neither adopts nor forecloses.

## References

- [ADR-0011](0011-secret-management-api.md) — server-side generation and the metadata-only retrieval
  invariant this decision refines.
- [ADR-0002](0002-ephemeral-development-keys.md) — the data-protection boundary that guards
  `EncodedValue`.
- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the server/tenant/client ownership planes
  the reveal scope follows.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the Auth0-parity-plus philosophy that
  frames one-time reveal as parity with familiar client-secret workflows.
- [ADR-0049](0049-openid-errors-audited-from-single-funnel.md) — the audit funnel that must never
  observe the one-time plaintext.
- `NCode.Identity.OpenId.Management.Contracts.Secrets.SecretResource` — the material-free read contract.
- `NCode.Identity.Secrets.Persistence.DataContracts.PersistedSecret` — the data-protected
  `EncodedValue` that backs the reveal.
