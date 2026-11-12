# 1237. Failed password attempts are limited per account, and a refusal costs what a wrong password costs

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The interactive sign-in, the password grant and the self-service change of a password
([ADR-1230](1230-an-end-user-changes-their-password-through-an-optional-seam-and-keeps-only-the-current-session.md))
verify a password as often as they are asked, so a password can be guessed without end. The request rate limiter
([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)) counts requests per
tenant, per address and per client, never per account, so a guesser that spreads its attempts across addresses is not
limited by it.

No store limits the attempts. The Entity Framework store has no lockout, and the ASP.NET Core Identity adapter treats
Identity's lockout as a disabled account and refuses a locked-out user without verifying the password
([ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md)); it does
not count a failed password.

NIST SP 800-63B-4 §3.2.2 requires a verifier to limit the failed attempts on an account: the number of consecutive
failures does not exceed 100, a technique such as a waiting period after a failure may replace or add to that limit, and
the verifier SHOULD disregard the failures that precede a successful authentication.

A limit that refuses a password must also not tell. A store verifies a password against a dummy hash when it holds no
hash it may verify
([ADR-1239](1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md)), so a wrong password
costs the same whether or not an account has the user name. A refusal at the limit has to cost the same, in the answer, in
the work and in the count. Otherwise three differences tell a caller something:

- A lookup that names the counted user name, if it reads the account's claims only when an account has the name, runs one
  more database query on that path, where no hash hides it.
- A refusal that runs no hash tells by the time of the answer that the limit was reached.
- A database collation that matches spellings the folding of the counter key keeps apart gives them one count when an
  account has the name and separate counts when none has, so reaching the limit under one spelling and trying another
  would tell by the time of the answer whether an account has the name.

## Decision

**The interactive sign-in, the password grant and the self-service change count a failed password per account in the
shared `IExpiringCounterSet`, and refuse every password for an account that reached the tenant's limit, until the window
of its first failure ends, with exactly the answer, and the work, that a wrong password gets. The store looks the account
up once, asks its caller whether to refuse the password with the user name the account holds, and verifies a refused
password against its dummy hash; every result that is not verified, a refusal included, is counted under that user
name.**

1. **The seam has three members.** `IPasswordAttemptLimiter` is in `NCode.Identity.OpenId.Authentication.Abstractions`,
   namespace `NCode.Identity.OpenId.Authentication.Passwords`, beside the password policy
   ([ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)). `IsLimitReachedAsync(OpenIdContext,
   string userName, CancellationToken)` returns `ValueTask<bool>`, `RecordFailureAsync(OpenIdContext, string userName,
   CancellationToken)` counts one failure, and `ResetAsync(string tenantId, string userName, CancellationToken)` clears
   the count. The first two read the tenant's settings from the context. A reset needs no settings, so it takes the
   tenant's identifier, and the management API clears the count of the addressed tenant without materializing it.
   `DefaultPasswordAttemptLimiter` is registered by `AddPasswordServices()`. The limiter sits above
   `ILocalAccountStore`, so it applies to every store.
2. **One counter holds the failures of one account.** The key is `ExpiringKeys.Create("password-attempts", tenantId,
   digest)`, where the digest is the SHA-256 of the length-prefixed UTF-8 bytes of the folded user name, so the shared
   state holds no user name. The folding is `Normalize(NormalizationForm.FormKC)`, then `ToUpperInvariant()`, then
   `ToLowerInvariant()`. The counted name is the one the store holds for the submitted one, which the lookup of the
   verification itself finds, or the submitted name when no account has it. The store compares names in its database,
   whose collation can match spellings that differ in .NET, such as a trailing space under a padding collation or an
   accent under an accent-insensitive one, so every spelling that matches an account shares that account's count. The
   folding gives the spellings of a name that no account has one count as well: the Entity Framework store lower-cases a
   name, and ASP.NET Core Identity composes and upper-cases it.
3. **One store call verifies.** `ILocalAccountStore.VerifyCredentialAsync(userName, password, isRefusedAsync,
   cancellationToken)` returns a `LocalAccountVerificationResult`. The store looks the account up once, calls
   `isRefusedAsync` once with the user name that the account holds, or the submitted one when none has it, and then
   verifies once. `LocalAccountVerificationResult` is a public sealed class in `NCode.Identity.OpenId.Accounts.Stores`
   with a required `UserName`, the `Account` whose password verified, `IsRefused` and `IsVerified`. One lookup names the
   counted user name and finds the hash, so no query depends on the account existing before the verification. The Entity
   Framework store keeps its single query with the claims include. The ASP.NET Core Identity adapter calls
   `FindByNameAsync`, reads the lockout and the hash of a found user, calls `isRefusedAsync`, and then verifies.
4. **A refused password is verified against the dummy hash, never the stored hash.** The result of a refusal carries no
   account and `IsRefused`. A refusal does the work of a wrong password: one verification against a hash of the current
   parameters, and two for a password that is not in NFC
   ([ADR-1238](1238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md)). Verifying the stored hash at the limit
   would let a right password cost a different number of verifications or a rehash, and the refusal would tell it.
5. **The callers pass the gate.** The interactive sign-in (`LocalAccountInteractiveCredentialVerifier`), the password
   grant (`DefaultAuthenticatePasswordGrantHandler`) and the self-service change of a password
   (`LocalAccountInteractivePasswordChanger`) pass the `IsRefusedAsync` of a `PasswordAttemptGate` as `isRefusedAsync`.
   The gate asks `IPasswordAttemptLimiter.IsLimitReachedAsync` and remembers its answer, and none of the callers looks
   the account up to name the count. A result that a store verifies after the gate refused is treated as a refusal, and a
   store that never calls `isRefusedAsync` fails the verification with an `InvalidOperationException`, so no store lifts
   the limit.
6. **Every result that is not verified is counted.** The caller calls `RecordFailureAsync` with the `UserName` of the
   result on `CancellationToken.None`, so a client that disconnects after it submits a guess does not skip the count, and
   a refusal does exactly the work of a failed verification: one increment of the counter. `IncrementAsync` sets an
   expiry of the current time plus `password_failed_attempts_window`. The counter set starts a counter at one with that
   expiry and leaves the expiry of a live counter unchanged
   ([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)), so the window
   starts at the first failure, and a refusal is counted and does not extend it. The limit is reached when the count is at
   least `password_failed_attempts_limit`, and the refusal lasts until the window ends.
7. **A refusal answers like a wrong password.** The sign-in redirects to the login page with `invalid_credentials`, the
   password grant answers its failed authentication, and the change reports `invalid_current_password`. The grant audits
   the refusal as a failed subject authentication with the reason `attempt_limit_reached`, so an operator tells it from a
   wrong password and the client cannot. `DefaultPasswordAttemptLimiter` logs `PasswordAttemptLimitReached` at
   `Information` with the tenant's identifier and the limit each time it refuses a password, and never the user name.
8. **Two tenant settings set the limit.** Both apply to tenants and neither is discoverable
   ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)).
   - `password_failed_attempts_limit` is an `int` between 1 and 100 that defaults to 10, and merges with `Min`, so a
     narrower scope may only lower it. 100 is the most that NIST allows.
   - `password_failed_attempts_window` is a `TimeSpan` between one minute and one day that defaults to 15 minutes, and
     merges with `Max`, so a narrower scope may only lengthen it.

   With the defaults, an account takes at most 960 guesses a day, against a password of at least 15 characters that is on
   no blocklist.
9. **A verified password and an administrator's password clear the count.** After the store verifies a password, the
   caller calls `ResetAsync`, so the failures that precede a success are disregarded. The management API's create and
   reset clear the count through `IPostCommitSecurityEffects.ResetPasswordAttemptsAsync` after the commit and the
   termination of the sessions, before the audit record, on its own bounded token
   ([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)). A reset by an administrator
   restores the access of the end user whose password was being guessed.
10. **A user name that no account has is counted too.** The caller counts it under the name as submitted, so the count,
    the refusal and the answer of the limit are the same for a name that an account has and for one that none has, and the
    limit reveals nothing about whether an account exists. The stores keep their database collation for matching: with
    items 4 and 6, neither the answer nor the work of a refusal differs from that of a failed verification, so a count
    that a collation shares among spellings tells nothing, and the deployment's collation keeps deciding which spellings
    sign in.
11. **`IExpiringCounterSet.RemoveAsync(key, cancellationToken)` removes a counter.** The next increment starts the counter
    again at one with the expiry of that increment, and removing a counter that is absent or expired does nothing. The
    in-process adapter deletes the entry from its dictionary, and `ValkeyExpiringCounterSet` runs `DEL` on the key and
    records the operation `counter.remove` in the metric of the shared-state operations ([metrics](../metrics.md)).
12. **A failure of the counter store fails closed.** `DefaultPasswordAttemptLimiter` lets the exception propagate, so the
    sign-in, the grant or the change fails. The request rate limiter fails open, because it guards capacity
    ([ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md)); this limit is a
    security control, and an outage of the shared state must not lift it.
13. **The ASP.NET Core Identity adapter keeps its own meaning of lockout.** It never calls `AccessFailedAsync`, and
    Identity's lockout means that the account is disabled
    ([ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md)). The
    limit applies to it because the limiter sits above the store.

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
- **A second, claim-free lookup to name the counted user name.** Rejected: it is two queries, and the account can change
  between them, so the user name that is counted may not be the one that is verified.
- **A store-owned credential handle that the caller verifies later.** Rejected: the adapter's dependency-injection scope
  would outlive a call.
- **Verifying the stored hash and refusing after.** Rejected: a right password would cost other work than a wrong one, or
  a rehash, so the refusal would tell it.
- **Strict matching of user names in every store.** Rejected: the deployment's collation decides sign-in matching, and
  items 4 and 6 leave nothing in a shared count to tell.
- **The chosen design.** A per-account counter above the store, one store call that consults the gate between its lookup
  and its verification, and a refusal that does a wrong password's work, keeps the limit store-independent and the answer
  uniform.

## Consequences

- Someone who knows a user name can keep the password of that account refused, one window at a time, by failing the limit
  within each window. A reset by an administrator clears the count, and an end user waits at most one window.
- Concurrent attempts can exceed the limit by the number in flight, because the count is read before the verification and
  incremented after it.
- A sign-in, a password grant or a change of a password makes one lookup of the account by its user name, one read of the
  counter, and one increment or, after a verified password, one removal of it, each a round trip to the shared state when
  a deployment uses Valkey. The change finds the signed-in account by its identifier before it, and the grant resolves the
  principal of a verified password in a second store manager.
- A password refused at the limit costs one dummy verification and one counter increment, and answers as a wrong password
  does, so neither the answer nor the work tells whether an account has the user name under any spelling that a database
  collation matches.
- A guesser that invents user names creates a counter for each of them, and each expires with its window.
- A custom `ILocalAccountStore` calls `isRefusedAsync` once, between its lookup and its verification, and a store that
  never calls it fails every verification. A custom `IExpiringCounterSet` implements `RemoveAsync`.

## References

- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/) §3.2.2 — rate limiting of failed
  attempts.
- [OWASP ASVS 5.0 V6](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x15-V6-Authentication.md) — V6.3.8, valid users
  cannot be deduced from failed authentication.
- [ADR-1164](1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md) — the
  shared state that the replicas use.
- [ADR-1246](1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md) — the counter set and
  its increment.
- [ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md) — the
  lockout of ASP.NET Core Identity is a disabled account.
- [ADR-1230](1230-an-end-user-changes-their-password-through-an-optional-seam-and-keeps-only-the-current-session.md) — the
  self-service change.
- [ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) — the password policy.
- [ADR-1238](1238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md) — the NFC form a password is hashed and
  verified in.
- [ADR-1239](1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) — a verification takes as
  long whether or not the account exists.
- [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md) — post-commit work on its own
  bounded token.
- Code: `NCode.Identity.OpenId.Authentication.Passwords.IPasswordAttemptLimiter`,
  `NCode.Identity.OpenId.Authentication.Passwords.DefaultPasswordAttemptLimiter`,
  `NCode.Identity.SharedState.IExpiringCounterSet`, `NCode.Identity.OpenId.Accounts.Stores.ILocalAccountStore`,
  `NCode.Identity.OpenId.Accounts.Stores.LocalAccountVerificationResult`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores.LocalAccountStore`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.AspNetIdentityLocalAccountStore`,
  `NCode.Identity.OpenId.Accounts.Authentication.PasswordAttemptGate`.
