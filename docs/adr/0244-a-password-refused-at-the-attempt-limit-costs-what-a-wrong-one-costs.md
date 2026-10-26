# 244. A password refused at the attempt limit costs what a wrong one costs

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

The limit on failed password attempts ([ADR-0237](0237-failed-password-attempts-are-limited-per-account.md)) counts
under the user name that the store holds for the submitted one, and a store verifies a password against a dummy hash
when it holds no hash it may verify
([ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md)). ADR-0239 recorded
three differences that remained between a refusal at the limit and a wrong password (issue [#300](https://github.com/NCodeGroup/Identity/issues/300)):

- The lookup that named the counted user name read the account's claims only when an account had the name, so the
  ASP.NET Core Identity adapter ran one more database query on that path, where no hash hides it.
- A refusal at the limit ran no hash, so the time of the answer told that the limit was reached.
- A database collation that matches spellings that the folding of ADR-0237 keeps apart gives them one count when an
  account has the name and separate counts when none has, so reaching the limit under one spelling and trying another
  told by the time of the answer whether an account has the name.

## Decision

**The store looks the account up once, asks its caller whether to refuse the password with the user name the account
holds, and verifies a refused password against its dummy hash; every result that is not verified, a refusal included,
is counted under that user name.**

1. **One store call verifies.** `ILocalAccountStore.VerifyCredentialAsync(userName, password, isRefusedAsync,
   cancellationToken)` returns a `LocalAccountVerificationResult`. The store looks the account up once, calls
   `isRefusedAsync` once with the user name that the account holds, or the submitted one when none has it, and then
   verifies once. `LocalAccountVerificationResult` is a public sealed class in `NCode.Identity.OpenId.Accounts.Stores`
   with a required `UserName`, the `Account` whose password verified, `IsRefused`, and `IsVerified`. One lookup names
   the counted user name and finds the hash, so no query depends on the account existing before the verification. The
   Entity Framework store keeps its single query with the claims include. The adapter calls `FindByNameAsync`, reads
   the lockout and the hash of a found user, calls `isRefusedAsync`, and then verifies.
2. **A refused password is verified against the dummy hash, never the stored hash.** The result of a refusal carries no
   account and `IsRefused`. A refusal does the work of a wrong password: one verification against a hash of the
   current parameters, and two for a password that is not in NFC
   ([ADR-0238](0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md)). Verifying the stored hash at the
   limit would let a right password cost a different number of verifications or a rehash, and the refusal would tell
   it.
3. **Every result that is not verified is counted.** The caller calls `RecordFailureAsync` with
   `CancellationToken.None` and the `UserName` of the result, so a refusal does exactly the work of a failed
   verification: one increment of the counter. The counter keeps the expiry of its first failure, so a refusal does not
   extend the window ([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)).
4. **The callers pass the limiter.** The interactive sign-in (`LocalAccountInteractiveCredentialVerifier`), the password
   grant (`DefaultAuthenticatePasswordGrantHandler`) and the self-service change of a password
   (`LocalAccountInteractivePasswordChanger`) pass the `IsRefusedAsync` of a `PasswordAttemptGate`, which asks
   `IPasswordAttemptLimiter.IsLimitReachedAsync` and remembers its answer, as `isRefusedAsync`, and none looks the
   account up to name the count. The grant audits a refusal with its own reason, so an operator tells it from a wrong
   password, and answers the client as it answers a wrong one. Each caller asks through `PasswordAttemptGate`, which
   remembers the limiter's answer: a result that a store verifies after the gate refused is answered as a refusal, and a
   store that never calls `isRefusedAsync` fails the verification with an `InvalidOperationException`, so no store lifts
   the limit.
5. **The stores keep counting under the user name they hold.** The stores do not match user names strictly. With items
   2 and 3 neither the answer nor the work of a refusal differs from that of a failed verification, so a count that a
   database collation shares among spellings tells nothing, and the deployment's collation keeps deciding which
   spellings sign in.
6. **Some differences stay.**
   - A stored hash with weaker parameters than the current ones costs less to verify until its next successful
     verification replaces it.
   - The adapter reads the lockout and the hash of a found user through the user store. The Entity Framework store of
     ASP.NET Core Identity answers both from the loaded user, so they add no query there.
   - A request that is refused before its user name is read, such as one whose client fails to authenticate, never
     reaches a store.
   - A verified password takes the work of its account: a rehash, and the read of the account's claims.

## Options considered

- **A second, claim-free lookup.** Rejected: it is two queries, and the account can change between them, so the user
  name that is counted may not be the one that is verified.
- **A store-owned credential handle that the caller verifies later.** Rejected: the adapter's dependency-injection scope
  would outlive a call.
- **Verifying the stored hash and refusing after.** Rejected: a right password would cost other work than a wrong one,
  or a rehash, so the refusal would tell it.
- **Strict matching of user names in every store.** Rejected: the deployment's collation decides sign-in matching, and
  items 2 and 3 leave nothing in a shared count to tell.

## Consequences

- The verification of a sign-in, a password grant or a change of a password makes one lookup of the account by its user
  name; the change finds the signed-in account by its identifier before it, and the grant resolves the principal of a
  verified password in a second store manager.
- A password refused at the limit costs one dummy verification and one counter increment, and answers as a wrong
  password does, so neither the answer nor the work tells whether an account has the user name under any spelling that
  a database collation matches.
- **Breaking:** `ILocalAccountStore.VerifyCredentialAsync` takes `isRefusedAsync` and returns
  `LocalAccountVerificationResult`, which a custom store implements: it calls `isRefusedAsync` once, between its lookup
  and its verification, and a store that never calls it fails every verification.
- This implements issue [#300](https://github.com/NCodeGroup/Identity/issues/300).

## References

- [OWASP ASVS 5.0 V6](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x15-V6-Authentication.md) — V6.3.8, valid users
  cannot be deduced from failed authentication.
- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/) §3.2.2 — rate limiting of failed
  attempts.
- [ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — the counter set and its
  increment.
- [ADR-0237](0237-failed-password-attempts-are-limited-per-account.md) — the limit on failed attempts.
- [ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) — a verification takes
  as long whether or not the account exists.
- Code: `NCode.Identity.OpenId.Accounts.Stores.ILocalAccountStore`,
  `NCode.Identity.OpenId.Accounts.Stores.LocalAccountVerificationResult`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores.LocalAccountStore`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.AspNetIdentityLocalAccountStore`,
  `NCode.Identity.OpenId.Authentication.Passwords.IPasswordAttemptLimiter`,
  `NCode.Identity.OpenId.Accounts.Authentication.PasswordAttemptGate`.
