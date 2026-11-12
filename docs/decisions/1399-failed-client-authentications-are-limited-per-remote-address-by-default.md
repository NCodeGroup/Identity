# 1399. Failed client authentications are limited per remote address by default

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md)

## Context

A client authenticates at the token, pushed authorization, introspection, revocation, device authorization and
backchannel authentication endpoints with a credential that a caller can guess online: a `client_secret` an operator
supplied, an assertion signed with it, or a certificate matched against a registration. A public client presents no
credential: its `client_id` is not a secret (RFC 6749 §2.2), so a lookup that fails guesses nothing. The server stores a supplied
secret as a PBKDF2 verifier and bounds the concurrent derivations
([ADR-1391](1391-a-client-secret-is-a-one-way-verifier-unless-its-method-keys-from-its-utf-8-octets.md)), which bounds the processors
a flood of guesses takes, not the number of guesses.

The request rate limits ([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)) do not bound guessing
either:

- `rate_limit_client_per_minute` is judged at the `client_authentication` stage, which a request reaches only once its
  client has authenticated, so a wrong guess never counts against it.
- `rate_limit_address_per_minute` and `rate_limit_tenant_per_minute` count every request, successful ones included, and
  have no default ([ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md)). A value low enough to bound
  guessing, such as ten a minute, refuses the legitimate traffic of a busy client or of every client behind one NAT
  gateway, and a value that admits that traffic, thousands a minute, admits millions of guesses a day.

A limit keyed by the `client_id` alone is a lever: anyone who knows a public `client_id`, such as the bootstrap
administrator's, could keep that client locked out from anywhere, which ADR-1391 rejects. RFC 9700 §4.10 and OWASP's guidance on credential stuffing count failures per source and per source and
account rather than per account alone, and NIST SP 800-63B-4 §3.2.2 limits consecutive failures.

Brute-force protection is a correctness guard: it must hold while its store is unreachable, so it fails closed
([project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md)).

## Decision

**Every handler of a client-authentication method that verifies a credential asks
`IClientAuthenticationAttemptLimiter` before it verifies one, and counts each credential it refuses; `none`, which
verifies none, neither asks nor counts. The default limiter counts failures in the shared `IExpiringCounterSet` per
remote address and per remote address and `client_id`, with limits that hold by default, and refuses a partition at its
limit until its window ends, the way a rate limit refuses.**

1. **The seam.** `IClientAuthenticationAttemptLimiter`, in `NCode.Identity.OpenId.Authentication.Abstractions`
   (namespace `NCode.Identity.OpenId.Authentication.Clients`), has `CheckAsync(OpenIdContext, string? clientId,
   CancellationToken)`, which returns the `RateLimitDecision` of the request rate limiter, and
   `RecordFailureAsync(OpenIdContext, string? clientId, CancellationToken)`. The `client_id` is the unverified one the
   request presents, or null when it presents none that can be read. `DefaultClientAuthenticationAttemptLimiter` is
   registered by `AddClientServices()`.
2. **A handler that verifies a credential asks first and counts every credential it refuses.**
   `CommonClientAuthenticationHandler`, the base of every built-in handler, asks the limiter in `ResolveClientAsync`,
   before the client is looked up, so a refused request of `client_secret_basic`, `client_secret_post`,
   `private_key_jwt` or `client_secret_jwt` costs no lookup; the assertion handlers also ask before they refuse an
   assertion whose `iss` and `sub` name no client. The mutual-TLS handlers ask once the resolved client's registration
   says the request is theirs, because until then a request that carries only a `client_id` is a public client's as
   much as a mutual-TLS client's, so a refusal is audited with the method the client is registered for and costs no
   certificate verification. `FailAsync`, which every refused credential goes through, the identifier of a client that
   cannot be found included, counts the failure first, with `CancellationToken.None` and before the audit, so a caller
   that disconnects after it sends a guess is still counted even when a replaced audit recorder honours the request's
   cancellation. Nothing that verifies no credential is counted: a `none` authentication, since a public client's
   `client_id` is no secret and refusing it would only deny the public clients behind an address (`VerifiesCredential`
   is `false` for its handler, so it neither asks nor counts); a Basic header that is empty, not Base64 or without a
   colon, which carries no secret and is answered `invalid_client` without an audit or a count; a verification that the
   PBKDF2 gate refuses (`503`), because the secret was never compared; a refusal at the limit; and a failure after a
   credential verified, such as a method the client is not registered for that the authentication service refuses,
   which is not a guess.
3. **Two partitions, never the `client_id` alone.** One counter holds the failures of a remote address for every
   `client_id`, limited by `client_authentication_failed_attempts_address_limit`, and one the failures of the remote
   address with one `client_id`, limited by `client_authentication_failed_attempts_limit`. An address is partitioned as
   the request rate limiter partitions it: an IPv6 address by its `/64`, and an IPv4-mapped address as the IPv4 address
   it maps (`RemoteAddressPartition`). A request without a remote address, which a host sees when its proxy does not
   forward the client's address or Kestrel listens on a socket, is not limited, and the limiter logs
   `ClientAuthenticationAttemptLimitSkippedWithoutRemoteAddress` (event 2073) at `Warning` once per process, naming the
   tenant, so the operator learns that the limit is off. A caller at another address
   can never refuse a client's own requests, so the limit is no lockout lever; the address partition bounds one address
   that tries many `client_id`s, and the narrower partition bounds one address that guesses one client's credential.
4. **Fixed windows that hold by default.** The counters count in fixed windows of
   `client_authentication_failed_attempts_window` that start at multiples of its length, as the rate limits' windows do,
   so a refusal's `Retry-After` is exactly the time until the window ends. The three settings apply to tenants, are not
   discoverable, and have defaults:

   | Setting                                               | Rule                  | Default    | Merge |
   | ----------------------------------------------------- | --------------------- | ---------- | ----- |
   | `client_authentication_failed_attempts_limit`         | 1 to 100              | 10         | `Min` |
   | `client_authentication_failed_attempts_address_limit` | 1 to 10,000           | 100        | `Min` |
   | `client_authentication_failed_attempts_window`        | one minute to one day | 15 minutes | `Max` |

   With the defaults, one address tries at most 960 credentials of one client a day and 9,600 across clients. A
   narrower scope may only lower a limit or lengthen the window. The ceiling of 100 on
   `client_authentication_failed_attempts_limit` is NIST SP 800-63B-4 §3.2.2's bound on the consecutive failures
   against one account's secret, as it is on `password_failed_attempts_limit`
   ([ADR-1237](1237-failed-password-attempts-are-limited-per-account-and-a-refusal-costs-what-a-wrong-password-costs.md)); the address limit bounds an aggregate
   across clients, each of which the narrower limit already caps, so its ceiling of 10,000 admits the confidential
   clients behind one large NAT gateway or proxy without raising the guesses one client's credential is exposed to. A
   successful authentication clears nothing: clearing would cost a write on every authentication, and a client that
   holds its credential does not fail.
5. **One read for a clean address, one write per failure.** A failure increments both counters of its window in one
   `IncrementUntilExceededAsync` batch, address first, with each limit as the batch's limit, so the counter of an
   address and a `client_id` never exceeds the address's. A check therefore reads the address's counter and reads the
   narrower counter only when the address has at least as many failures as the narrower limit, so a request from an
   address without failures costs one read. The reads are kept on the request (`ClientAuthenticationAttemptState`), so
   the several handlers that examine one request share them.
6. **A refusal is answered like a rate limit, audited, logged, and not billed.** The handler answers
   `429 Too Many Requests` with `temporarily_unavailable`, a `Retry-After` in whole seconds until the window ends and
   `Cache-Control: no-store` (`RateLimitErrors.RefuseFailedClientAuthentications`), never `invalid_client`, which would
   tell a legitimate client behind the address that its credential is wrong. It audits a `client.authentication` failure
   with the error code `attempt_limit_reached`, the code a password refused at its limit carries, and the limiter logs
   `ClientAuthenticationAttemptLimitReached` (event 2072) at `Information` with the tenant, the partition's name, the
   limit and the window, never the address or the `client_id`. The limiter marks the request refused on its rate-limit
   state, so it is not recorded as the tenant's usage.
7. **The limit fails closed.** A counter store that throws propagates its exception, so client authentication fails
   while the shared state is unreachable instead of admitting unlimited guesses; the request rate limiter, a capacity
   guard, fails open. The limit complements the PBKDF2 gate of
   [ADR-1391](1391-a-client-secret-is-a-one-way-verifier-unless-its-method-keys-from-its-utf-8-octets.md), which bounds the
   processors, not the guesses.

## Options considered

- **Give `rate_limit_address_per_minute` and `rate_limit_tenant_per_minute` default values.** Rejected: they count
  successful requests (Context); they remain capacity limits a tenant sets.
- **Limit failures per `client_id`.** Rejected: anyone who knows a `client_id` could lock the client out from anywhere
  (ADR-1391).
- **Limit failures per remote address only.** Rejected: one limit would have to be high enough for every client behind
  a NAT gateway together and so would let an address guess one client's credential that often; the narrower partition
  bounds that with a low limit.
- **Register the failure limits as `RateLimitDescriptor`s.** Rejected: a descriptor counts every request in one
  increment before the endpoint runs, records usage, and fails open; a failure limit reads before the verification,
  increments after a refusal, records no usage and fails closed.
- **Count the attempts, successful ones included, before the verification.** Rejected: a busy client would spend its
  own allowance and refuse itself.
- **Clear the narrower counter on a successful authentication.** Rejected: a write on every authentication, for a client
  that does not fail.
- **Windows that start at the first failure, as the password limit's do.** Rejected: `Retry-After` could only be an
  upper bound; aligned windows give the exact time.
- **Answer a refusal with `invalid_client`.** Rejected: a legitimate client behind the address would read its
  credential as wrong and stop instead of retrying.
- **Fail open with a breaker, as the rate limits do.** Rejected: an outage of the shared state would lift the limit
  exactly when an attacker can cause one (project-template ADR-0144).
- **Audit only the failure that reaches a limit.** Rejected: an operator investigating a refused client needs each
  refusal.
- **Refuse and count a public client's `none` authentication too.** Rejected: it verifies no credential, so a refusal
  protects nothing, while an attacker behind the carrier-grade NAT or corporate egress of a public client's users could
  send a hundred invented `client_id`s a window and deny every code exchange and refresh of every user behind that
  address; and a failed lookup of a public `client_id` guesses nothing worth bounding. A credential-bearing request from
  the same address is still refused at the limit, which is the address's own exposure.
- **Count a Basic header that carries no credential.** Rejected: nothing is verified, so there is nothing to bound.
- **Ask the limiter before the mutual-TLS lookup.** Rejected: the handler owns the request only once the resolved
  client's registration says so, so an earlier refusal would be audited with a mutual-TLS method for a public client
  that never used one, and would refuse public clients, which the first option rejects.
- **Fail without a remote address.** Rejected: a host that listens on a socket has none by design, and an in-process
  test host has none; a warning tells the operator the limit is off without refusing a legitimate deployment.

## Consequences

- Online guessing of a client's credential is bounded by default, for every method and every endpoint that
  authenticates a client, without a per-`client_id` lockout.
- Every client authentication from an address costs one read of the shared state, and each failure one batched write;
  on Valkey each is a round trip.
- Every confidential client behind one address shares the address's allowance: an attacker behind a client's NAT
  gateway can make that client wait until the window ends. A public client is never refused. A host behind a reverse
  proxy must trust it through the `ForwardedHeaders` section
  ([ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package-outside-development.md)),
  or every caller shares the proxy's address; a host that
  sees no remote address at all is not limited, and its log says so once.
- A failed lookup of a public `client_id` is not bounded; the response tells a caller whether a `client_id` exists and
  is public, which a public `client_id` reveals anyway.
- Concurrent failures can exceed a limit by the number in flight, because the count is read before the verification and
  incremented after it.
- An outage of the shared state fails client authentication.

## References

- [RFC 9700 §4.10](https://www.rfc-editor.org/rfc/rfc9700#section-4.10), client authentication.
- [NIST SP 800-63B-4 §3.2.2](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/), rate limiting.
- [OWASP Credential Stuffing Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Credential_Stuffing_Prevention_Cheat_Sheet.html).
- [ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md),
  [ADR-1237](1237-failed-password-attempts-are-limited-per-account-and-a-refusal-costs-what-a-wrong-password-costs.md),
  [ADR-1447](1447-each-host-composes-forwarded-header-trust-and-strict-transport-security-from-one-general-package-outside-development.md),
  [ADR-1391](1391-a-client-secret-is-a-one-way-verifier-unless-its-method-keys-from-its-utf-8-octets.md).
- Code: `NCode.Identity.OpenId.Authentication.Clients.IClientAuthenticationAttemptLimiter`,
  `NCode.Identity.OpenId.Authentication.Clients.DefaultClientAuthenticationAttemptLimiter`,
  `NCode.Identity.OpenId.Authentication.Clients.DefaultClientAuthenticationAttemptSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.Clients.Handlers.CommonClientAuthenticationHandler`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.RateLimitErrors`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.RemoteAddressPartition`.
