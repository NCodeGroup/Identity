# 239. A password is verified in the same time whether or not its account exists

- **Status:** Accepted (amended by
  [ADR-0244](0244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md): a password refused at the
  attempt limit is verified against the dummy hash and counted, and the verification's own lookup names the counted
  user name)
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

OWASP ASVS 5.0 requirement V6.3.8 says that valid users cannot be deduced from failed authentication challenges, whether
by error messages, response codes or response times.

Verifying a password costs a password hash, which takes hundreds of milliseconds at the default cost, while finding no
account costs a lookup. A store that holds no hash to verify for the submitted user name would answer sooner than one
that verifies: no account has the user name, the account has no password, the ASP.NET Core Identity user is locked out
(the adapter refuses the password of a locked-out user without verifying it,
[ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)), or the stored hash
cannot be read. The sign-in, the password grant and the self-service change of a password would tell by their response
time whether an account exists (backlog `R107`).

The limit on failed attempts ([ADR-0237](0237-failed-password-attempts-are-limited-per-account.md)) counts a user name
that no account has like one that an account has, so its answer reveals nothing about the account, but it does not
change how long a verification takes.

## Decision

**When a store holds no hash it may verify for the submitted user name, it verifies the submitted password against a
dummy hash of the current parameters, through the path that verifies a stored hash, and refuses the password whatever
the result.**

1. **`IPasswordHasher.DummyPasswordHash` is a hash that no known password matches.** `DefaultPasswordHasher` builds it
   in its constructor, in the format of `HashPassword` and with the current iterations, from a random salt and a random
   key of the configured sizes. It runs no derivation, so building it is cheap, and verifying against it costs what
   verifying a stored hash of the current parameters costs.
2. **The Entity Framework store verifies one hash with one call.** `LocalAccountStore` verifies
   `storedHash ?? DummyPasswordHash` with a single call to `VerifyHashedPassword`. It returns no account when no account
   has the user name or the account has no password, whatever the result of verifying the dummy.
3. **`DefaultPasswordHasher` verifies an unreadable hash against the dummy.** A stored hash that the hasher cannot read
   takes the work of verifying the dummy hash and then fails, so a damaged or foreign hash is not told apart by its
   time.
4. **The adapter makes its dummy hash before it looks the user up.** `AspNetIdentityDummyPasswordHash<TUser>` is an
   internal singleton that `AddAspNetIdentityLocalAccountStore<TUser>()` registers. It holds a hash that the user
   manager's hasher made of a random password of 32 characters, and the adapter gets it before the lookup, so the first
   verification costs the same on every path. An unknown user name, a locked-out user, whose own hash stays unverified
   ([ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md)), and a user without
   a password, whose stored hash is absent or empty, are verified against it through the routine that verifies a stored
   hash, and refused.
5. **The dummy takes the steps of a stored hash.** A password that is not in NFC is verified in its NFC form and then as
   submitted ([ADR-0238](0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md)), against the dummy as
   against a stored hash, so a wrong password costs the same number of hash verifications on every path.
6. **Some differences stay.** A password refused at the limit of failed attempts takes the work of a wrong one
   ([ADR-0244](0244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md)), and these remain:
   - A stored hash with weaker parameters than the current ones costs less to verify until its next successful
     verification replaces it.
   - The adapter verifies a stored hash with the host's ASP.NET Core Identity hasher, which refuses a non-empty hash
     that it cannot read at its own cost: the default hasher fails at once on an unknown format and throws on a hash
     that is not Base64.
   - A request that is refused before its user name is read, such as one whose client fails to authenticate, never
     reaches a store.

## Options considered

- **Padding a failure to a constant duration.** Rejected: the duration depends on the hardware and the load, and it adds
  latency to every failure.
- **Hashing the submitted password instead of verifying a dummy.** Rejected: it is a different path, and the second
  verification that the hasher makes of a password that is not in NFC would not be mirrored.
- **Verifying the password of an account that reached its limit against its stored hash.** Rejected: a right password
  would take another number of verifications or a rehash, so the refusal would tell it; a refused password is verified
  against the dummy hash ([ADR-0244](0244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md)).

## Consequences

- Every failed attempt costs a password hash whether or not the account exists, and the request rate limiter bounds the
  rate ([ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)).
- Tests record that the hasher verified the dummy hash, never the time, because elapsed time is flaky in a parallel run.
- **Breaking:** `IPasswordHasher` gains `DummyPasswordHash`, which a custom hasher implements.
- This implements backlog `R107`.

## References

- [OWASP ASVS 5.0 V6](https://github.com/OWASP/ASVS/blob/v5.0.0/5.0/en/0x15-V6-Authentication.md) — V6.3.8, valid users
  cannot be deduced from failed authentication.
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html) —
  the responses to a failed authentication are the same whether or not the account exists.
- [ADR-0196](0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) — the request rate limiter.
- [ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md) — a locked-out user's
  password is not verified.
- [ADR-0237](0237-failed-password-attempts-are-limited-per-account.md) — the limit on failed attempts.
- [ADR-0238](0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md) — the form of a password that is hashed
  and verified.
- Code: `NCode.Identity.OpenId.Accounts.Credentials.IPasswordHasher`,
  `NCode.Identity.OpenId.Accounts.Credentials.DefaultPasswordHasher`,
  `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts.Stores.LocalAccountStore`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.AspNetIdentityLocalAccountStore`.
