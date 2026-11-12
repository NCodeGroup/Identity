# 1336. The protocol client bounds every call with a Polly.Core pipeline and retries only safe requests

- **Status:** Accepted
- **Date:** 2026-11-01
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)

## Context

The OpenID protocol client (`NCode.Identity.OpenId.Client`) sends discovery, JWKS, token, PAR, revocation and userinfo
requests to an operator-configured OpenID Provider. A request bounded by one overall timeout, with no per-attempt
timeout and no retry, fails the call the caller is waiting for while the provider restarts, and a provider that accepts
the connection and never answers holds it for the whole timeout.
[project-template ADR-0145](project-template/0145-outbound-calls-are-bounded-and-caller-supplied-urls-are-guarded.md)
requires every outbound call to carry a total timeout, a per-attempt timeout and a bounded, jittered retry.

No shipping package carries `Microsoft.Extensions.Http.Resilience`
([ADR-1385](1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md)),
so the protocol client builds the same strategies from `Polly.Core` alone
([#469](https://github.com/NCodeGroup/identity/issues/469), [#471](https://github.com/NCodeGroup/identity/issues/471)).

## Decision

The protocol client bounds every call with a `Polly.Core` pipeline, and retries only a request that is safe to send
twice.

1. **A handler bounds every client call.** `ProviderResilienceHandler` serves the named client
   `OpenIdClientDefaults.HttpClientName` and every mutual-TLS client
   (`OpenIdClientDefaults.MutualTlsHttpClientNamePrefix…`). The `HttpClient.Timeout` of those clients is
   `Timeout.InfiniteTimeSpan`, so the options are the only bound; the default of 100 seconds would silently cut a longer
   `TotalTimeout`.

2. **Four options, validated when the host starts.** `OpenIdClientOptions.TotalTimeout` (15 seconds) bounds the whole
   request, the read of the response body included; `AttemptTimeout` (10 seconds), `MaxRetryAttempts` (2) and
   `RetryDelay` (1 second) bound its attempts. The timeouts are between 10 milliseconds and 5 minutes, `AttemptTimeout`
   is not greater than `TotalTimeout`, the retries are between 0 and 10, and the delay is between 0 and 5 minutes.
   These bounds lie inside the range every timer accepts (`NCode.Identity.Time.TimerPeriod`,
   [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)), so a value the
   validator accepts is always a valid timer period; the validator checks the tighter bounds directly, and the client
   package does not take a dependency on `NCode.Identity.Time` for a check they already imply. The names match
   `OutboundHttpOptions`, so the family has one vocabulary. The default `TotalTimeout` is 15 seconds, so the BFF's
   40-second refresh lease covers two attempts of 15 seconds and 10 seconds
   ([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)).

3. **Only a safe request is retried.** A GET or HEAD without a body and without a `DPoP` header, while
   `MaxRetryAttempts` is above zero, runs through the total timeout, then the retry, then the per-attempt timeout. A
   request with a body is never resent, so a retry never replays content that an attempt has consumed. A DPoP proof's
   `jti` is single-use (RFC 9449 §11.1), so a request that carries one is never resent. The retry handles any
   `HttpRequestException`, a `TimeoutRejectedException` from the attempt timeout, and the statuses 408, 429 and 5xx. A
   refused connection counts as transient, because the provider is operator-configured and refuses connections while it
   restarts. The backoff is exponential with jitter. `Retry-After` is ignored; the total timeout bounds the call. The
   retry disposes each response it discards.

4. **Any other request gets the total timeout only.** A token, PAR or revocation request keeps its full total timeout
   and the pipeline never resends it, because its body may be consumed and a DPoP proof's `jti` is single-use. Only a
   `use_dpop_nonce` answer (RFC 9449 §8) sends it once more, with a new proof and the nonce the provider supplied.

5. **A timeout looks like `HttpClient.Timeout`.** A pipeline timeout surfaces as a `TaskCanceledException` with a
   `TimeoutException` inner exception, which `ProviderHttpClient` turns into `OpenIdProviderException`; a cancellation
   by the caller is never reported as a timeout.

6. **The package is `Polly.Core`, referenced directly.** It is pinned in `Directory.Packages.props` and has no
   dependencies. The client runs on `Polly.Core` alone because no shipping package carries
   `Microsoft.Extensions.Http.Resilience`
   ([ADR-1385](1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md)).

7. **Metrics.** `ProviderMetrics` records the counters `ncode.identity.openid_client.retries` and
   `ncode.identity.openid_client.timeouts` on the `NCode.Identity.OpenId.Client` meter, created through
   `IMeterFactory`.

## Options considered

- **`AddResilienceHandler` or `AddStandardResilienceHandler`.** Rejected: they bring Telemetry, and the standard
  handler's circuit breaker and rate limiter add behavior this client does not need.
- **A hand-written retry loop.** Rejected: Polly's strategies are tested, and the template warns against a hand-written
  loop.
- **Retrying POSTs.** Rejected: no OAuth endpoint honors an idempotency key, and an authorization code or a refresh
  token must not be sent twice.
- **Honoring `Retry-After`.** Rejected: the endpoint chooses that delay, and the total timeout is the bound.
- **A `Polly.Core` pipeline that retries only safe requests.** Chosen: it gives the template's timeout, retry and
  per-attempt timeout with no extra dependency.

## Consequences

- A host that sets a `TotalTimeout` below the default `AttemptTimeout` of 10 seconds also sets `AttemptTimeout` no
  higher, or the host does not start.
- Discovery, JWKS and bearer userinfo GETs survive a transient provider failure; token requests still fail on the first
  error.
- The BFF's lease check reads `TotalTimeout`
  ([ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md)).
- `NCode.Identity.OpenId.Client` carries `Polly.Core` and does not carry `Microsoft.Extensions.Telemetry.Abstractions`.

## References

- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md),
  [ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md),
  [ADR-1291](1291-the-bff-refreshes-a-sessions-token-single-flight-under-a-lease-its-winner-releases-by-owner-token.md),
  [ADR-1315](1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md),
  [ADR-1385](1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md)
- Issues [#469](https://github.com/NCodeGroup/identity/issues/469) and [#471](https://github.com/NCodeGroup/identity/issues/471)
- `ProviderResilienceHandler.cs`, `ProviderResiliencePipelines.cs` and `OpenIdClientOptions.cs` in
  `NCode.Identity.OpenId.Client`
