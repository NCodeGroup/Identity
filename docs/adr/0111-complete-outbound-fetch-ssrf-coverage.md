# 111. Complete outbound-fetch SSRF coverage and bound the `request_uri` fetch

- **Status:** Accepted
- **Date:** 2026-10-15
- **Deciders:** NCode Group

## Context

[ADR-0106](0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md) consolidated the server-side request
forgery (SSRF) defenses for the OP's outbound fetches of **client-supplied URLs** behind one shared, injectable
`IOutboundHttpForgeryGuard` and gave the `jwks_uri` and `sector_identifier_uri` fetches a uniform bound set — an
`https`-only scheme check, an optional host allow-list, a wall-clock fetch timeout, and a streamed response-size cap.
It left two gaps, tracked as issue [#67](https://github.com/NCodeGroup/Identity/issues/67):

1. **The JAR `request_uri` fetch** (RFC 9101, `DefaultLoadAuthorizationRequestHandler`) was already routed through the
   shared guard (socket pinned away from private addresses, no auto-redirect), but it read the response body with
   `ReadAsStringAsync` under **no size cap** — an oversized-response resource-exhaustion (DoS) vector — and had no
   fetch-timeout or host allow-list. It also did not require `https`.

2. **The two server→client POST deliveries** that target client-registered URLs — back-channel logout
   (`BackChannelLogoutDeliveryJobHandler`, `backchannel_logout_uri`) and CIBA ping/push notification
   (`CibaDeliveryJobHandler`, `client_notification_endpoint`) — used the **default unnamed** `HttpClient`, which
   follows redirects and applies **no** SSRF control. A client that registers an internal URL could turn the OP into a
   confused deputy (probe internal services, or follow a redirect to an internal host).

A third, smaller smell: the streamed size-cap reader (`ReadCappedAsync`) was **duplicated** verbatim in the `jwks_uri`
and `sector_identifier_uri` consumers, and the `request_uri` fix would have triplicated it.

## Decision

**Give the `request_uri` fetch the same bound set as the other client-URL fetches, route the two POST deliveries
through SSRF-guarded named clients, and extract the capped reader into one shared helper.**

### Bound the JAR `request_uri` fetch

- A new `request_uri_*` settings family mirrors `jwks_uri_*` / `sector_identifier_uri_*`: `request_uri_allowed_hosts`
  (optional SSRF host allow-list, ceiling/`Intersect`), `request_uri_fetch_timeout` (default 10s, ceiling/`Min`), and
  `request_uri_max_size_bytes` (default 100 KB, ceiling/`Min`). All three are non-discoverable per-client ceilings.
- The fetch now requires an absolute `https` URI, enforces the optional host allow-list (the always-on
  private/loopback/link-local floor in `DefaultOutboundHttpForgeryGuard` still applies regardless), bounds the request with a
  linked timeout `CancellationTokenSource`, and reads the body through the shared capped reader. A response that
  exceeds the cap, uses a non-`https` scheme, or fails the allow-list is rejected as `invalid_request_uri`.
- A specific validation failure (status code, content type, or size) is surfaced as-is rather than masked as the
  generic "failed to fetch" error, and genuine caller cancellation propagates unchanged (only the fetch timeout is
  translated to a fetch error).

### Guard the server→client POST deliveries

- Back-channel logout and CIBA delivery each get a dedicated **SSRF-guarded named** `HttpClient`
  (`NCode.Identity.OpenId.BackChannelLogout`, `NCode.Identity.OpenId.CibaDelivery`) wired exactly like the fetch
  clients: the guard as `ConnectCallback` (socket pinned to a vetted public address) and `AllowAutoRedirect = false`
  (a redirect cannot bounce the delivery to an internal host). Both deliveries already run as durable, retryable
  dispatch jobs; a 3xx that is no longer auto-followed simply fails `EnsureSuccessStatusCode` and the dispatcher
  retries.
- The deliveries deliberately get **no** size cap or `*_max_size_bytes` / `*_fetch_timeout` settings family: their
  response body is discarded (only the status code matters), so the SSRF-relevant controls are the pinned connection
  and the disabled redirect, not a body bound. Keeping them settings-free avoids speculative public surface.

### Extract the shared capped reader as a replaceable, buffer-aware seam

- `IOutboundHttpContentReader` (public, in `…Authentication.OutboundHttp`, `TryAddSingleton`-registered and
  DI-overridable) is the single implementation of the "read the response body under a hard byte cap" primitive,
  consumed by the `jwks_uri`, `sector_identifier_uri`, and `request_uri` fetches. It folds in the early
  `Content-Length` reject (used only as a hint) and enforces the cap while streaming, so an unset or lying length
  cannot bypass it.
- The reader writes into a **caller-supplied `IBufferWriter<byte>`** and returns an `OutboundHttpReadResult`
  (`Completed` / `Exceeded`) rather than allocating and returning a `byte[]`. Each call site passes a **pooled**
  `NCode.Buffers` writer and parses directly from the resulting `ReadOnlySequence<byte>` (zero-copy `JsonDocument.Parse`
  for the JWKS / sector documents, a contiguous-span lease for the `request_uri` JWT), so the capped read no longer
  allocates an intermediate array and the buffers return to the pool.

## Consequences

- Every server-initiated outbound HTTP call to a client-controlled URL — the three GET fetches and the two POST
  deliveries — is now SSRF-guarded (pinned connection + no auto-redirect), and the three body-reading fetches share one
  capped reader and one `*_allowed_hosts` / `*_fetch_timeout` / `*_max_size_bytes` settings shape.
- `request_uri` must now be an absolute `https` URI; a client that registered an `http` request object URL must
  re-register with `https`. This is the intended secure-by-default posture (greenfield, pre-release).
- Three new `request_uri_*` settings and the `IOutboundHttpContentReader` / `OutboundHttpReadResult` seam are new
  public surface; the two delivery clients add no public surface.
- The private/reserved-address floor and `IOutboundAddressFilter` additive-deny extensibility from
  [ADR-0106](0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md) apply uniformly to all five calls.
