# 238. A password is judged, hashed and verified in its NFC form

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

Unicode writes the same text as different sequences of code points: `é` is U+00E9, or `e` followed by the combining
acute accent U+0301. A device, an input method, a password manager or a paste produces either one, so a verifier that
hashes the bytes as submitted holds two passwords for what an end user sees as one, and an end user who types the
password on another device cannot sign in (backlog `R106`).

NIST SP 800-63B-4 §3.1.1.2 says that the verifier SHOULD apply the normalization process for stabilized strings with
NFC (UAX #15 §12.1) before it hashes a password, and that each Unicode code point counts as one character.

The password policy ([ADR-0236](0236-a-password-is-judged-by-one-policy-wherever-it-is-set.md)) counts code points and
asks the blocklists, while the stores hash, so all of them must see one form: eight letters that each carry a combining
accent are sixteen code points, and eight in NFC. A policy that counts the submitted form accepts a password whose
hashed form is too short.

Stored hashes of the password as submitted exist: a database that a store wrote without normalizing, and every
ASP.NET Core Identity database, whose own pages hash the password as typed.

The ASP.NET Core Identity adapter differs in what it refuses as well. The password methods of `UserManager<TUser>`
(`CreateAsync(user, password)`, `ResetPasswordAsync`, `AddPasswordAsync` and `ChangePasswordAsync`) run every
`IPasswordValidator<TUser>` first, and the default one requires a digit, an upper-case letter, a lower-case letter and a
non-alphanumeric character. NIST forbids such composition rules, so those methods refuse passwords that the policy
accepts, and the management API answers `500` (backlog `R105`).

## Decision

**The password policy judges, and every store hashes and verifies, a password's Unicode Normalization Form C (NFC); a
stored hash of the password as submitted still verifies and is replaced; and the ASP.NET Core Identity adapter hashes
with the user manager's hasher and stores the hash through the user store, so no ASP.NET Core Identity password
validator judges a password the server sets.**

1. **The form is NFC.** NIST SP 800-63B-4 §3.1.1.2 names it, and RFC 8265 chooses it for the OpaqueString profile of
   PRECIS. NFKC and NFKD fold compatibility characters, such as fullwidth letters and ligatures, into others, so they
   merge passwords that differ visibly.
2. **The policy normalizes first.** `DefaultPasswordPolicy` normalizes the password, counts the code points of the NFC
   form against the minimum and the maximum, and asks each blocklist with the NFC form, in that order. Its callers pass
   the password as submitted. The length is the length of what a store hashes, and a decomposed spelling does not pass a
   blocklist that lists the composed one.
3. **`IPasswordHasher` owns the form.** `DefaultPasswordHasher` hashes and verifies the NFC form of the UTF-8 bytes it
   is given, so the Entity Framework store and any other store that hashes through it need no code of their own. It
   decodes the bytes, normalizes the text and encodes it again in sensitive rentals, which the pinned, zeroing secure
   pool lends ([ADR-0116](0116-memory-allocations-and-buffer-operations.md)) and which it disposes at once. Bytes that
   are not valid UTF-8 spell no text to normalize, so it hashes them as they are. `ILocalAccountStore` and its callers
   pass the UTF-8 bytes of the password as submitted.
4. **A stored hash of the submitted form keeps verifying and is replaced.** The hasher verifies the NFC form first.
   When that fails and the NFC form differs from the submitted form, it verifies the submitted form, and a match is
   `PasswordVerificationResult.SuccessRehashNeeded`. The Entity Framework store takes its rehash branch and hashes the
   NFC form, and its caller's unit of work saves the replacement. The adapter stores the hash of the NFC form at once
   and calls `UpdateAsync`, which keeps the security stamp because the credential is the same, and it ignores a refused
   update because the stored hash still verifies. No mark tells the format of a hash, so the fallback runs for every
   hash and the time of a verification does not depend on the format of the account's hash
   ([ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md)). A wrong password
   that is not in NFC costs two verifications, and one in NFC costs one.
5. **The adapter hashes with the user manager's hasher and stores the hash through the user store.** It hashes the NFC
   form with `UserManager<TUser>.PasswordHasher` and stores the hash with `IUserPasswordStore<TUser>`, which it gets by
   casting the scope's `IUserStore<TUser>`; a store that is not one throws `NotSupportedException`. A create hashes,
   stores the hash and calls `CreateAsync(user)`. A reset hashes, stores the hash and calls
   `UpdateSecurityStampAsync(user)`, which rotates the security stamp and saves the user. A verification asks the user
   manager's hasher about the NFC form and then about the submitted form, and replaces the hash as item 4 says. The
   adapter never calls `CreateAsync(user, password)`, `ResetPasswordAsync`, `GeneratePasswordResetTokenAsync`,
   `AddPasswordAsync`, `ChangePasswordAsync` or `CheckPasswordAsync`, so no `IPasswordValidator<TUser>` runs on a path
   of the server, and the host's own pages keep their validators.

## Options considered

- **NFKC or NFKD.** Rejected: they fold compatibility characters, such as fullwidth letters and ligatures, into others
  and merge passwords that differ visibly, and NIST names NFC.
- **Normalizing in each caller.** Rejected: a store could not verify a stored hash of the submitted form, and the five
  callers would repeat the code.
- **Normalizing in the Entity Framework store.** Rejected: the hasher owns what it hashes, and every store that uses it
  gets the form.
- **A format mark on NFC hashes, with the fallback only for the unmarked ones.** Rejected: the time of a verification
  would depend on the format of the account's hash, which
  [ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) must not reveal, and a
  hash of ASP.NET Core Identity has no room for a mark.
- **Removing the password validators of ASP.NET Core Identity from the container, or translating their refusal into
  `400`.** Rejected: it changes the host's own pages, and it keeps composition rules in the way of a password that the
  policy accepts.
- **Decorating the host's `IPasswordHasher<TUser>` so that its own pages normalize as well.** Deferred (backlog
  `R108`): it depends on the order of the host's registrations.

## Consequences

- Another composition of the same characters verifies at the sign-in, the password grant and the change of a password.
- The policy refuses a password whose NFC form is too short, even when the submitted form is long enough.
- A host's own ASP.NET Core Identity pages verify the password as typed, so an end user who types a password that is
  not in NFC cannot sign in there once the server has stored or rehashed an NFC hash (backlog `R108`).
- .NET does not normalize in globalization-invariant mode, where a password is hashed as submitted, and the reference
  image (`noble-chiseled-extra`) carries ICU.
- **Breaking:** an `IPasswordHasher` hashes and verifies the NFC form of the bytes it is given, with the fallback to the
  submitted form.
- This implements backlog `R105` and `R106`.

## References

- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/) §3.1.1.2 — normalization of a password
  and its length in code points.
- [UAX #15](https://unicode.org/reports/tr15/) — the Unicode normalization forms.
- [RFC 8265](https://www.rfc-editor.org/rfc/rfc8265#section-4.2) §4.2 — the OpaqueString profile normalizes to NFC.
- [ADR-0116](0116-memory-allocations-and-buffer-operations.md) — sensitive buffers.
- [ADR-0229](0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md) — a locked-out user's
  password is not verified.
- [ADR-0236](0236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) — the password policy.
- [ADR-0239](0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) — a verification takes
  as long whether or not the account exists.
- Code: `NCode.Identity.OpenId.Accounts.Credentials.IPasswordHasher`,
  `NCode.Identity.OpenId.Accounts.Credentials.DefaultPasswordHasher`,
  `NCode.Identity.OpenId.Authentication.Passwords.DefaultPasswordPolicy`,
  `NCode.Identity.OpenId.Accounts.AspNetIdentity.AspNetIdentityLocalAccountStore`.
