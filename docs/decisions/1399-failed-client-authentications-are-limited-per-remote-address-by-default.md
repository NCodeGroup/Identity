# 1399. Failed client authentications are limited per remote address by default

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md)
- **Amends:** [ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)

## Context

A client authenticates at the token, pushed authorization, introspection, revocation, device authorization and
backchannel authentication endpoints with a credential that a caller can guess online: a `client_secret` an operator
supplied, an assertion signed with it, or simply a `client_id` that the server looks up. The server stores a supplied
secret as a PBKDF2 verifier and bounds the concurrent derivations
([ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)), which bounds the processors
a flood of guesses takes, not the number of guesses.

The request rate limits ([ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md),
[ADR-1246](1246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md)) do not bound guessing
either:

- `rate_limit_client_per_minute` is judged at the `client_authentication` stage, which a request reaches only once its
  client has authenticated, so a wrong guess never counts against it.
- `rate_limit_address_per_minute` and `rate_limit_tenant_per_minute` count every request, successful ones included, and
  have no default ([ADR-1010](1010-supported-settings-unset-means-unrestricted.md)). A value low enough to bound
  guessing, such as ten a minute, refuses the legitimate traffic of a busy client or of every client behind one NAT
  gateway, and a value that admits that traffic, thousands a minute, admits millions of guesses a day.

A limit keyed by the `client_id` alone is a lever: anyone who knows a public `client_id`, such as the bootstrap
administrator's, could keep that client locked out from anywhere, which ADR-1391 rejects as the bound on the work
factor. RFC 9700 §4.10 and OWASP's guidance on credential stuffing count failures per source and per source and
account rather than per account alone, and NIST SP 800-63B-4 §3.2.2 limits consecutive failures.

Brute-force protection is a correctness guard: it must hold while its store is unreachable, so it fails closed
([project-template ADR-0144](project-template/0144-correctness-guards-fail-closed-capacity-guards-fail-open.md)).

## Decision

**Every client-authentication handler asks `IClientAuthenticationAttemptLimiter` before it resolves a client or
verifies a credential, and counts each credential or identifier it refuses. The default limiter counts failures in the
shared `IExpiringCounterSet` per remote address and per remote address and `client_id`, with limits that hold by
default, and refuses a partition at its limit until its window ends, the way a rate limit refuses.**

1. **The seam.** `IClientAuthenticationAttemptLimiter`, in `NCode.Identity.OpenId.Authentication.Abstractions`
   (namespace `NCode.Identity.OpenId.Authentication.Clients`), has `CheckAsync(OpenIdContext, string? clientId,
   CancellationToken)`, which returns the `RateLimitDecision` of the request rate limiter, and
   `RecordFailureAsync(OpenIdContext, string? clientId, CancellationToken)`. The `client_id` is the unverified one the
   request presents, or null when it presents none that can be read. `DefaultClientAuthenticationAttemptLimiter` is
   registered by `AddClientServices()`.
2. **Every handler asks first and counts every refusal.** `CommonClientAuthenticationHandler`, the base of every
   built-in handler, asks the limiter in `ResolveClientAsync`, before the client is looked up; the mutual-TLS handlers
   ask before their own lookup, and the assertion handlers ask before they refuse an assertion whose `iss` and `sub`
   name no client. `FailAsync`, which every `invalid_client` a handler answers goes through, records the failure with
   `CancellationToken.None`, so a caller that disconnects after it sends a guess is still counted. A verification that
   the PBKDF2 gate refuses (`503`) is not counted, because the secret was never compared; a refusal at the limit is not
   counted either, because it verifies nothing. A failure after a credential verified, such as a method the client is not
   registered for that the authentication service refuses, is not a guess and is not counted.
3. **Two partitions, never the `client_id` alone.** One counter holds the failures of a remote address for every
   `client_id`, limited by `client_authentication_failed_attempts_address_limit`, and one the failures of the remote
   address with one `client_id`, limited by `client_authentication_failed_attempts_limit`. An address is partitioned as
   the request rate limiter partitions it: an IPv6 address by its `/64`, and an IPv4-mapped address as the IPv4 address
   it maps (`RemoteAddressPartition`). A request without a remote address is not limited. A caller at another address
   can never refuse a client's own requests, so the limit is no lockout lever; the address partition bounds one address
   that tries many `client_id`s, and the narrower partition bounds one address that guesses one client's credential.
4. **Fixed windows that hold by default.** The counters count in fixed windows of
   `client_authentication_failed_attempts_window` that start at multiples of its length, as the rate limits' windows do,
   so a refusal's `Retry-After` is exactly the time until the window ends. The three settings apply to tenants, are not
   discoverable, and have defaults:

   | Setting                                              | Rule                 | Default    | Merge |
   |------------------------------------------------------|----------------------|------------|-------|
   | `client_authentication_failed_attempts_limit`         | 1 to 100             | 10         | `Min` |
   | `client_authentication_failed_attempts_address_limit` | 1 to 10,000          | 100        | `Min` |
   | `client_authentication_failed_attempts_window`        | one minute to one day | 15 minutes | `Max` |

   With the defaults, one address tries at most 960 credentials of one client a day and 9,600 across clients. A
   narrower scope may only lower a limit or lengthen the window. A successful authentication clears nothing: clearing
   would cost a write on every authentication, and a client that holds its credential does not fail.
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
   guard, fails open.
8. **ADR-1391 is amended.** The default limits on failed client authentications that its decision 5 and its options
   considered name are this decision; they complement the PBKDF2 gate, which still bounds the processors.

## Options considered

- **Give `rate_limit_address_per_minute` and `rate_limit_tenant_per_minute` default values.** Rejected: they count
  successful requests, so a value that bounds guessing refuses legitimate traffic, and a value that admits it bounds
  nothing (Context). They remain capacity limits a tenant sets.
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
- **Windows that start at the first failure, as the password limit's do.** Rejected: the counter's expiry is not read,
  so `Retry-After` could only be an upper bound; aligned windows give the exact time, and bound a day's guesses the same.
- **Answer a refusal with `invalid_client`.** Rejected: a legitimate client behind the address would read its
  credential as wrong and stop instead of retrying.
- **Fail open with a breaker, as the rate limits do.** Rejected: an outage of the shared state would lift the limit
  exactly when an attacker can cause one (project-template ADR-0144).
- **Audit only the failure that reaches a limit.** Rejected: an operator investigating a refused client needs each
  refusal, and the refused requests are already published as `openid.error`; a refusal costs no verification.

## Consequences

- Online guessing of a client's credential is bounded by default, for every method and every endpoint that
  authenticates a client, without a per-`client_id` lockout.
- Every client authentication from an address costs one read of the shared state, and each failure one batched write;
  on Valkey each is a round trip.
- Every client behind one address shares the address's allowance: an attacker behind a client's NAT gateway can make
  that client wait until the window ends. A host behind a reverse proxy must forward the client's address
  (`ReverseProxy`), or every caller shares the proxy's address.
- Concurrent failures can exceed a limit by the number in flight, because the count is read before the verification and
  incremented after it.
- An outage of the shared state fails client authentication.
- A guesser that invents `client_id`s creates a counter for each, which expires with its window.
- This implements issue [#533](https://github.com/NCodeGroup/Identity/issues/533).

## References

- [RFC 9700 §4.10](https://www.rfc-editor.org/rfc/rfc9700#section-4.10), client authentication.
- [NIST SP 800-63B-4 §3.2.2](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/), rate limiting.
- [OWASP Credential Stuffing Prevention Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Credential_Stuffing_Prevention_Cheat_Sheet.html).
- [ADR-1196](1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md),
  [ADR-1237](1237-failed-password-attempts-are-limited-per-account.md),
  [ADR-1246](1246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md),
  [ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md).
- Code: `NCode.Identity.OpenId.Authentication.Clients.IClientAuthenticationAttemptLimiter`,
  `NCode.Identity.OpenId.Authentication.Clients.DefaultClientAuthenticationAttemptLimiter`,
  `NCode.Identity.OpenId.Authentication.Clients.DefaultClientAuthenticationAttemptSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.Clients.Handlers.CommonClientAuthenticationHandler`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.RateLimitErrors`,
  `NCode.Identity.OpenId.Authentication.RateLimiting.RemoteAddressPartition`.
