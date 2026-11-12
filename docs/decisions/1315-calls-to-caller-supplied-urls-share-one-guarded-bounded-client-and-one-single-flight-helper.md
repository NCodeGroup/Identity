# 1315. Calls to caller-supplied URLs share one guarded, bounded client and one single-flight helper

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
- **Supersedes:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md), decisions 3 and 6

## Context

The server fetches from, and delivers to, URLs that a client or a certificate supplies: the `sector_identifier_uri`,
`jwks_uri` and `request_uri` documents, the back-channel logout and CIBA notifications, and the CRL and OCSP locations
of a mutual-TLS client certificate.
[project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
requires every such call to be guarded against forged destinations, bounded by a per-attempt and a total timeout and by
a limited, jittered retry, and shared between concurrent callers of one URL (decision 4) with a failed fetch remembered
for a short time (decision 5).

Several forces shape the design:

- A copy of the guard configuration per purpose drifts from the others, and a copy of the single-flight bookkeeping per
  feature misses a fix made to another copy
  ([#482](https://github.com/NCodeGroup/identity/issues/482)).
- A call to a client-controlled URL is a server-side request forgery (SSRF) vector: a client that registers an internal
  URL could turn the server into a confused deputy that probes internal services or follows a redirect to an internal
  host.
- A response body from an endpoint the server does not control is a resource-exhaustion vector if it is read without a
  cap, and the pipeline's timeouts end when the headers arrive.
- The `jwks_uri` fetch runs per token request, so a retry of a failure that is deterministic only adds load.

## Decision

One guarded client serves every call, a pipeline bounds each call, one reader caps each body, and one helper shares each
fetch between callers.

### One guarded client

`GuardedHttpClient.Name` (`NCode.Identity.OpenId.GuardedHttp`), registered by `AddOutboundHttpServices`, serves the
`sector_identifier_uri`, `jwks_uri` and `request_uri` fetches, the back-channel logout and CIBA deliveries, and the CRL
and OCSP retrievals. Its `SocketsHttpHandler` has `AllowAutoRedirect = false`, so a redirect cannot bounce a call to an
internal host.

`IOutboundHttpForgeryGuard` (default `DefaultOutboundHttpForgeryGuard`) is the handler's `ConnectCallback`. It resolves
the host once, skips every address in the IANA special-use floor (loopback, RFC 1918, CGNAT, link-local including the
cloud metadata address, unique-local, multicast, reserved and documentation ranges; IPv4-mapped addresses included) and
connects to the address it vetted, which defeats DNS rebinding.

No setting turns the floor off. `IOutboundAddressFilter` (public, registered with `TryAddEnumerable`) only adds denials
and never re-permits an address, and the `*_allowed_hosts` settings narrow the reachable hosts further.

### A pipeline bounds each call

`GuardedResilienceHandler` runs `GuardedResiliencePipelines` on `Polly.Core`
([ADR-1385](1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md)):
a total timeout, then a jittered exponential retry, then a per-attempt timeout. `OutboundHttpOptions`
(`OpenId:OutboundHttp`) supplies the values, validated when the host starts:

| Option                 | Default |
| ---------------------- | ------- |
| `AttemptTimeout`       | 10 s    |
| `TotalTimeout`         | 30 s    |
| `MaxRetryAttempts`     | 2       |
| `RetryDelay`           | 1 s     |
| `FailureCacheDuration` | 1 min   |

The client's `HttpClient.Timeout` is `Timeout.InfiniteTimeSpan`, so these options are the only bound on a call; the
framework default of 100 seconds would silently cut a longer `TotalTimeout`.

Only a GET or HEAD without a body is retried, on a 408, a 429, a 5xx or a timeout. The retry ignores `Retry-After`
because the endpoint chooses that delay, never retries a connection or name-resolution error because the guard's refusal
is deterministic, and never sends a POST twice. A timeout surfaces as a `TaskCanceledException` with a
`TimeoutException` inner exception, the shape `HttpClient.Timeout` produces.

### A reader caps each body

Callers send with `HttpCompletionOption.ResponseHeadersRead`, so the timeouts end when the headers arrive.
`IOutboundHttpContentReader` (public, registered with `TryAddSingleton`, replaceable) is the one implementation of
reading a response body under a hard byte cap. It enforces the cap while streaming into a caller-supplied, pooled
`IBufferWriter<byte>`, treats `Content-Length` only as a hint so a missing or lying length cannot bypass the cap, and
returns an `OutboundHttpReadResult` (`Completed` or `Exceeded`). Each call site parses straight from the written
sequence, so the read allocates no intermediate array.

### The document fetches

The `sector_identifier_uri`, `jwks_uri` and `request_uri` fetches require an absolute `https` URI and share three
non-discoverable settings:

| Setting            | Default | Merge       |
| ------------------ | ------- | ----------- |
| `*_allowed_hosts`  | none    | `Intersect` |
| `*_fetch_timeout`  | 10 s    | `Min`       |
| `*_max_size_bytes` | 100 KB  | `Min`       |

The `jwks_uri_*` and `request_uri_*` families apply to clients and `sector_identifier_uri_*` to tenants. They cascade
from `OpenId:Platform:Settings`, so a narrower scope can only lower them.

A `request_uri` that is not `https`, fails the allow-list, exceeds the cap or times out is rejected as
`invalid_request_uri`. A specific failure (status, content type, size) is reported as itself rather than as a generic
fetch error, and the caller's cancellation propagates unchanged.

### Deliveries and revocation data

The back-channel logout and CIBA deliveries never read the response body, so they have no size or timeout settings; the
guard's connection and the disabled redirect are their controls. They run as durable dispatch jobs whose retry wraps the
client's, and a 3xx fails `EnsureSuccessStatusCode`.

Revocation data is fetched over `http` too, under the same address floor: RFC 5280 section 4.2.1.13 and RFC 6960
Appendix A publish the CRL and OCSP locations as `http` URLs, and the integrity of the data comes from the CA's
signature, not the transport.

### One single-flight helper

`SingleFlight<TResult>` shares a fetch between concurrent callers. It is not a dependency-injection service, and each
owner creates its own instance so the keys of two features never share a dictionary. It keeps one
`Lazy<Task<TResult>>` per key in a `ConcurrentDictionary`. The retrieval runs without any caller's token, each caller
stops waiting with its own token (`WaitAsync`), and the entry leaves the dictionary after the retrieval recorded its
result. `RememberFailure(key)` stores `{key}:unavailable` in `IMemoryCache` for the duration the owner gives,
`IsFailureRemembered(key)` reads it, and `FailureCacheDuration` returns that duration so the owner's log reports it.

Each owner (`DefaultClientKeyResolver`, `RevocationRetrievalCache`) keeps what is its own: what counts as a failure, the
value cache with its key and lifetime, its log entries and its options. It checks its caches before it calls the helper
and again inside the retrieval. Any further remote fetch that several callers share uses the helper.

### Overrides of project-template ADR-0145

- **Decision 6.** The document-fetch limits stay per-scope settings that a narrower scope can only lower; the client's
  options bound every call host-wide regardless.
- **Decision 3.** Revocation data is fetched over `http`, where the RFCs above publish it.

## Options considered

- **One named client per purpose.** Rejected: each copy of the guard configuration drifts from the others.
- **`AddStandardResilienceHandler`.** Rejected: its circuit breaker and rate limiter are shared across every
  caller-supplied host, so one hostile endpoint would stop every other client's calls, and a pipeline per authority
  grows with every attacker-chosen host.
- **Converting the fetch settings to options.** Rejected: they are the family's per-scope controls, and lowering them is
  safe.
- **Retrying connection failures.** Rejected: the guard's refusal is deterministic, and the `jwks_uri` fetch runs per
  token request.
- **A toggleable address floor.** Rejected: a setting that permits the private network defeats the guard for every
  caller.
- **A helper that also caches the value, or a singleton service.** Rejected: the owners cache under different keys and
  lifetimes, and a singleton would mix the keys of different features in one dictionary.
- **One client, one pipeline, one reader and one helper, as decided.** Chosen: the guard, the bounds and the sharing
  have one implementation, so a fix reaches every caller.

## Consequences

- A hostile endpoint costs at most the total timeout to answer with its headers, and a redirect or a rebinding name
  cannot reach the private network.
- The deliveries buffer no response body; the capped reader and the per-scope fetch timeouts bound a fetch's body, which
  the pipeline's timeouts do not cover once the headers have arrived.
- The `jwks_uri` fetch is single-flight and remembers a failed fetch for `FailureCacheDuration`, as the template's
  decisions 4 and 5 ask
  ([ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md)).
- The retry of a revocation-list `GET` runs within `FetchTimeout`, and the remembered failure stays the back-off between
  retrievals ([ADR-1400](1400-client-certificate-revocation-is-checked-by-configured-sources-under-the-clients-revocation-mode.md)).
- A fix to the single-flight mechanism reaches every owner, and the document fetches share one settings shape.

## References

- [ADR-1093](1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md),
  [ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md),
  [ADR-1400](1400-client-certificate-revocation-is-checked-by-configured-sources-under-the-clients-revocation-mode.md),
  [ADR-1385](1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md),
  [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)
- Issues [#67](https://github.com/NCodeGroup/identity/issues/67) and
  [#482](https://github.com/NCodeGroup/identity/issues/482)
- `GuardedHttpClient.cs`, `GuardedResilienceHandler.cs`, `GuardedResiliencePipelines.cs`, `OutboundHttpOptions.cs`,
  `OutboundHttp/DefaultRegistration.cs`, `SingleFlight.cs`, `RevocationRetrievalCache.cs` and
  `DefaultClientKeyResolver.cs` in `NCode.Identity.OpenId.Authentication`
