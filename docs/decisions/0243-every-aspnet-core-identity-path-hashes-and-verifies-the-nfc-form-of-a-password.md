# 243. Every ASP.NET Core Identity path hashes and verifies the NFC form of a password

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

[ADR-0238](0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md) makes the ASP.NET Core Identity adapter
hash and verify a password's Unicode Normalization Form C (NFC) with the user manager's hasher, and normalize the
password itself. A host's own pages do not go through the adapter: `SignInManager<TUser>` calls
`UserManager<TUser>.CheckPasswordAsync`, and the pages call `CreateAsync(user, password)`, `ChangePasswordAsync` and
`ResetPasswordAsync`, which hash and verify the password as typed with the same `IPasswordHasher<TUser>`. A password
that is not in NFC therefore fails at one of the two once the other has stored or replaced its hash: the adapter
stores a hash of the NFC form, which a page that verifies the typed form does not match, and a page stores a hash of
the typed form, which the adapter replaces at its next verification (issue [#299](https://github.com/NCodeGroup/Identity/issues/299)).

A host registers its `IPasswordHasher<TUser>` through `AddIdentityCore`, `AddIdentity` or `AddDefaultIdentity`, which
register the default hasher with `TryAdd`, so an earlier registration stays and a later one wins. A decorator that the
adapter adds must work whichever of the two comes first.

## Decision

**The adapter wraps the host's `IPasswordHasher<TUser>` in the public `NormalizingPasswordHasher<TUser>`, which hashes
and verifies a password's NFC form, and hashes and verifies only through it, so the adapter and the host's own
ASP.NET Core Identity pages hash and verify the same form.**

1. **`NormalizingPasswordHasher<TUser>` owns the form.** It is a public sealed `IPasswordHasher<TUser>` decorator in
   `NCode.Identity.OpenId.Accounts.AspNetIdentity`, for a `TUser` that is a class. `HashPassword` hashes the NFC form
   of the password with the inner hasher. `VerifyHashedPassword` verifies the NFC form with the inner hasher and
   returns its result. When that fails and the NFC form differs from the provided password, it verifies the provided
   password, and a match is `PasswordVerificationResult.SuccessRehashNeeded`, so a hash of the typed form keeps
   verifying and the `UserManager<TUser>` replaces it with a hash of the NFC form
   ([ADR-0238](0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md)). A password with an unpaired
   surrogate has no NFC form, which `string.Normalize` refuses, so it is used as provided.
2. **The registration wraps the host's registration whatever its order.** `AddAspNetIdentityLocalAccountStore<TUser>()`
   replaces the last non-keyed `IPasswordHasher<TUser>` registration in place with one of the same lifetime that wraps
   the registration's instance, factory or implementation type. When the host has registered none, it registers a
   scoped one that wraps `PasswordHasher<TUser>`, which the `TryAdd` of ASP.NET Core Identity that follows leaves in
   place. An inner hasher that already is a `NormalizingPasswordHasher<TUser>` is not wrapped again, so the decorator
   never normalizes twice. A hasher that a host registers after the call is the host's to wrap.
3. **The adapter hashes and verifies only through it.** `AspNetIdentityLocalAccountStore<TUser>` takes
   `UserManager<TUser>.PasswordHasher`, requires it to be a `NormalizingPasswordHasher<TUser>`, and throws an
   `InvalidOperationException` that names the fix otherwise. It normalizes nothing of its own, so one implementation
   holds the steps of the NFC form. A path that hashes or verifies a password throws; a create without a password does
   not.
4. **The dummy hash is made and verified by the same hasher.** `AspNetIdentityDummyPasswordHash<TUser>` makes its hash
   with the normalizing hasher, and the adapter verifies it through that hasher, so the dummy takes the steps of a
   stored hash and a password that is not in NFC costs the same verifications on every path
   ([ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) item 5).
5. **A rehash by the host's own pages rotates the security stamp.** When `UserManager<TUser>.CheckPasswordAsync` of a
   host's page gets `SuccessRehashNeeded`, it replaces the hash with one of the NFC form and updates the security
   stamp, as ASP.NET Core Identity does for every rehash. The adapter's own rehash keeps the stamp, because the
   credential is the same.

## Options considered

- **Documentation only.** Rejected: a host that never reads it has pages and a server that accept different spellings
  of one password, and nothing tells it.
- **Normalizing in the adapter and wrapping only for its own calls.** Rejected: the host's pages keep hashing and
  verifying the typed form, so they diverge from the adapter without notice.
- **Replacing `UserManager<TUser>`.** Rejected: a host's own subclass of it would break, and the host registers it.
- **Checking the hasher when the host starts.** Rejected: a hasher can need request services, so it cannot be built
  before a request; the adapter checks it when it hashes or verifies.

## Consequences

- The host's pages and the server accept every composition of a password: an end user signs in at both with the
  composed or the decomposed spelling.
- A hash of the typed form that a host's page stored verifies at both and is replaced by a hash of the NFC form at its
  first verification.
- **Breaking:** a host that registers its `IPasswordHasher<TUser>` after the call, or replaces
  `UserManager<TUser>.PasswordHasher`, wraps it in `NormalizingPasswordHasher<TUser>`; otherwise the adapter throws an
  `InvalidOperationException` that names that fix whenever it hashes or verifies a password.
- The `SignInManager<TUser>` of ASP.NET Core Identity answers an unknown user name without verifying a hash. That is
  the host's page behavior, not the adapter's.
- `NormalizingPasswordHasher<TUser>` is public API of the adapter package.
- This implements issue [#299](https://github.com/NCodeGroup/Identity/issues/299).

## References

- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/) §3.1.1.2 — normalization of a password
  and its length in code points.
- [UAX #15](https://unicode.org/reports/tr15/) — the Unicode normalization forms.
- [ADR-0238](0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md) — a password is judged, hashed and
  verified in its NFC form.
- [ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) — a verification takes
  as long whether or not the account exists.
- Code: `NCode.Identity.OpenId.Accounts.AspNetIdentity.NormalizingPasswordHasher<TUser>`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.DefaultRegistration`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.AspNetIdentityLocalAccountStore`.
