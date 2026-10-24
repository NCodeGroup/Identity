# 237. Failed password attempts are limited per account

- **Status:** Accepted (amended by
  [ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md): a password is
  verified in the same time whether or not an account has the user name; and by
  [ADR-0244](0244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md): the verification's own
  lookup names the counted user name, and a password refused at the limit is verified against the dummy hash and
  counted)
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The interactive sign-in, the password grant and the self-service change of a password
([ADR-0230](0230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md)) verify a password as often
as they are asked, so a password can be guessed without end (backlog `R102`). The request rate limiter
([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)) counts requests per tenant, per
address and per client, never per account, so a guesser that spreads its attempts across addresses is not limited by
it.

No store limits the attempts. The Entity Framework store has no lockout, and the ASP.NET Core Identity adapter treats
Identity's lockout as a disabled account and refuses a locked-out user without verifying the password
([ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)); it does not count a
failed password.

NIST SP 800-63B-4 §3.2.2 requires a verifier to limit the failed attempts on an account: the number of consecutive
failures does not exceed 100, a technique such as a waiting period after a failure may replace or add to that limit,
and the verifier SHOULD disregard the failures that precede a successful authentication.

## Decision

**The interactive sign-in, the password grant and the self-service change count a failed password per account in the
shared `IExpiringCounterSet`, and refuse every password for an account that reached the tenant's limit, until the window
of its first failure ends, with exactly the answer that a wrong password gets.**

1. **The seam has three members.** `IPasswordAttemptLimiter` is in `NCode.Identity.OpenId.Authentication.Abstractions`,
   namespace `NCode.Identity.OpenId.Authentication.Passwords`, beside the password policy
   ([ADR-0236](0236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)). `IsLimitReachedAsync(OpenIdContext,
   string userName, CancellationToken)` returns `ValueTask<bool>`, `RecordFailureAsync(OpenIdContext, string userName,
   CancellationToken)` counts one failure, and `ResetAsync(string tenantId, string userName, CancellationToken)` clears
   the count. The first two read the tenant's settings from the context. A reset needs no settings, so it takes the
   tenant's identifier, and the management API clears the count of the addressed tenant without materializing it.
   `DefaultPasswordAttemptLimiter` is registered by `AddPasswordServices()`. The limiter sits above
   `ILocalAccountStore`, so it applies to every store.
2. **One counter holds the failures of one account.** The key is `password-attempts:{tenantId}:{digest}`, built by
   `ExpiringKeys.Create`, where the digest is the SHA-256 of the length-prefixed UTF-8 bytes of the folded user name, so
   the shared state holds no user name. The folding is `Normalize(NormalizationForm.FormKC)`, then `ToUpperInvariant()`,
   then `ToLowerInvariant()`. The store passes the user name that it holds for the submitted one, which the lookup of
   the verification itself finds, or the submitted name when no account has it
   ([ADR-0244](0244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md)). The store compares names
   in its database, whose collation can match spellings that differ in .NET, such as a trailing space under a padding
   collation or an accent under an accent-insensitive one, so every spelling that matches an account shares that
   account's count. The folding gives the spellings of a name that no account has one count as well: the Entity
   Framework store lower-cases a name, and ASP.NET Core Identity composes and upper-cases it.
3. **The limit is read before the verification, and a failure is counted after it.** `IsLimitReachedAsync` reads the
   counter with `GetAsync`, and the limit is reached when the count is at least `password_failed_attempts_limit`. The
   store then verifies the password against its dummy hash instead of the account's hash, and the caller answers as it
   answers a wrong password ([ADR-0244](0244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md)):
   the sign-in redirects to the login page with `invalid_credentials`, the password grant answers its failed
   authentication, and the change reports `invalid_current_password`. After a failed verification, a refusal included,
   `RecordFailureAsync` calls `IncrementAsync` with an expiry of the current time plus
   `password_failed_attempts_window`. The counter set starts a counter at one with that expiry and leaves the expiry of
   a live counter unchanged ([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)), so the
   window starts at the first failure, and a refused password is counted and does not extend it. The refusal lasts
   until the window ends.
4. **Two tenant settings set the limit.** Both apply to tenants and neither is discoverable
   ([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)).
   - `password_failed_attempts_limit` is an `int` between 1 and 100 that defaults to 10, and merges with `Min`, so a
     narrower scope may only lower it. 100 is the most that NIST allows.
   - `password_failed_attempts_window` is a `TimeSpan` between one minute and one day that defaults to 15 minutes, and
     merges with `Max`, so a narrower scope may only lengthen it.

   With the defaults, an account takes at most 960 guesses a day, against a password of at least 15 characters that is
   on no blocklist.
5. **A verified password and an administrator's password clear the count.** After the store verifies a password, the
   caller calls `ResetAsync`, so the failures that precede a success are disregarded. The management API's create and
   reset call `ResetAsync` last, after the password is saved, the sessions end and the change is audited, because the
   limit fails closed and a counter store that throws must not skip them. A reset by an administrator restores the
   access of the end user whose password was being guessed.
6. **A failure is recorded even when the request is cancelled.** `RecordFailureAsync` runs with
   `CancellationToken.None`, so a client that disconnects after it submits a guess does not skip the count. The
   management API clears the count with `CancellationToken.None` as well, because the password is saved.
7. **A user name that no account has is counted too.** The caller counts it under the name as submitted, so the count,
   the refusal and the answer of the limit are the same for a name that an account has and for one that none has, and
   the limit reveals nothing about whether an account exists. The limiter's log message never carries a user name. A
   store verifies the password against a dummy hash when no account has the name, so the verification takes as long as
   well ([ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md)).
8. **`IExpiringCounterSet.RemoveAsync(key, cancellationToken)` removes a counter.** The next increment starts the
   counter again at one with the expiry of that increment, and removing a counter that is absent or expired does
   nothing. The in-process adapter removes the entry from its dictionary, and `ValkeyExpiringCounterSet` runs `DEL` on
   the key and records the operation `counter.remove` in the metric of the shared-state operations
   ([metrics](../metrics.md)).
9. **A failure of the counter store fails closed.** `DefaultPasswordAttemptLimiter` lets the exception propagate, so the
   sign-in, the grant or the change fails. The request rate limiter fails open, because it guards capacity
   ([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)); this limit is a security
   control, and an outage of the shared state must not lift it.
10. **The ASP.NET Core Identity adapter keeps its own meaning of lockout.** It does not call `AccessFailedAsync`, and
    Identity's lockout means that the account is disabled
    ([ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)). The limit applies
    to it because the limiter sits above the store.
11. **A refusal is logged and audited without telling the client.** `DefaultPasswordAttemptLimiter` logs
    `PasswordAttemptLimitReached` at `Information` with the tenant's identifier and the limit, each time it refuses a
    password. The password grant audits the refusal as a failed subject authentication with its own reason, "The account
    has reached its limit of failed password attempts.", so an operator tells it from a wrong password, and the client
    cannot.

## Options considered

- **Key the counter by the remote address as well.** Rejected: a distributed guesser gets a fresh allowance per address,
  and the request rate limiter already limits by address.
- **Key the counter by the submitted user name alone.** Rejected: a database collation that pads or ignores accents
  matches spellings that the folding keeps apart, and each would get its own allowance against one account.
- **Count the attempts before the verification.** Rejected: a success would count, and a busy client of the password
  grant would lock out its own end users.
- **Disable the account after 100 consecutive failures until it is rebound.** Rejected: it hands anyone who knows a user
  name a lasting denial of service. The throttle already bounds the guesses to the limit in each window.
- **Reuse `AccessFailedAsync` of ASP.NET Core Identity.** Rejected: it limits one store only, and its lockout reads as a
  disabled account.
- **Fail open like the request rate limiter.** Rejected: an outage of the shared state would lift the limit.
- **Never reset the count.** Rejected: the typing errors of a legitimate end user accumulate toward the limit.

## Consequences

- Someone who knows a user name can keep the password of that account refused, one window at a time, by failing the
  limit within each window. A reset by an administrator clears the count, and an end user waits at most one window.
- Concurrent attempts can exceed the limit by the number in flight, because the count is read before the verification
  and incremented after it.
- A sign-in costs the one lookup of the account that its verification makes, one read of the counter, and one increment
  or, after a verified password, one removal of it, each a round trip to the shared state when a deployment uses
  Valkey.
- A guesser that invents user names creates a counter for each of them, and each expires with its window.
- **Breaking:** `IExpiringCounterSet` gains `RemoveAsync`, which a custom adapter implements, and
  `IPasswordAttemptLimiter` is added.
- This implements backlog `R102`.

## References

- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/) §3.2.2 — rate limiting of failed
  attempts.
- [ADR-0164](0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) — the shared state that the
  replicas use.
- [ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — the counter set and its
  increment.
- [ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md) — the lockout of
  ASP.NET Core Identity is a disabled account.
- [ADR-0230](0230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md) — the self-service change.
- [ADR-0236](0236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) — the password policy.
- Code: `NCode.Identity.OpenId.Authentication.Passwords.IPasswordAttemptLimiter`,
  `NCode.Identity.OpenId.Authentication.Passwords.DefaultPasswordAttemptLimiter`,
  `NCode.Identity.SharedState.IExpiringCounterSet`.
