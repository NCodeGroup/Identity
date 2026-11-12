# 1010. Settings merge as ceilings and floors, unset lists restrict nothing, and a secure baseline supplies defaults

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode Group

## Context

Settings resolve down the cascade platform, team, tenant, and client or resource server
([ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)). Each scope may
only make the effective value safer or narrower than the scope above it, or override it where the setting has no
restriction semantics. A cascade that mixed these directions per setting would let a tenant or client loosen a
security setting that the operator set above it.

Three questions follow from that model:

- **How does a child scope combine with its parent?** A lifetime, a toggle, a capability list, a requirement and a
  minimum length each have a different safe direction. A descriptor must state its direction once, in a vocabulary
  that every package shares.
- **What does a capability list mean when nobody set it?** If the root fixes a list of supported grant types as a
  literal, every narrower scope is clamped to that literal: an extension that registers a new grant handler or a
  custom scope can never be used, because intersection only narrows. The literal conflates what the server is
  capable of, what the operator's policy allows, and what a child scope narrows to.
- **What does a host that configures nothing get?** A server that publishes nothing must still advertise and accept
  only modern, safe values, and discovery and enforcement must agree on them.

## Decision

Each setting merges by the polarity of its value, an unset capability list restricts nothing, and the platform root
is a secure, replaceable baseline.

### Merge vocabulary

`SettingDescriptor<TValue>.OnMerge` runs as `merge(parent, child)`. The public `SettingMerge`
(`NCode.Identity.Settings.Abstractions`, namespace `NCode.Identity.Settings`) ships the pairs and projections that
every package's descriptors use:

| Merge                | Meaning                                                                       |
| -------------------- | ----------------------------------------------------------------------------- |
| `Min` / `Max`        | the smaller or the larger of the two values                                   |
| `And` / `Or`         | both toggles must hold, or either one sets the requirement                    |
| `Intersect` / `Union`| the values both scopes list, or the values either lists                       |
| `Keep`               | the parent wins                                                               |
| `Replace`            | the child wins                                                                |

A descriptor chooses the merge that realizes a ceiling, a floor or an override. No built-in descriptor uses `Keep`.

### Ceilings

A ceiling is a limit that a narrower scope can lower and never raise.

- Lifetimes, `clock_skew`, `subject_max_age`, timeouts and size caps merge as `Min`. A lifetime's default is also its
  maximum, so raising one scope's lifetime means raising the platform's.
- Toggles such as `allow_loopback_redirect`, `request_parameter_supported` and `claims_parameter_supported` merge as
  `And`.
- Capability lists (the `*_supported` settings, `redirect_uri_kinds_supported` and `allowed_identity_providers`)
  merge as `Intersect`.

### Floors

A floor is a requirement that a narrower scope can raise and never lower.

- Requirements such as `require_pkce`, `require_request_uri_registration`, `*_encryption_required`,
  `claims_supported_is_strict` and the `federated_identity_*` requirements merge as `Or`.
- Minimums such as `user_code_length`, polling intervals and `password_min_length` merge as `Max`.
- `security_profiles` is the one `Union` list
  ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
- Values without restriction semantics merge as `Replace`.

A narrower scope therefore never loosens a broader scope's security setting.

### Absence restricts nothing

- An unset list imposes no restriction. Enforcement restricts only a present value:
  `if (settings.TryGetValue(key, out var allowed) && !allowed.Contains(item)) reject;`.
- A set list is an explicit ceiling that narrows by `Intersect`, and a set but empty list permits nothing.
- The capability itself is enforced by the registries, not by the literal: an unknown `grant_type` is rejected by
  grant-handler lookup and an unknown algorithm by the algorithm providers. A `*_supported` list is therefore
  policy, and a package enables an extension grant or a custom scope by registering it.
- The exception is an algorithm list that decides what the server accepts from another party or protects a token
  with. `GetPermittedAlgorithms` treats an unset list as permitting nothing, because the root always supplies those
  lists ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).
- A value-type setting without a default, such as `subject_max_age`, stays absent. `SettingDescriptor<TValue>.HasDefault`
  tracks a configured default, and `GetValueOrNull<TValue>() where TValue : struct` reads the setting, so an unset
  value is never materialized as `default(TValue)`.

### The secure baseline

The platform root, `RootSettingsCollectionDataSource`, is a baseline of descriptor defaults, algorithm lists
computed from the registered algorithms, and the contributions of the replaceable `IDefaultSettingsProvider`
implementations (`DefaultSettingsProvider`). `OpenId:Platform:Settings` is layered above it: a configured value
replaces the baseline's value.

The baseline is secure:

- `grant_types_supported` is `authorization_code`, `refresh_token`, device code and CIBA.
- `response_types_supported` is `code`.
- `code_challenge_methods_supported` is `S256`.
- `subject_types_supported` is `public` and `pairwise`.
- It also sets the delivery modes, prompt values, response modes and `claims_supported`.

Signing lists for tokens that cross a trust boundary advertise only asymmetric algorithms. The request-object list
and `token_endpoint_auth_signing_alg_values_supported` keep the symmetric ones that `client_secret_jwt` needs, and
no list offers `none`, `dir` or `RSA1_5`.

Discovery and enforcement read the same settings, so widening a setting advertises and accepts it, and discovery
omits settings that are unset.

## Options considered

- **`Union` for capability lists.** Rejected: a client cannot add a grant type the server has no handler for, and a
  union breaks the invariant that the server advertises the maximum.
- **`Replace` everywhere, with the checks in the handlers.** Rejected: it moves the ceiling out of the cascade and
  lets a narrower scope loosen a broader one.
- **Intent-named merges, `Ceiling` and `Floor`.** Rejected: the set operations name what the code does and cover
  lists, toggles and numbers alike.
- **A default and a maximum for each scalar.** Rejected: a lifetime's default is also its maximum, so one value
  states both.
- **Policy literals as descriptor defaults.** Rejected: they freeze the ceiling, so an extension is dead on arrival.
  The policy lives in a replaceable baseline instead.
- **Chosen: merges by polarity, absence as no restriction, and a secure replaceable baseline.** Operator control
  stays at the scope that sets a ceiling, and an extension needs only a registration.

## Consequences

- A narrower scope never loosens a broader scope's security setting, and raising one scope's lifetime means raising
  the platform's.
- Extensions, custom scopes and grants are not blocked by library literals. A host opts into a capability by
  registering it and, optionally, listing it.
- When neither the baseline nor any scope sets a capability list, that dimension is permissive. The baseline keeps
  the out-of-the-box server safe and standards-compliant.
- Enforcement reads use `TryGetValue`: `GetValue` throws on an unset, default-less setting.
- A smell: a descriptor whose merge does not match its polarity, such as a security requirement merged with
  `Replace`, or a policy literal in a descriptor default.

## References

- `NCode.Identity.Settings.Abstractions`: `SettingMerge`, `SettingDescriptor<TValue>`.
- `RootSettingsCollectionDataSource`, `IDefaultSettingsProvider`, `DefaultSettingsProvider`.
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md): the one `Union` list.
- [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md): algorithm lists that decide
  what the server accepts.
- [ADR-1411](1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md): the cascade's
  scopes.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700): OAuth 2.0 Security Best Current Practice.
