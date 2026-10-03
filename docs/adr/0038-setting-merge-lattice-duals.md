# 38. Setting merge is a policy of lattice duals — ceiling, floor, or override

- **Status:** Accepted
- **Date:** 2026-10-03
- **Deciders:** NCode Group

## Context

Server, tenant, and client settings merge into one effective collection that endpoint logic reads. Each
`SettingDescriptor<TValue>` carries an `OnMerge` function applied as `merge(parent, child)` down the
`server → tenant → child` cascade.

The merge functions were ad-hoc: a handful of `public static` helpers on an **internal** descriptor data source, so an
external author of a descriptor could not reuse them. More importantly, almost every scalar and boolean used
`Replace` (the child value wins), which **cannot express a restriction** — a more-specific scope could always loosen a
broader scope's policy. Boolean "meet/join" helpers (`And`/`Or`) existed but were used by **zero** descriptors, so in
practice:

- a client could set `require_pkce = false` even when the server required it;
- a client could set `allow_unsafe_token_response = true` even when the server forbade it;
- a client could mint a longer token lifetime than the tenant intended (`Replace` on `access_token_lifetime`).

Collection `*_supported` lists already merged with `Intersect` (a narrowing ceiling,
[ADR-0010](0010-supported-settings-unset-means-unrestricted.md)), but that was the only expressible restriction.

## Decision

**Setting merge is a first-class policy expressed through a public `SettingMerge` vocabulary of lattice dual pairs; a
descriptor's `OnMerge` is chosen to realize the intended policy for that setting's value polarity.**

The policy intents are **ceiling** (the parent caps the child), **floor** (the parent sets an un-loosenable minimum),
and **override** (the child wins, no restriction). Which primitive realizes a ceiling/floor depends on the setting's
value polarity, so the vocabulary ships **both halves of every pair** and the author selects the half whose polarity
makes the intent true:

| Domain | meet (narrowing) | join (widening) | projections |
| --- | --- | --- | --- |
| Ordered (`IComparable<T>`) | `Min` | `Max` | — |
| Boolean | `And` | `Or` | — |
| Collection | `Intersect` | `Union` | — |
| Value | — | — | `Keep` (parent wins) / `Replace` (child wins) |

`SettingMerge` lives in `NCode.Identity.Abstractions` (namespace `NCode.Identity.Settings`) so host-authored
descriptors reuse the same vocabulary; the default implementations that consume it stay internal
([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)).

The built-in catalog is mapped by polarity:

- **Lifetimes → ceiling (`Min`)**: `access_token_lifetime`, `id_token_lifetime`, `refresh_token_lifetime`,
  `authorization_code_lifetime`, `continue_authorization_lifetime`, `clock_skew`, `subject_max_age` — a child may only
  _shorten_. For a lifetime that carries a descriptor default, that value is simultaneously the default **and** the hard
  maximum; to permit a longer lifetime, raise the value at the server scope.
- **Requirements → floor (`Or`)**: `require_pkce`, `require_request_uri_registration`,
  `request_uri_require_strict_content_type`, `access_token_encryption_required`, `id_token_encryption_required`,
  `claims_supported_is_strict`, `refresh_token_rotation_enabled`, `federated_identity_require_verified`,
  `federated_identity_explicit_only` — once a parent requires it, a child cannot un-require it.
- **Permissions / capability toggles → ceiling (`And`)**: `allow_loopback_redirect`,
  `allow_plain_code_challenge_method`, `allow_unsafe_token_response`, `request_parameter_supported`,
  `request_uri_parameter_supported`, `claims_parameter_supported` — once a parent forbids/disables it, a child cannot
  re-enable it.
- **Capability lists → ceiling (`Intersect`)**: unchanged for the `*_values_supported` / `*_supported` families;
  `subject_types_supported` is corrected from `Replace` to `Intersect` to match the family; `allowed_identity_providers`
  becomes `Intersect` with **no default** (unset = unrestricted per ADR-0010).
- **Identity → `Keep`**: `tenant_issuer` — the tenant owns its issuer, so a child scope cannot override it.
- **Override (`Replace`)**: configuration values with no restriction semantics (claim-source names, schemes, URIs,
  `access_token_type`, `redirect_uris`, `send_id_claims_in_access_token`, …).

## Options considered

- **Keep `Replace` everywhere and enforce policy in endpoint code.** Rejected: scatters policy across handlers, has no
  single source of truth, and lets misconfiguration silently bypass a server's intent.
- **Name the functions by intent (`Ceiling`/`Floor`).** Rejected: the intent→primitive mapping depends on polarity
  (a lifetime ceiling is `Min`, a minimum-key-length ceiling would be `Max`), so a single generic `Ceiling` cannot
  exist; naming by operation plus documenting polarity is unambiguous.
- **Model a separate "default" and "maximum" per scalar.** Rejected as premature: it enlarges the `Setting` model; the
  root-value-as-ceiling model is simpler and sufficient.

## Consequences

- A more-specific scope can no longer loosen a broader scope's security-relevant setting. Because settings are
  admin-authored (through the management API), this is defense-in-depth against **misconfiguration**, not an
  end-user-controlled vector.
- Lifetime ceilings couple the default and the maximum: raising a tenant/client lifetime requires raising the server
  value (which also raises the effective default for scopes that set nothing).
- `allowed_identity_providers` is now **fail-closed**: unset imposes no restriction, but an explicit list — or one
  narrowed to empty by intersection — denies every IdP. Its consumer reads it with "restrict only if present".
- `subject_max_age` remains read via `GetValue` with no default; an unset value throws. This predates this decision and
  is tracked as a defaults-hardening follow-up.
- Host-authored descriptors use the same `SettingMerge` vocabulary, so extensions participate in the ceiling/floor
  model.

## References

- Code: `NCode.Identity.Settings.SettingMerge`, `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.Settings.DefaultClientSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.Subject.DefaultValidateSubjectAuthenticationHandler`.
- Prior art: [ADR-0010](0010-supported-settings-unset-means-unrestricted.md),
  [ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md).
