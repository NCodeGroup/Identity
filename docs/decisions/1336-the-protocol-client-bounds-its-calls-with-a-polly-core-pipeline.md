# 1336. The protocol client bounds its calls with a Polly.Core pipeline

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
- **Supersedes:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md), decision 1's `Microsoft.Extensions.Http.Resilience` for `NCode.Identity.OpenId.Client`

## Context

The OpenID protocol client (`NCode.Identity.OpenId.Client`) sends discovery, JWKS, token, PAR, revocation and userinfo
requests to an operator-configured OpenID Provider. It bounded each request with one 15-second `RequestTimeout` and had
no per-attempt timeout and no retry, so a provider that is restarting fails the request the caller is waiting for, and a
provider that accepts the connection and never answers holds it for the whole timeout.
[project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
requires every outbound call to carry a total timeout, a per-attempt timeout and a bounded, jittered retry, and names
`Microsoft.Extensions.Http.Resilience` for it.

That package depends on `Microsoft.Extensions.Telemetry.Abstractions`, which switches the `[LoggerMessage]` source
generator of every project that references it, so a library that adds it changes the logging code of its consumers
([#471](https://github.com/NCodeGroup/Identity/issues/471)).

## Decision

1. **A handler bounds every client call.** `ProviderResilienceHandler` serves the named client
   `OpenIdClientDefaults.HttpClientName` and every mutual-TLS client
   (`OpenIdClientDefaults.MutualTlsHttpClientNamePrefix…`). The `HttpClient.Timeout` of those clients is
   `Timeout.InfiniteTimeSpan`, so the options are the only bound; the default of 100 seconds would silently cut a longer
   `TotalTimeout`.

2. **Four options, validated when the host starts.** `OpenIdClientOptions.TotalTimeout` (15 seconds) replaces
   `RequestTimeout` and also bounds the read of the response body; `AttemptTimeout` (10 seconds), `MaxRetryAttempts` (2)
   and `RetryDelay` (1 second) join it. The timeouts are between 10 milliseconds and 5 minutes, `AttemptTimeout` is not
   greater than `TotalTimeout`, the retries are between 0 and 10, and the delay is between 0 and 5 minutes. The names
   match `OutboundHttpOptions`, so the family has one vocabulary. The default `TotalTimeout` stays 15 seconds, so the
   BFF's default 40-second refresh lease still covers two attempts of 15 seconds and 10 seconds
   ([ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)).

3. **Only a safe request is retried.** A GET or HEAD without a `DPoP` header, while `MaxRetryAttempts` is above zero,
   runs through the total timeout, then the retry, then the per-attempt timeout. A DPoP proof's `jti` is single-use
   (RFC 9449 §11.1), so a request that carries one is never resent. The retry handles any `HttpRequestException`, a
   `TimeoutRejectedException` from the attempt timeout, and the statuses 408, 429 and 5xx. A refused connection counts
   as transient here, unlike the server's guarded client, because the provider is operator-configured and refuses
   connections while it restarts, where the guard's refusal is deterministic. The backoff is exponential with jitter.
   `Retry-After` is not honored; the total timeout bounds the call. The retry disposes each response it discards.

4. **Any other request gets the total timeout only.** A token, PAR or revocation POST keeps its full total timeout and
   is never sent twice.

5. **A timeout looks like `HttpClient.Timeout`.** A pipeline timeout surfaces as a `TaskCanceledException` with a
   `TimeoutException` inner exception, which `ProviderHttpClient` already turns into `OpenIdProviderException`; a
   cancellation by the caller is not reported as a timeout.

6. **The package is `Polly.Core` 8.9.0, referenced directly.** It has no dependencies on net10.0. `Polly.Extensions`
   and `Polly.RateLimiting`, which `Microsoft.Extensions.Http.Resilience` brings to the server, are
   transitive-pinned to the same version so the Polly packages stay in lockstep. The pipeline uses the same strategies
   that the template names (timeout, retry, timeout), so the intent of decision 1 holds.

## Options considered

- **`AddResilienceHandler` or `AddStandardResilienceHandler`.** Rejected: the Telemetry dependency described above, and
  the standard handler's circuit breaker and rate limiter add behavior this client does not need.
- **A hand-written retry loop.** Rejected: ADR-0145 warns against it, and Polly's strategies are tested.
- **Retrying POSTs.** Rejected: no OAuth endpoint honors an idempotency key, and an authorization code or a refresh token
  must not be sent twice.
- **Honoring `Retry-After`.** Rejected: the endpoint chooses that delay, and the total timeout is the bound.

## Consequences

- The rename of `RequestTimeout` to `TotalTimeout` is **breaking** for a host that configures the former.
- Discovery, JWKS and bearer userinfo GETs survive a transient provider failure; token requests still fail on the first
  error.
- The BFF's lease check reads `TotalTimeout`
  ([ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md)).
- `NCode.Identity.OpenId.Client` carries `Polly.Core` and still does not carry `Microsoft.Extensions.Telemetry.Abstractions`.

## References

- [ADR-1270](1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md),
  [ADR-1291](1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
  [ADR-1315](1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md)
- Issues [#469](https://github.com/NCodeGroup/Identity/issues/469) and [#471](https://github.com/NCodeGroup/Identity/issues/471)
- `ProviderResilienceHandler.cs`, `ProviderResiliencePipelines.cs` and `OpenIdClientOptions.cs` in
  `NCode.Identity.OpenId.Client`
