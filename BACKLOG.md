# Backlog — known pending work

This file is the **single source of truth for documented known pending work** in NCode.Identity:
deferred decisions, feature gaps, and action items. It is durable, versioned repository content — not
an agent's volatile memory or a chat transcript. This file is the **primary, default view of the backlog** — it
lists **only pending work**, so scanning it is the fastest way to see what is still open. The companion archive
[`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) preserves the full original text of every **completed or retired**
item (keyed by ID, cross-referenced to the [`CHANGELOG.md`](CHANGELOG.md) entry and/or ADR that closed it), so
completed work is just as discoverable without reading git history.

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

Entry convention: a short title, one-line context, and (where useful) the touch points. Because this file tracks
**pending** work only, never mark an entry "Done" and leave it here. When work is fully done, **move** the entry's
full text to [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) in the same change — do not just delete it — appending a
trailing `_Closed:_` line that points at the [`CHANGELOG.md`](CHANGELOG.md) entry and/or ADR that closed it (and note
the change in [`CHANGELOG.md`](CHANGELOG.md) if user-facing). When an entry is only **partially** done, keep it here but
rewrite it so its title and context describe only the **remaining** work (the completed portion is stated as
already-existing context, not as a "done" status).

**Allocating an ID:** take the next free number for the group from the [ID ledger](#backlog-id-ledger--never-reuse-a-number)
below and bump that group's high-water mark — **never reuse or backfill a number**, because ADRs, the changelog, and
code comments reference it long after the entry is gone. The ledger is the quick reference; the authoritative upper
bound for a group is the **maximum** of its high-water mark, the highest pending ID here, and the highest archived ID
in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md) — allocate strictly above that.

---

## Standing design posture (read first)

This family is **greenfield and pre-release** — everything lands on `dev`, nothing is shipped, and there is **no
backward-compatibility to preserve** ([ADR-0025](docs/adr/0025-pre-release-posture-and-auth0-parity-plus.md)).

**Default to the best long-term design.** When a change has a fork between a minimal, compatibility-preserving patch and
the _cohesive, comprehensive, correct_ model, **choose the comprehensive model by default** — do not weigh "how hard is
it to refactor now" or "will this break the public surface." Breaking changes are expected and welcome here: public-API
churn (`PublicAPI.Unshipped.txt`), setting renames/removals, signature changes, moved types, and a clean cutover are all
preferred over shims, adapters, or "add-only" compromises. Only call out a fork explicitly when the _options are
genuinely equivalent in quality_ and the choice is a real value judgment (e.g. a security/semantics trade-off), not when
one option is merely more work.

---

## Backlog ID ledger — never reuse a number

Backlog IDs outlive their entries: ADRs, the [`CHANGELOG.md`](CHANGELOG.md), and code comments reference a number long
after the item is completed and moved out of this file. So **a number is never reused or backfilled.** Allocate the next
ID for a group strictly **above** its high-water mark, then bump the mark here. Every number at or below a mark is
**allocated** — whether still active in the body below, archived in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md)
(completed or retired), or retired without a surviving entry (absorbed into another item). A completed item's full text
is preserved in [`BACKLOG-COMPLETED.md`](BACKLOG-COMPLETED.md), cross-referenced to the [`CHANGELOG.md`](CHANGELOG.md)
entry and/or ADR that closed it.

| Group                                 | Next free | High-water |
| ------------------------------------- | --------- | ---------- |
| `D` — Deferred, intentionally not-yet | `D6`      | `D5`       |
| `F` — Feature gaps                    | `F24`     | `F23`      |
| `T` — Conformance & interop testing   | `T7`      | `T6`       |
| `U` — Frontend presentation platform  | `U13`     | `U12`      |
| `S` — Secrets management              | `S7`      | `S6`       |
| `X` — Tooling & agent customization   | `X3`      | `X2`       |

---

## Deferred — evaluated, intentionally not-yet (group `D`)

- **`D1` — UserInfo access auditing.** The UserInfo endpoint returns end-user PII but is not audited. A
  `userinfo.accessed` audit event would give a PII-access trail for compliance, but it is a read on a
  hot path (one event per request) and the subject is already captured at token issuance. Deferred as
  an opt-in; revisit if a compliance requirement needs per-access PII records.
  Touch points: `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/`,
  `IAuditEventRecorder`.
- **`D5` — Durable / distributed `IJobDispatcher` adapter (transactional outbox, Quartz, or message broker).** The
  generic background job-dispatch port and its in-process, self-scheduling default shipped with
  [ADR-0090](docs/adr/0090-durable-outbound-delivery-dispatch-adapter.md); the in-process adapter retries transient
  failures but loses in-flight jobs on a process crash and does not coordinate across nodes. A deployment that needs
  **crash-durable, at-least-once, cluster-safe** delivery (guaranteed CIBA push or back-channel logout) must register
  its own `IJobDispatcher`. Ship a reference durable adapter — a **transactional outbox** (which also realizes the
  deferred durable-audit outbox of [ADR-0048](docs/adr/0048-durable-audit-delivery-outbox.md) as one adapter of this
  port), a **Quartz** scheduling-cluster adapter (persist jobs in its job store, honoring `DispatchOptions.Delay`
  natively), and/or a **message-broker** adapter (RabbitMQ/SQS/Kafka). A durable adapter that persists a job must
  protect payloads at rest — CIBA push jobs carry issued tokens and logout jobs carry a signed `logout_token`. Deferred
  until a concrete need justifies the persistence/migration (or broker/scheduler) operational cost. Touch points:
  implement `NCode.Identity.Dispatch.IJobDispatcher` in a new adapter package and register it before
  `AddInProcessJobDispatcher` (or omit that call).

## Feature gaps — not yet implemented (group `F`)

- **`F5` — First-class claim-shaping pipeline (Auth0-Actions-equivalent).** Custom claim shaping is already possible
  today by registering an `ICommandHandler` on the subject-claims / UserInfo commands (the mediator pipeline is the
  extension seam, per [ADR-0054](docs/adr/0054-local-account-metadata-projection.md)), but there is no first-class
  surface: a strongly-typed shaping context (event/api), per-key namespaced flattening helpers, or ordering/management.
  Layer these on the existing pipeline additively. (Subject metadata already resolves fresh per issuance by default, per
  [ADR-0055](docs/adr/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md).)
  Touch points: `src/NCode.Identity.OpenId.Authentication/Tokens/Handlers/`,
  `src/NCode.Identity.OpenId.Authentication/Endpoints/UserInfo/Handlers/`.
- **`F7` — DPoP / sender-constrained tokens (RFC 9449 + mTLS, `cnf` claim).** Every token-issuance path hardcodes
  `TokenType = Bearer` with a `// TODO: add support for DPoP` marker, the token-request validators never inspect a
  DPoP proof, and the emitted-claims handlers cannot populate the `cnf` confirmation claim because no
  proof-of-possession subsystem exists. This is the single most recurring code-local TODO theme. Touch points (all
  `src/NCode.Identity.OpenId.Authentication/`): `Endpoints/Authorization/Handlers/DefaultCreateAuthorizationTicketHandler.cs`,
  `Endpoints/Token/AuthorizationCode/DefaultAuthorizationCodeGrantHandler.cs`,
  `Endpoints/Token/AuthorizationCode/DefaultValidateAuthorizationCodeGrantHandler.cs` (`// TODO: validate DPoP`),
  `Endpoints/Token/ClientCredentials/DefaultClientCredentialsGrantHandler.cs`,
  `Endpoints/Token/Password/DefaultPasswordGrantHandler.cs`,
  `Endpoints/Token/RefreshToken/DefaultRefreshTokenGrantHandler.cs`,
  `Tokens/Handlers/DefaultGetAccessTokenPayloadClaimsHandler.cs` /
  `Tokens/Handlers/DefaultGetIdTokenPayloadClaimsHandler.cs` (the `cnf` claim).
- **`F16` — FAPI (Financial-grade API) readiness epic.** An umbrella tracking the work to make the OP certifiable
  against the OpenID Foundation **FAPI** security profiles ([FAPI 1.0 Advanced](https://openid.net/specs/openid-financial-api-part-2-1_0.html),
  [FAPI 2.0](https://openid.net/specs/fapi-security-profile-2_0.html)). FAPI mandates **sender-constrained access
  tokens** (DPoP / mTLS — `F7`), **Pushed Authorization Requests** (implemented — the
  `pushed_authorization_request_endpoint` plus the `require_pushed_authorization_requests` policy), **signed request
  objects and responses** (JAR / JARM — not yet tracked separately), and **strong client authentication**
  (`private_key_jwt` or mTLS — the
  `private_key_jwt` half rides `S3`). It also restricts signing to **`PS256`/`ES256`** and forbids symmetric (`HS256`)
  signing — the always-tenant-asymmetric access-token signing from
  [ADR-0075](docs/adr/0075-runtime-consumption-of-resource-server-secrets.md) already satisfies the algorithm rule; a
  FAPI **profile** would additionally constrain the advertised `*_signing_alg_values_supported` settings to that set.
  This item is the tracking anchor; the concrete deliverables are `F7`, `S3`, and a new JAR/JARM work item.
- **`F21` — End-session reverse-map for a pairwise `id_token_hint`.** Because the pairwise projection is one-way and
  unstored (`F9`), an `id_token_hint` presented at the end-session endpoint carries a pairwise `sub` the OP cannot map
  back to the stable principal. End-session resolves the session by `sid` (which our ID tokens always carry), so the
  practical RP-initiated-logout path is covered, but a hint that carries only a pairwise `sub` (no `sid`, no cookie
  session) cannot revoke by subject. If a stored/rotatable pairwise mapping is ever introduced (a replacement
  `ISubjectIdentifierService`), wire its reverse lookup into `DefaultResolveEndSessionSubjectHandler`. Touch point:
  `src/NCode.Identity.OpenId.Authentication/Endpoints/EndSession/Handlers/DefaultResolveEndSessionSubjectHandler.cs`.- **`F22` — Enforce the `Subject.sub == PrincipalId` invariant at every ticket-construction site.** The principal is now
  resolved uniformly and `SubjectAuthentication.PrincipalId` is the single authoritative subject
  ([ADR-0094](docs/adr/0094-resolve-principal-at-every-authentication-entry-point.md)), and the interactive login path
  re-stamps the cookie's `sub` claim to the resolved id. The ROPC and bearer/UserInfo paths, however, still leave the
  principal's raw `sub` claim (`LocalAccountId` / the token's `sub`) on `SubjectAuthentication.Subject` while
  `PrincipalId` holds the resolved id — harmless today because emission reads `PrincipalId`, not the claim, but a
  latent divergence. Normalize the principal's subject claim to `PrincipalId` at those construction sites too (a shared
  ticket factory), so `Subject.sub` and `PrincipalId` can never disagree and the two-sources-of-truth trap cannot
  return. Touch points: `src/NCode.Identity.OpenId.Accounts.Authentication/DefaultAuthenticatePasswordGrantHandler.cs`,
  `src/NCode.Identity.OpenId.Authentication/Subject/DefaultAuthenticateSubjectHandler.cs`.
- **`F23` — OAuth 2.0 Token Exchange (RFC 8693).** Implement the token-exchange grant
  (`urn:ietf:params:oauth:grant-type:token-exchange`) so the token endpoint can mint delegation/impersonation tokens
  from a presented `subject_token` (and optional `actor_token`). The routing seam already exists — register an
  `ITokenGrantHandler` whose `GrantTypes` includes the grant URI and the token endpoint dispatches to it with no
  endpoint changes (see `ITokenGrantHandler` remarks). Deliverables: a token-exchange request model binding the
  RFC 8693 parameters (`subject_token`, `subject_token_type`, `actor_token`, `actor_token_type`,
  `requested_token_type`, `resource`, `audience`, `scope`), validation of the presented token types
  (`urn:ietf:params:oauth:token-type:*`), an authorization policy governing which clients may exchange and which
  delegation/impersonation targets are permitted, `act`/`may_act` claim handling for delegation chains, and the
  `issued_token_type` response field. Surfaces in the conformance coverage report as the currently-missing
  `grant-token-exchange` probe (`src/frontend/apps/conformance/src/coverage.ts`). Touch point: a new
  `DefaultTokenExchangeGrantHandler` under
  `src/NCode.Identity.OpenId.Authentication/Endpoints/Token/`.

## Conformance & interop testing (group `T`)

- **`T1` — Independent OIDC/OAuth conformance validation (OpenID Foundation Conformance Suite).** Our own
  integration tests encode our own reading of the specs; an independent, spec-author-maintained oracle is
  needed to catch shared misreadings. The full design and automation plan — harness architecture, the
  `BrowserControl`/`run-test-plan.py` automation model, target profiles, the reachable-host /
  automatable-login / discovery-alignment prerequisites, and the nightly (not `dod.ps1`) CI wiring — lives
  in its dedicated doc: [`docs/conformance-testing.md`](docs/conformance-testing.md). The Dynamic OP profile
  prerequisite (Dynamic Client Registration) is implemented ([ADR-0063](docs/adr/0063-dynamic-client-registration.md));
  the harness enables anonymous registration (`registration_endpoint_allow_anonymous`). Depends on the prerequisite
  work item `T3` below (the reachable HTTPS host the harness drives). The browser-automatable login/consent surface
  prerequisite is **done** (`T2` — the OP-hosted `apps/auth` interactive login/consent surface,
  [ADR-0065](docs/adr/0065-interactive-login-consent-op-hosted-auth-surface.md)). The deterministic conformance tenant,
  clients, and test-user seeding prerequisite is **done** (`T4` — `AddConformanceSeed`). The discovery-metadata
  alignment prerequisite is **done**: the OP advertises secure-by-default, verified capabilities
  ([ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)).
- **`T3` — Reachable HTTPS Playground host for the external harness.** The only launchable host is the
  in-process `TestServer`; the suite needs a real HTTPS origin (Windows/Docker: `host.docker.internal`)
  whose discovery `issuer` byte-matches that origin (pin via the `tenant_issuer` setting). Detailed plan:
  [`docs/conformance/reachable-host.md`](docs/conformance/reachable-host.md).
- **`T6` — Spec-coverage conformance harness (in-house gap & regression dashboard).** Evolve `apps/conformance` from a
  discovery _security audit_ into a _spec-coverage matrix_ that measures the OP against the **full published
  OAuth/OIDC/CIBA/FAPI catalog**, independent of what is implemented, so missing functionality (CIBA `F3`, DPoP/mTLS
  `F7`, token exchange, the FAPI profile `F16`; PAR, pairwise `F9`, `acr`/`amr`
  emission, and essential-`acr` enforcement `F19` are implemented) surfaces as explicit,
  attributable gap rows. Because every row is classified from **live discovery metadata**, the same matrix is a **regression**
  detector: a capability that silently stops being advertised (or flips to non-conformant) flips its row, so the harness
  guards _shipped_ features as well as tracking _missing_ ones. Design and
  rationale, including the `browser` / `probe-runner` / `external-suite` exercise boundary, are in
  [ADR-0084](docs/adr/0084-spec-coverage-conformance-harness.md). Phases: **(1 — done)** the data-first spec catalog
  (`coverage.ts`) + coverage-matrix UI, classified from live discovery metadata (semantics: _advertised_, per the
  secure-by-default subset [ADR-0083](docs/adr/0083-secure-by-default-discovery-metadata.md)); **(2)** browser-drivable
  active probes (PAR construction, response-mode matrix, `prompt`/`max_age`/`acr`/`claims` behavior, negative/error
  paths) that refine _advertised_ into _verified_; **(3)** a headless Node/backend **probe runner** for the confidential
  and server-initiated legs a browser cannot drive (`client_credentials`, introspection/revocation with client auth,
  `private_key_jwt`, device-code polling, back-channel logout receipt, DPoP/mTLS, CIBA poll/ping once `F3` lands);
  **(4)** wire the external OpenID Foundation Conformance Suite (`T1`) as the certified oracle with this matrix as the
  fast pre-flight. The harness is a **measurement** surface — it tracks the backend gaps above, it does not implement
  them. Touch point: [`src/frontend/apps/conformance`](src/frontend/apps/conformance).

## Frontend presentation platform (group `U`)

The platform foundation landed under [`src/frontend`](src/frontend) with the Management console as the first surface
([ADR-0064](docs/adr/0064-frontend-presentation-platform.md)). Remaining work builds additively on it.

- **`U5` — Frontend CI job.** The frontend toolchain is intentionally outside `dod.ps1`. Add a separate CI job
  (`corepack` + `pnpm install` + `turbo run lint typecheck test build`) so the platform is gated independently.
- **`U6` — Per-tenant branding.** `@ncode/ui` themes from a runtime `BrandingConfig` (the seam exists); wire
  end-to-end per-tenant brand/theme resolution through the discovery channel for customer-facing surfaces.

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
- **`S5` — Client-secret storage: hash-at-rest vs reversible protection (needs its own ADR).** A client secret
  is reveal-once and never returned again, so the server only needs to _verify_ a presented value — a
  one-way hash (OWASP-preferred) is safer than today's reversible data-protected `EncodedValue`. But
  symmetric secrets used as actual signing keys (`HS256`) require reversible material. The
  authentication-secret vs signing-key fork deserves an ADR before changing storage.
  Touch points: `src/NCode.Identity.Secrets.Persistence.Abstractions/DataContracts/PersistedSecret.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretGenerator.cs`,
  `src/NCode.Identity.Secrets.Persistence/Logic/DefaultSecretSerializer.cs`.

## Tooling & agent customization (group `X`)

- **`X1` — `audit-conventions` duplicated as prompt + skill.** The convention-audit workflow now exists **twice**:
  the original VS Code prompt [`.github/prompts/audit-conventions.prompt.md`](.github/prompts/audit-conventions.prompt.md)
  and a migrated Agent Host skill [`.github/skills/audit-conventions/SKILL.md`](.github/skills/audit-conventions/SKILL.md)
  (prompt files are not loaded by Agent Host / Copilot CLI, which reads `SKILL.md`). The skill originally carried
  `disable-model-invocation: true` to preserve the prompt's explicit-invocation-only semantics, but that flag makes the
  skill **unreachable** in Copilot CLI (it returns "Skill not found" even on explicit invocation — upstream bug
  github/copilot-cli#4438), so it was **removed**. **Trade-off accepted:** the skill is now both explicitly invocable
  **and** model-auto-invocable (it no longer enforces explicit-only invocation); its `description` is the only guard on
  when the model reaches for it. The prompt's `agent: "agent"` frontmatter has no skill equivalent and was dropped.
  **Runtime-validated (2026-10-10):** the skill was confirmed discoverable and loadable in a live Agent Host / Copilot
  CLI session (it surfaced in the session's available-skills list), and its five relative links and LF line endings were
  verified; the body is identical to the prompt apart from the intended `+1 ../` link-depth adjustment. **Decision still
  deferred:** whether to delete the prompt (Agent-Host-only) or keep both (dual VS-Code-chat + Agent-Host support). Keep
  both for now — the prompt still serves VS Code chat via its `agent:` frontmatter, and `disable-model-invocation: true`
  is intended to return once github/copilot-cli#4438 is fixed (restoring explicit-only semantics). If kept, edits must be
  mirrored across both files to avoid drift.
