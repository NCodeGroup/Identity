# 106. Runtime `jwks_uri` client-key resolution and a shared, extensible outbound-fetch SSRF guard

- **Status:** Accepted
- **Date:** 2026-10-15
- **Deciders:** NCode Group

## Context

FAPI 2.0 readiness (backlog `F16`) requires strong client authentication. `private_key_jwt`
([ADR-0102](0102-jwt-bearer-client-authentication.md)) and signed request objects (JAR, RFC 9101) both verify a
client-signed JWT against the client's **public** keys. A client may onboard those keys two ways:

- **inline JWK Set** at registration (static, stored as verify-only secrets, [ADR-0104](0104-import-public-asymmetric-secret-material.md)); or
- a **`jwks_uri`** the client controls, which the OP resolves **at runtime** so the client can rotate keys without
  re-registering. This is the **primary** onboarding path for FAPI.

Before this ADR the client's `jwks_uri` registration metadata was **stored but never consumed**: the client's effective
key set (`OpenIdClient.SecretKeys`) was built from stored secrets only, so a `private_key_jwt` or JAR assertion signed
with a key published only at `jwks_uri` could never validate.

Resolving `jwks_uri` is a server-side fetch of a **client-supplied URL** — a textbook **server-side request forgery
(SSRF)** sink, the same class of risk the registration-time `sector_identifier_uri` fetch already defends against with a
dedicated SSRF-guarded HTTP client ([ADR-0095](0095-sector-identifier-uri-fetch-and-ssrf-guard.md)). That floor lived in
a `sector_identifier`-specific static helper. A third outbound client-URL fetch — the JAR `request_uri` fetch (RFC 9101)
— used a plain unnamed `HttpClient` that followed redirects and applied **no** SSRF control at all.

## Decision

**Resolve a client's `jwks_uri` at runtime behind a replaceable `IClientKeyResolver` seam, and guard every
server-initiated outbound fetch of a client-supplied URL through one shared, injectable, extensible SSRF guard.**

### Runtime `jwks_uri` resolution

- **`IClientKeyResolver` is the seam** (public, `TryAddSingleton`, DI-overridable). `ResolveSecretKeysAsync(openIdClient,
ct)` returns the client's **stored** `SecretKeys` **merged** with the public keys resolved from its `jwks_uri` setting;
  when no `jwks_uri` is configured it returns the stored keys unchanged. The two paths that verify a client-signed JWT
  against the client's keys — the `client_assertion` handlers (`private_key_jwt` / `client_secret_jwt`) and the JAR
  loader — resolve keys through this seam instead of reading `OpenIdClient.SecretKeys` directly.

- **JWK → runtime `SecretKey` reuses the existing import pipeline.** Each JWK in the fetched set is imported via
  `ISecretImporter` (the ADR-0104 import path) and materialized with `ISecretSerializer`, so RSA/EC parsing, validation,
  and the key-type taxonomy are shared with inline import rather than reimplemented. A JWK of an unsupported `kty`, or a
  malformed supported key, is **skipped** so one bad key never fails an otherwise-usable set.

- **The fetch is bounded and cached.** A dedicated named `HttpClient` (`NCode.Identity.OpenId.ClientJwks`) applies a
  wall-clock timeout (`jwks_uri_fetch_timeout`), a response-size cap (`jwks_uri_max_size_bytes`), an optional host
  allow-list (`jwks_uri_allowed_hosts`), and requires absolute `https`. A successful fetch is cached per URI for
  `jwks_uri_cache_duration`. The five `jwks_uri*` settings mirror the `sector_identifier_uri*` family and are
  non-discoverable per-client ceilings (allow-list intersects; timeout / size / cache-duration take the minimum).

- **Resolution fails closed.** An unreachable, oversized, disallowed, or malformed `jwks_uri` logs a warning and falls
  back to the client's **stored** keys only — it never throws and never grants access, so a missing key set simply fails
  signature verification.

- **Cached keys need no disposal.** A `SecretKey` holds public key material and mints a fresh `RSA`/`ECDsa` per export,
  so a cached public key owns no native handle; the cache uses a plain absolute expiration with no eviction cleanup, and
  the merged collection is a non-owning view over the client's stored keys plus the cached keys.

### Shared, extensible outbound-fetch SSRF guard

- **One guard for every client-URL fetch.** The `sector_identifier`-specific static helper is generalized into an
  injectable `IOutboundHttpForgeryGuard` whose default implementation is wired as the `ConnectCallback` of the named
  HTTP client **each** outbound fetch uses: `sector_identifier_uri`, `jwks_uri`, **and** the JAR `request_uri` fetch,
  which previously had no guard and followed redirects. Every such client now pins the socket to the resolved public IP
  and disables auto-redirect.

- **The private/reserved-address floor is a non-weakenable security control.** The hardcoded deny-list is the IANA
  special-use address registry (RFC 6890 and friends: loopback, RFC 1918 private, CGNAT, link-local including the cloud
  instance-metadata address, unique-local, multicast, reserved, documentation/benchmark blocks). These are facts, not
  deployment opinions, and re-permitting any of them is an SSRF footgun — so there is **no** configuration knob to allow
  them, per the repo's correct-by-default posture ("no knob weakens a security control below its safe baseline").

- **Extensibility is additive-deny only.** Deployments extend the guard by registering `IOutboundAddressFilter`
  implementations (public seam, `TryAddEnumerable`); the guard blocks an address when the floor **or** any filter blocks
  it. A filter can only **further** restrict reachability (for example, block additional corporate CIDRs); it can never
  re-permit an address the floor blocked. Per-tenant **narrowing** also remains available through the existing
  `*_allowed_hosts` allow-list settings.

## Consequences

- A FAPI client can onboard its signing keys by publishing a `jwks_uri` and rotate them without re-registering; its
  `private_key_jwt` and JAR assertions validate against the runtime-resolved keys merged with any stored keys.
- The JAR `request_uri` fetch is now SSRF-guarded and no longer follows redirects, closing a prior confused-deputy gap.
- The private/reserved-address floor is uniform across all three fetches and cannot be weakened by configuration; a
  deployment that needs stricter egress adds `IOutboundAddressFilter` rules.
- `IClientKeyResolver`, `IOutboundAddressFilter`, and the five `jwks_uri*` settings are new public surface.
- Open follow-ups (tracked in [`BACKLOG.md`](../../BACKLOG.md)): the `request_uri` fetch still has no response-size cap,
  and the back-channel logout and CIBA client-notification **deliveries** POST to client-registered URLs through
  unguarded clients — both should route through the shared guard.
