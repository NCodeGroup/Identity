# Backlog — known pending work

This file is the **single source of truth for documented known pending work** in NCode.Identity:
deferred decisions, feature gaps, and action items. It is durable, versioned repository content — not
an agent's volatile memory or a chat transcript.

**This catalog is not exhaustive.** It lists only work that has already been _identified_ and
deliberately written down — primarily durable storage for AI chat sessions and items a human asked to
record. Unidentified work, and pending work tracked elsewhere (issues, `// TODO:` comments, ADR
follow-ups, test gaps), also exists and is not necessarily duplicated here. Absence from this file is
not evidence that something is done or that nothing is pending.

**For contributors and automated agents:** when you identify pending/deferred/follow-up work during a
change, record it **here** (and, for a non-obvious _decision_, in an [ADR](docs/adr/)) rather than
leaving it only in chat history or agent memory. This follows
[ADR-0008](docs/adr/0008-conventions-and-lessons-live-in-the-repository.md): conventions, reasoning,
and recurring work live in the repository. Code-local reminders may also use `// TODO:` comments; this
file is the catalog that makes those discoverable in one place.

Entry convention: a short title, one-line context, and (where useful) the touch points. Remove an
entry when the work lands (and note it in [`CHANGELOG.md`](CHANGELOG.md) if user-facing).

---

## Deferred — evaluated, intentionally not-yet (group `D`)

- **`D1` — UserInfo access auditing.** The UserInfo endpoint returns end-user PII but is not audited. A
  `userinfo.accessed` audit event would give a PII-access trail for compliance, but it is a read on a
  hot path (one event per request) and the subject is already captured at token issuance. Deferred as
  an opt-in; revisit if a compliance requirement needs per-access PII records.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/`,
  `IAuditEventRecorder`.

## Feature gaps — not yet implemented (group `F`)

- **`F1` — End-session / RP-initiated logout endpoint.** No OpenID Connect end-session endpoint, session
  termination, or back-channel logout notification exists. Until it does, there is no logout operation
  to audit. (ID-token `sid` claim for logout is also not yet emitted.)
- **`F2` — Consent persistence and enforcement.** The authorize flow negotiates `prompt` and audits the
  grant/deny decision, but there is no persisted consent record or scope pre-approval. See
  `// TODO: check consent` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultAuthorizeHandler.cs`.
- **`F3` — `device_code` (RFC 8628) and CIBA grants.** Not implemented (TODOs in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/`).
- **`F4` — `refresh_token_reuse_policy` (none / revoke_all).** Refresh-token replay is _detected and audited_
  (`token.refresh.replay`), but there is no configurable response that revokes the whole token family
  on reuse. See `// TODO: refresh_token_reuse_policy` in
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/RefreshToken/DefaultRefreshTokenGrantHandler.cs`.

## Secrets management (group `S`)

Follow-ups surfaced while implementing [ADR-0050](docs/adr/0050-secret-material-revealed-once-on-create.md)
(client-secret material revealed once on create).

- **`S1` — Client-secret rotate endpoint.** Rotation is create-new + delete-old (two calls) per
  [ADR-0011](docs/adr/0011-secret-management-api.md); ADR-0050 notes a convenience `rotate` would inherit
  the same one-time reveal. A `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate`
  returning `CreatedSecretResource` is the natural next increment.
  Touch points: `src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs`.
- **`S3` — Key import / BYOK (needs its own ADR).** Secret creation is server-generation-only; there is no way
  to onboard a `private_key_jwt` client (client generates its keypair and registers the **public** half)
  or to import an `x509` certificate — `SecretTypes.Certificate` is defined but `DefaultSecretGenerator`
  refuses it. ADR-0011 explicitly defers import; the `GenerateSecretRequest` `// Future:` note reserves
  room. Import needs its own validation + threat-model ADR before implementation.
  Touch points: `src/NCode.Identity.Secrets.Persistence.Abstractions/Logic/GenerateSecretRequest.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretGenerator.cs`.
- **`S4` — Asymmetric public-material exposure.** No read path returns public key material (JWK/PEM) for a
  client's registered public key or for server/tenant signing keys. ADR-0011/ADR-0050 reserve this as a
  deliberate additive option (public material only; private keys stay unreachable).
  Touch points: `src/NCode.Identity.OpenId.Management.Abstractions/Contracts/Secrets/`.
- **`S5` — Client-secret storage: hash-at-rest vs reversible protection (needs its own ADR).** A client secret
  is reveal-once and never returned again, so the server only needs to _verify_ a presented value — a
  one-way hash (OWASP-preferred) is safer than today's reversible data-protected `EncodedValue`. But
  symmetric secrets used as actual signing keys (`HS256`) require reversible material. The
  authentication-secret vs signing-key fork deserves an ADR before changing storage.
  Touch points: `src/NCode.Identity.Secrets.Persistence.Abstractions/DataContracts/PersistedSecret.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretGenerator.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretSerializer.cs`.

## Consistency / refactors (group `R`)

- **`R1` — Unify subject-id extraction from a `ClaimsPrincipal`.** The canonical, configurable extraction is
  `OpenIdOptions.GetSubjectId` (default `sub` → `nameidentifier` → `upn`), but `DefaultManagementAuditRecorder.GetActorId`
  re-implements a hardcoded subset (`sub ?? nameidentifier`), and `DefaultPrincipalResolver` uses a _different_
  configurable mechanism (the `PrincipalSourceClaim` tenant setting). The audit recorder cannot simply call
  `GetSubjectId` because that delegate lives in `OpenId.Core` and the Management package may depend only on
  `*.Abstractions` ([ADR-0016](docs/adr/0016-implementation-packages-depend-only-on-abstractions.md)). The fix is to
  hoist a single subject-id extraction contract into an abstractions package (or converge on the `PrincipalSourceClaim`
  setting) so all three paths share one source of truth.
  Touch points: `src/NCode.Identity.OpenId.Management/Auditing/DefaultManagementAuditRecorder.cs`,
  `src/NCode.Identity.OpenId.Core/Logic/DefaultClaimsPrincipalLogic.cs`,
  `src/NCode.Identity.OpenId.Core/PrincipalResolution/DefaultPrincipalResolver.cs`.
