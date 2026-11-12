# 1236. A password is judged by one policy wherever it is set

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

Four paths set the password of a local account: the management API's create and reset
([ADR-1229](1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md)), the
self-service change ([ADR-1230](1230-an-end-user-changes-their-password-through-an-optional-seam-and-keeps-only-the-current-session.md)), and the
developer and conformance seeders. A path that accepted any non-empty value would let an account hold a password that
anyone guesses at the first attempt.

NIST SP 800-63B-4 §3.1.1.2 sets what a verifier requires of a password:

- a password that is the only authentication factor has at least 15 characters, and one that is part of multi-factor
  authentication has at least 8;
- a verifier SHOULD permit a maximum length of at least 64 characters;
- each Unicode code point counts as one character;
- the verifier imposes no composition rules;
- the verifier compares the whole password with a blocklist of commonly used, expected and compromised passwords, and
  gives the reason when it refuses one;
- the verifier offers guidance for choosing a password.

The server has no second factor, so a password is the only factor and the 15-character requirement applies.

The rules for setting a password belong to the verifier, not to one store: the Entity Framework store, the ASP.NET Core
Identity adapter and a consumer's own store each hold a password, and each differs in what it refuses.

## Decision

**Every path that sets a local account's password asks one public `IPasswordPolicy`, which requires a length that the
tenant's settings set, imposes no composition rules, and refuses a password that a registered `IPasswordBlocklist`
lists.**

1. **The seam is in the Authentication abstractions package.** `IPasswordPolicy.ValidateAsync(PasswordPolicyContext,
   ReadOnlyMemory<char> password, CancellationToken)` returns `ValueTask<PasswordPolicyResult>`. `IPasswordPolicy`,
   `IPasswordBlocklist`, `PasswordPolicyContext`, `PasswordPolicyResult` and `PasswordPolicyErrors` are in
   `NCode.Identity.OpenId.Authentication.Abstractions`, namespace `NCode.Identity.OpenId.Authentication.Passwords`,
   because every caller references that package: accounts management, accounts authentication, the server's
   seeders and a consumer's own verifier. `PasswordPolicyContext` carries the tenant's effective settings, the account's
   user name and its email. The defaults are internal classes in `NCode.Identity.OpenId.Authentication`, in the folder
   `Passwords/`, registered by `AddPasswordServices()`, which `AddOpenIdAuthenticationLibrary` calls, so the composition
   root wires them ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)) and a host replaces any of
   them.
2. **Two tenant settings set the length.** Both apply to tenants and neither is discoverable
   ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)).
   - `password_min_length` is an `int` between 8 and 64 that defaults to 15, and merges with `Max`, so a narrower scope
     may only raise the floor. 15 is the requirement for a single-factor password, and 8 is the absolute floor.
   - `password_max_length` is an `int` between 64 and 4096 that defaults to 128, and merges with `Replace`. 64 is the
     minimum for the maximum.
3. **The length is counted in Unicode code points over the whole password.** `DefaultPasswordPolicy` counts the runes of
   the password (`ReadOnlySpan<char>.EnumerateRunes()`), so a surrogate pair is one character. It counts the code points
   of the password's Unicode Normalization Form C
   ([ADR-1238](1238-a-password-is-hashed-and-verified-in-its-nfc-form-on-every-path.md)), does not truncate the password and
   requires no character classes. A password with fewer characters than the minimum is `password_too_short`, and one
   with more than the maximum is `password_too_long`.
4. **The blocklists are a strategy collection.** `IPasswordBlocklist.ContainsAsync(PasswordPolicyContext,
   ReadOnlyMemory<char> password, CancellationToken)` says whether the whole password is on a list. The registrations
   use `TryAddEnumerable`, and the policy asks each blocklist in registration order after the length checks and refuses
   the password at the first one that lists it. Two ship:
   - `CommonPasswordBlocklist` holds the 100,000 most common passwords that the UK National Cyber Security Centre
     derived from Have I Been Pwned's Pwned Passwords, as SecLists publishes them
     (`Passwords/Common-Credentials/100k-most-used-passwords-NCSC.txt` at commit
     `1a7bb9127eca9e6ff2fc0301c597fe6e16a0cb56`). The bundled list keeps the entries of at least eight code points, as
     the minimum length is never lower and a shorter entry cannot matter, lower-cased and de-duplicated: 46,483 entries,
     about 450 KB. It is an embedded resource of the Authentication assembly, loaded once on first use into a frozen
     set, and a password matches when it equals an entry as a whole, ignoring case.
     `build/Update-CommonPasswords.ps1` reproduces the file from the pinned commit after it verifies the SHA-256 of the
     source. `THIRD-PARTY-NOTICES.md` at the repository root records the attribution and the licenses of the source, and
     the Authentication package contains it.
   - `AccountNamePasswordBlocklist` lists the account's own names: a password that equals the user name, the email or
     the local part of the email, ignoring case.
5. **No blocklist needs the network.** A breached-password service is not called by default. A deployment that wants
   one registers another `IPasswordBlocklist` that queries it, for example with a k-anonymity range request, and the
   policy asks it with the others.
6. **The result names the requirement, and its vocabulary is open.** `PasswordPolicyResult` is a sealed class:
   `Accepted`, `Rejected(error, errorDescription)`, `Error`, `ErrorDescription` and `IsRejected`, which is
   `[MemberNotNullWhen(true, ...)]`. `PasswordPolicyErrors` names `password_too_short`, `password_too_long` and
   `password_blocklisted`, and a replacement policy may report other codes
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). The description names the requirement, such as
   "The password must have at least 15 characters." The management API returns the description, and the change page
   maps the code to a message of its own ([ADR-1230](1230-an-end-user-changes-their-password-through-an-optional-seam-and-keeps-only-the-current-session.md)).
7. **Each entry point answers in its own form, and the policy runs before the store hashes the password.**
   - The management API's create and reset answer a refusal with a `400` problem that carries the description.
     `LocalAccountApiEndpointHandler` resolves the tenant's settings from the stored tenant document with
     `IEffectiveSettingsResolver`, so the tenant need not be materialized, and it answers `404` for a tenant that does
     not exist.
   - The self-service change judges the new password before it verifies the current one, and reports
     `InteractivePasswordChangeResult.Refused(code)` with the policy's error code.
     `LocalAccountInteractivePasswordChanger` does so.
   - A seeder asks `SeededLocalAccountValidator` before it adds the account, which throws an `InvalidOperationException`
     that names the kind of account, its user name and the description.

   A refused password is never hashed, and a store need not repeat the rules.

## Options considered

- **A breached-password service by default.** Rejected: it makes every password change depend on the network and on a
  third party's availability, and it needs a privacy review. A deployment adds one as a blocklist.
- **A length requirement only.** Rejected: NIST requires the blocklist, and a long password such as `passwordpassword`
  is among the most common.
- **Composition rules.** Rejected: NIST forbids them, as they lead end users to predictable substitutions.
- **A store's own validators.** Rejected: each store differs, the defaults of ASP.NET Core Identity impose composition
  rules, and a custom store would skip them.
- **A much larger list.** Rejected: the limit on failed attempts
  ([ADR-1237](1237-failed-password-attempts-are-limited-per-account-and-a-refusal-costs-what-a-wrong-password-costs.md)) throttles guessing, so a larger list is of
  little benefit, and the list is embedded in the package.
- **Per-client settings.** Rejected: an account belongs to its tenant, not to a client.

## Consequences

- A password is refused at one place whichever path sets it, with the reason, and a deployment tightens the length for a
  tenant with its settings or refuses more passwords with a blocklist.
- A password that an account holds is judged only when it is next set; no stored password is re-validated.
- The bundled list adds about 450 KB to the Authentication package.
- The ASP.NET Core Identity adapter stores a password through the user store without Identity's password validators, so
  the policy is the only judge of a password the server sets there too
  ([ADR-1238](1238-a-password-is-hashed-and-verified-in-its-nfc-form-on-every-path.md)).

## References

- [NIST SP 800-63B-4](https://pages.nist.gov/800-63-4/sp800-63b/authenticators/) §3.1.1.2 — the length, the blocklist
  and the guidance for a password.
- [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) — the composition root wires the defaults.
- [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md) — the domains of the length settings.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — the error codes are an open vocabulary.
- [ADR-1230](1230-an-end-user-changes-their-password-through-an-optional-seam-and-keeps-only-the-current-session.md) — the self-service change.
- [ADR-1237](1237-failed-password-attempts-are-limited-per-account-and-a-refusal-costs-what-a-wrong-password-costs.md) — the limit on failed attempts.
- Code: `NCode.Identity.OpenId.Authentication.Passwords.IPasswordPolicy`,
  `NCode.Identity.OpenId.Authentication.Passwords.DefaultPasswordPolicy`,
  `NCode.Identity.OpenId.Authentication.Passwords.CommonPasswordBlocklist`,
  `NCode.Identity.OpenId.Authentication.Passwords.AccountNamePasswordBlocklist`,
  `NCode.Identity.OpenId.Accounts.Management.LocalAccountApiEndpointHandler`,
  `NCode.Identity.OpenId.Accounts.Authentication.LocalAccountInteractivePasswordChanger`,
  `NCode.Identity.Server.SeededLocalAccountValidator`.
