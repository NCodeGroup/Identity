# 95. Registration-time `sector_identifier_uri` fetch and validation with an SSRF-guarded client

- **Status:** Accepted
- **Date:** 2026-10-14
- **Deciders:** NCode Group

## Context

Pairwise subject identifiers ([ADR-0093](0093-pairwise-subject-identifiers.md)) resolve a client's pairwise **sector**
from the host of its `sector_identifier_uri` client setting when present, otherwise the common host of its
`redirect_uris`. That ADR accepted the configured `sector_identifier_uri`'s host **without fetching** the document it
references, and split the remote step out as issue [#58](https://github.com/NCodeGroup/Identity/issues/58).

OpenID Connect Core 1.0 §8.1 defines that remote step. When a client registers a `sector_identifier_uri`:

- the URI **MUST** use the `https` scheme;
- the OP **fetches** the document, which is a **JSON array of `redirect_uri` strings**;
- the OP **verifies** that every one of the client's registered `redirect_uris` is present in that array.

The check is what lets several clients **share a sector** (and therefore a stable pairwise `sub`) only when a party that
controls the `sector_identifier_uri` document has vouched for their redirect URIs — otherwise any client could claim any
host as its sector and either correlate or impersonate another sector's pseudonyms. §8.1 also requires a
`sector_identifier_uri` for a pairwise client whose `redirect_uris` span **multiple hosts** (the sector would otherwise
be ambiguous).

A server-side fetch of a client-supplied URL is a textbook **server-side request forgery (SSRF)** sink: left
unguarded, a caller could point `sector_identifier_uri` at `http://169.254.169.254/…` (cloud metadata),
`http://127.0.0.1/…`, or an internal host and use the OP as a confused deputy. Dynamic Client Registration
([ADR-0063](0063-dynamic-client-registration.md)) makes this reachable by untrusted callers (anonymously when
`registration_endpoint_allow_anonymous` is enabled for the conformance harness), so the fetch must be hardened.

## Decision

**Perform the §8.1 fetch-and-validate at registration and update time through a replaceable
`ISectorIdentifierValidator` seam whose default implementation uses a dedicated, SSRF-guarded HTTP client.** The
synchronous metadata validation keeps the cheap, pure checks (`subject_type` is supported; a pairwise client with
multiple redirect hosts must supply a `sector_identifier_uri`); the asynchronous network step is isolated behind the
new seam.

Concretely:

- **`subject_type` and `sector_identifier_uri` are now first-class registration metadata.** The dynamic-registration
  request/response (RFC 7591) carry `subject_type` and `sector_identifier_uri`, projected onto the same client settings
  document the runtime already resolves (`OpenIdSettingNames.SubjectType` / `SectorIdentifierUri`), so a registration
  round-trips through the management endpoint (RFC 7592). `subject_type` is constrained to `public` / `pairwise`.

- **`ISectorIdentifierValidator` is the deployment seam** (sibling of `IInitialAccessTokenValidator`,
  `TryAddSingleton`, DI-overridable). `ValidateAsync(context, sectorIdentifierUri, redirectUris, ct)` returns
  `null` when valid or an `IOpenIdError` (`invalid_client_metadata`) otherwise. The endpoint invokes it from both
  `POST /register` and `PUT /register/{client_id}`, only when a `sector_identifier_uri` is present.

- **The default implementation enforces the §8.1 contract**: the URI must be absolute `https`; the document must be a
  JSON array of strings; and every registered redirect URI must appear in it (ordinal match).

- **The fetch is guarded by four layered controls:**
    1. **Always-on private-address block (SSRF floor).** A dedicated named `HttpClient`
       (`NCode.Identity.OpenId.SectorIdentifier`) uses a `SocketsHttpHandler` whose `ConnectCallback` resolves the host
       and **pins the socket to the resolved IP**, refusing to connect to loopback, private (RFC 1918), CGNAT, link-local,
       unique-local, multicast, and reserved/test ranges (IPv4 and IPv6, including IPv4-mapped IPv6). Pinning the vetted
       address defeats DNS-rebinding (the name is resolved once and connected to that answer), and `AllowAutoRedirect =
false` stops a redirect from bouncing the fetch to an internal host. This floor is **not** configurable off.
    2. **Optional host allow-list.** `sector_identifier_uri_allowed_hosts` (set-valued, merges by `Intersect`; unset =
       no host restriction per [ADR-0010](0010-supported-settings-unset-means-unrestricted.md)) further restricts which
       hosts may be fetched.
    3. **Timeout.** `sector_identifier_uri_fetch_timeout` (default 10s, merges by `Min`) bounds the wall-clock fetch via a
       linked `CancellationTokenSource`; a timeout is reported as `invalid_client_metadata`, distinct from caller
       cancellation.
    4. **Response size cap.** `sector_identifier_uri_max_size_bytes` (default 100 KB, merges by `Min`) rejects an
       oversized document both by the advertised `Content-Length` and by a hard cap while streaming, so a lying or absent
       `Content-Length` cannot exhaust memory.

- **Registration-time enforcement of the multi-host rule is stricter than runtime resolution.** `ValidateMetadata`
  rejects a pairwise DCR client whose `redirect_uris` span multiple hosts without a `sector_identifier_uri`. The
  runtime `DefaultSubjectIdentifierService` still falls back to treating such a client as its own sector (ADR-0093) —
  that fallback remains for clients created outside DCR (management API / seeding), where no registration validation
  runs — but a client coming through DCR must satisfy §8.1 up front.

## Options considered

- **Keep accepting the configured host without fetching (status quo from ADR-0093).** Rejected. It is the explicit
  [#58](https://github.com/NCodeGroup/Identity/issues/58) gap: without the fetch, sector membership is unverified, so a client can claim another party's host as its
  sector. The greenfield posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) prefers the correct,
  spec-complete model.

- **Fetch with the shared default `HttpClient` (as the `request_uri` loader does today).** Rejected for this sink. The
  `sector_identifier_uri` is attacker-controlled at an anonymous-reachable endpoint; it needs the private-address block,
  no-redirect, timeout, and size cap, which the shared client does not provide. (Hardening the other outbound fetches —
  `request_uri`, `jwks_uri` — the same way is tracked separately; this ADR scopes the control to the §8.1 fetch.)

- **Block private addresses by inspecting only the URL host string.** Rejected. A hostname can resolve to a private
  address (DNS rebinding), and a literal can be obfuscated; the only robust check resolves the address and pins the
  socket to it, which is what the `ConnectCallback` does.

- **Make the private-address block a toggleable setting.** Rejected as the default seam. The block is the
  non-negotiable SSRF floor; a deployment that must reach a non-public sector host supplies its own
  `ISectorIdentifierValidator`. A `sector_identifier_uri` is public by nature (relying parties fetch it too), so the
  floor does not impede legitimate use.

## Consequences

- The OP now performs the full §8.1 registration-time validation, closing issue [#58](https://github.com/NCodeGroup/Identity/issues/58); a pairwise client can only
  share a sector when the `sector_identifier_uri` document vouches for its redirect URIs.
- The server-side fetch is SSRF-hardened by default (private-address block + no-redirect + timeout + size cap), with an
  optional host allow-list for deployments that want to narrow it further.
- `subject_type` and `sector_identifier_uri` are accepted, validated, persisted, and round-tripped by Dynamic Client
  Registration / management, so a registering client can request pairwise and configure its sector without the
  management API.
- The fetch policy is a replaceable service; a deployment with a different retrieval or trust model (for example an
  internal sector registry) supplies its own `ISectorIdentifierValidator` without touching the endpoint.
