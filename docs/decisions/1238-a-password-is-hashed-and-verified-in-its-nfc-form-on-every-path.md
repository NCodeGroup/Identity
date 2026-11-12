# 1238. A password is hashed and verified in its NFC form on every path

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

Unicode writes the same text as different sequences of code points: `é` is U+00E9, or `e` followed by the combining
acute accent U+0301. A device, an input method, a password manager or a paste produces either one, so a verifier that
hashes the bytes as submitted holds two passwords for what an end user sees as one, and an end user who types the
password on another device cannot sign in (issue [#297](https://github.com/NCodeGroup/identity/issues/297)).

NIST SP 800-63B-4 §3.1.1.2 says that the verifier SHOULD apply the normalization process for stabilized strings with
NFC (UAX #15 §12.1) before it hashes a password, and that each Unicode code point counts as one character.

The password policy ([ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)) counts code points and
asks the blocklists, while the stores hash, so all of them must see one form: eight letters that each carry a combining
accent are sixteen code points, and eight in NFC. A policy that counts the submitted form accepts a password whose
hashed form is too short.

Stored hashes of the password as submitted exist: a database that a store wrote without normalizing, and every
ASP.NET Core Identity database, whose own pages hash the password as typed.

The ASP.NET Core Identity adapter differs in what it refuses as well. The password methods of `UserManager<TUser>`
(`CreateAsync(user, password)`, `ResetPasswordAsync`, `AddPasswordAsync` and `ChangePasswordAsync`) run every
`IPasswordValidator<TUser>` first, and the default one requires a digit, an upper-case letter, a lower-case letter and a
non-alphanumeric character. NIST forbids such composition rules, so those methods refuse passwords that the policy
accepts, and the management API answers `500` (issue [#296](https://github.com/NCodeGroup/identity/issues/296)).

A host's own pages do not go through the adapter: `SignInManager<TUser>` calls `UserManager<TUser>.CheckPasswordAsync`,
and the pages call `CreateAsync(user, password)`, `ChangePasswordAsync` and `ResetPasswordAsync`, which hash and verify
the password as typed with the same `IPasswordHasher<TUser>`. A password that is not in NFC therefore fails at one of
the two once the other has stored or replaced its hash (issue
[#299](https://github.com/NCodeGroup/identity/issues/299)).

A host registers its `IPasswordHasher<TUser>` through `AddIdentityCore`, `AddIdentity` or `AddDefaultIdentity`, which
register the default hasher with `TryAdd`, so an earlier registration stays and a later one wins. A decorator that the
adapter adds must work whichever of the two comes first.

## Decision

**Every path that hashes or verifies a password uses its Unicode Normalization Form C (NFC): the password policy judges
it, `IPasswordHasher` hashes and verifies it, the ASP.NET Core Identity adapter and the host's own Identity pages
hash and verify it through `NormalizingPasswordHasher<TUser>`, and a stored hash of the password as submitted still
verifies and is replaced.**

1. **The form is NFC.** NIST SP 800-63B-4 §3.1.1.2 names it, and RFC 8265 chooses it for the OpaqueString profile of
   PRECIS. NFKC and NFKD fold compatibility characters, such as fullwidth letters and ligatures, into others, so they
   merge passwords that differ visibly.
2. **The policy counts and asks with the NFC form.** `DefaultPasswordPolicy` normalizes the password, counts the code
   points of the NFC form against the minimum and the maximum, and asks each blocklist with the NFC form
   ([ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)). Its callers pass the password as
   submitted. The length is the length of what a store hashes, and a decomposed spelling does not pass a blocklist that
   lists the composed one.
3. **`IPasswordHasher` owns the form.** `DefaultPasswordHasher` hashes and verifies the NFC form of the UTF-8 bytes it
   is given, so the Entity Framework store and any other store that hashes through it need no code of their own. It
   decodes the bytes, normalizes the text and encodes it again in sensitive rentals, which the pinned, zeroing secure
   pool lends ([ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md))
   and which it disposes at once. Bytes that are not valid UTF-8 spell no text to normalize, so it hashes them as they
   are. `ILocalAccountStore` and its callers pass the UTF-8 bytes of the password as submitted.
4. **A stored hash of the submitted form keeps verifying and is replaced.** The hasher verifies the NFC form first.
   When that fails and the NFC form differs from the submitted form, it verifies the submitted form, and a match is
   `PasswordVerificationResult.SuccessRehashNeeded`. The Entity Framework store takes its rehash branch and hashes the
   NFC form, and its caller's unit of work saves the replacement. No mark tells the format of a hash, so the fallback
   runs for every hash and the time of a verification does not depend on the format of the account's hash
   ([ADR-1239](1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md)). A wrong password
   that is not in NFC costs two verifications, and one in NFC costs one.
5. **The adapter hashes with the user manager's hasher and stores the hash through the user store.** It hashes with
   `UserManager<TUser>.PasswordHasher` and stores the hash with `IUserPasswordStore<TUser>`, which it gets by casting
   the scope's `IUserStore<TUser>`; a store that is not one throws `NotSupportedException`. A create stores the hash and
   then calls `CreateAsync(user)`. A reset stores the hash and then calls `UpdateSecurityStampAsync(user)`, which
   rotates the security stamp and saves the user. Its own rehash calls `UpdateAsync`, which keeps the security stamp
   because the credential is the same, and it ignores a refused update because the stored hash still verifies.
6. **The adapter never runs an ASP.NET Core Identity password validator.** It never calls `CreateAsync(user, password)`,
   `ResetPasswordAsync`, `GeneratePasswordResetTokenAsync`, `AddPasswordAsync`, `ChangePasswordAsync` or
   `CheckPasswordAsync`, so no `IPasswordValidator<TUser>` runs on a path of the server, and the host's own pages keep
   their validators.
7. **`NormalizingPasswordHasher<TUser>` owns the form for Identity.** It is a public sealed `IPasswordHasher<TUser>`
   decorator in `NCode.Identity.OpenId.Accounts.AspNetIdentity`, for a `TUser` that is a class. `HashPassword` hashes
   the NFC form of the password with the inner hasher. `VerifyHashedPassword` verifies the NFC form with the inner
   hasher and returns its result; when that fails and the NFC form differs from the provided password, it verifies the
   provided password, and a match is `PasswordVerificationResult.SuccessRehashNeeded`. A password with an unpaired
   surrogate has no NFC form, which `string.Normalize` refuses, so it is used as provided.
8. **The registration wraps the host's registration whatever its order.** `AddAspNetIdentityLocalAccountStore<TUser>()`
   replaces the last non-keyed `IPasswordHasher<TUser>` registration in place with one of the same lifetime that wraps
   the registration's instance, factory or implementation type. When the host has registered none, it adds a scoped
   wrapper of `PasswordHasher<TUser>`, which the `TryAdd` of ASP.NET Core Identity that follows leaves in place. An
   inner hasher that already is a `NormalizingPasswordHasher<TUser>` is not wrapped again, so the decorator never
   normalizes twice. A hasher that a host registers after the call is the host's to wrap.
9. **The adapter requires the decorator and normalizes nothing itself.** `AspNetIdentityLocalAccountStore<TUser>`
   requires the user manager's hasher to be a `NormalizingPasswordHasher<TUser>` and throws an
   `InvalidOperationException` that names the fix when it hashes or verifies otherwise, so one implementation holds the
   steps of the NFC form.
10. **The host's own pages follow.** Their hashes and verifications go through the same decorator, so they hash and
    verify the NFC form too, and their rehash rotates the security stamp, as ASP.NET Core Identity does for every
    rehash.
11. **The runtime must normalize.** .NET does not normalize in globalization-invariant mode, where a password is hashed
    as submitted. The reference images (`noble-chiseled-extra`) carry ICU.

## Options considered

- **NFKC or NFKD.** Rejected: they fold compatibility characters, such as fullwidth letters and ligatures, into others
  and merge passwords that differ visibly, and NIST names NFC.
- **Normalizing in each caller.** Rejected: a store could not verify a stored hash of the submitted form, and the
  callers would repeat the code.
- **Normalizing in the Entity Framework store.** Rejected: the hasher owns what it hashes, and every store that uses it
  gets the form.
- **A format mark on NFC hashes, with the fallback only for the unmarked ones.** Rejected: the time of a verification
  would depend on the format of the account's hash, which
  [ADR-1239](1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) must not reveal, and a
  hash of ASP.NET Core Identity has no room for a mark.
- **Removing the password validators of ASP.NET Core Identity from the container, or translating their refusal into
  `400`.** Rejected: it changes the host's own pages, and it keeps composition rules in the way of a password that the
  policy accepts.
- **Documentation only.** Rejected: a host that never reads it has pages and a server that accept different spellings
  of one password, and nothing tells it.
- **Replacing `UserManager<TUser>`.** Rejected: a host's own subclass of it would break, and the host registers it.
- **Checking the hasher when the host starts.** Rejected: a hasher can need request services, so it cannot be built
  before a request; the adapter checks it when it hashes or verifies.
- **Chosen: the hasher owns the form, and the adapter wraps the host's hasher in a decorator.** One implementation
  holds the steps of the NFC form for the server and for the host's pages, whichever registration comes first.

## Consequences

- Another composition of the same characters verifies at the sign-in, the password grant and the change of a password,
  at the server and in a host's own pages.
- The policy refuses a password whose NFC form is too short, even when the submitted form is long enough.
- A hash of the submitted form that a store or a host's page wrote verifies and is replaced by a hash of the NFC form
  at its first verification.
- The `SignInManager<TUser>` of ASP.NET Core Identity answers an unknown user name without verifying a hash. That is
  the host's page behavior, not the adapter's.
- A host that registers its `IPasswordHasher<TUser>` after the call, or replaces `UserManager<TUser>.PasswordHasher`,
  wraps it in `NormalizingPasswordHasher<TUser>`; otherwise the adapter throws an `InvalidOperationException` that
  names that fix whenever it hashes or verifies a password.
- `NormalizingPasswordHasher<TUser>` is public API of the adapter package.
- This implements issues [#296](https://github.com/NCodeGroup/identity/issues/296),
  [#297](https://github.com/NCodeGroup/identity/issues/297) and
  [#299](https://github.com/NCodeGroup/identity/issues/299).

## References

- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/) §3.1.1.2 — normalization of a password
  and its length in code points.
- [UAX #15](https://unicode.org/reports/tr15/) — the Unicode normalization forms.
- [RFC 8265](https://www.rfc-editor.org/rfc/rfc8265#section-4.2) §4.2 — the OpaqueString profile normalizes to NFC.
- [ADR-1266](1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md) —
  sensitive buffers.
- [ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md) — a
  locked-out user's password is not verified.
- [ADR-1236](1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) — the password policy.
- [ADR-1239](1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) — a verification takes
  as long whether or not the account exists.
- Code: `NCode.Identity.OpenId.Accounts.Credentials.IPasswordHasher`,
  `NCode.Identity.OpenId.Accounts.Credentials.DefaultPasswordHasher`,
  `NCode.Identity.OpenId.Authentication.Passwords.DefaultPasswordPolicy`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.NormalizingPasswordHasher<TUser>`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.DefaultRegistration`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.AspNetIdentityLocalAccountStore`.
