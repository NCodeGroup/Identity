# 1315. Outbound calls to caller-supplied URLs share one guarded, bounded client

- **Status:** Accepted (amended by
  [ADR-1335](1335-a-clients-jwks-uri-is-fetched-single-flight-and-a-failed-fetch-is-remembered.md): the `jwks_uri`
  fetch is single-flight and a failed fetch is remembered)
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
- **Supersedes:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md), decision 6 for the per-scope fetch limits and decision 3's https-only rule for revocation data

## Context

The server fetches from, and delivers to, URLs that a client or a certificate supplies: the `sector_identifier_uri`,
`jwks_uri` and `request_uri` documents, the back-channel logout and CIBA notifications, and the CRL and OCSP locations
of a mutual-TLS client certificate.
[project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
requires every such call to be guarded against forged destinations and bounded by a per-attempt and a total timeout and
by a limited, jittered retry.

Six named clients, one per purpose, each carry their own copy of the guard configuration, so the copies can drift. None
has a per-attempt or a total timeout or a bounded retry, and the two deliveries buffer the response body of an endpoint
the server does not control.

## Decision

1. **One client serves every call.** `GuardedHttpClient.Name`, registered by `AddOutboundHttpServices`, serves the
   `sector_identifier_uri`, `jwks_uri` and `request_uri` fetches, the back-channel logout and CIBA deliveries, and the
   revocation fetches. It is a `SocketsHttpHandler` whose `ConnectCallback` is the forgery guard
   ([ADR-1111](1111-complete-outbound-fetch-ssrf-coverage.md)) and which never follows a redirect.

2. **A pipeline bounds each call.** The pipeline is a total timeout, then a retry, then a per-attempt timeout, read from
   `OutboundHttpOptions` (`OpenId:OutboundHttp`: `AttemptTimeout` 10 s, `TotalTimeout` 30 s, `MaxRetryAttempts` 2,
   `RetryDelay` 1 s), which are validated when the host starts. The client's `HttpClient.Timeout` is
   `Timeout.InfiniteTimeSpan`, so these options are the only bound on a call; the default of 100 seconds would
   silently cut a longer `TotalTimeout`. The timeouts end when the response headers arrive,
   because the callers send with `HttpCompletionOption.ResponseHeadersRead`. The retry is exponential with jitter,
   ignores `Retry-After` because the endpoint chooses that delay, and never retries an unsafe method or a connection or
   name-resolution error. A timeout surfaces as a `TaskCanceledException` with a `TimeoutException` inner exception,
   the shape `HttpClient.Timeout` produces.

3. **A delivery reads only the status line and the headers.** The deliveries send with
   `HttpCompletionOption.ResponseHeadersRead` and never read the response body, so a hostile endpoint cannot make the
   server buffer it. The dispatcher's retry wraps the client's.

4. **Override: the fetch limits stay per-scope settings.** The `sector_identifier_uri_*`, `jwks_uri_*` and
   `request_uri_*` fetch timeouts, size caps and the `jwks_uri` cache duration stay settings of the server, the tenant
   and the client. The server ceiling comes from `OpenId:Server:Settings`, the scopes merge by `Min`, so a tenant or a
   client can only lower a limit, and the client's options bound every call host-wide regardless.

5. **Override: revocation data is fetched over `http`.** RFC 5280 §4.2.1.13 and RFC 6960 Appendix A publish the CRL
   and OCSP locations as `http` URLs, and the integrity of the data comes from the CA's signature, not the transport.
   The guard's address floor still applies.

## Options considered

- **One named client per purpose.** Rejected: each copy of the guard configuration drifts from the others.
- **`AddStandardResilienceHandler`.** Rejected: its circuit breaker and rate limiter are shared across every
  caller-supplied host, so one hostile endpoint would stop every other client's calls, and a pipeline per authority
  grows with every attacker-chosen host.
- **Converting the fetch settings to options.** Rejected: they are the family's per-scope controls, and lowering them is
  safe.
- **Retrying connection failures.** Rejected: the guard's refusal is deterministic, and the `jwks_uri` fetch runs per
  token request.

## Consequences

- A configuration of the former per-purpose client names has no effect; the one name configures them all.
- A hostile endpoint costs at most the total timeout to answer with its headers, and the deliveries buffer no response
  body; the capped reader and the per-scope fetch timeouts bound a fetch's body, which the pipeline's timeouts do not
  cover once the headers have arrived.
- The `jwks_uri` fetch is single-flight and remembers a failed fetch for `FailureCacheDuration`, as the template's
  decisions 4 and 5 ask
  ([ADR-1335](1335-a-clients-jwks-uri-is-fetched-single-flight-and-a-failed-fetch-is-remembered.md)).
- The retry of a revocation-list `GET` runs within `FetchTimeout`, and the remembered failure stays the back-off between
  retrievals ([ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)).

## References

- [ADR-1095](1095-sector-identifier-uri-fetch-and-ssrf-guard.md),
  [ADR-1106](1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md),
  [ADR-1111](1111-complete-outbound-fetch-ssrf-coverage.md),
  [ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)
- `GuardedHttpClient.cs`, `GuardedHttpTimeoutHandler.cs`, `OutboundHttpOptions.cs` and `OutboundHttp/DefaultRegistration.cs`
  in `NCode.Identity.OpenId.Authentication`
