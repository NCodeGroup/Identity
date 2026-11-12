# 1148. A scope selects security profiles that accumulate down the cascade as ceilings, defaults and requirements

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A security profile is a named baseline that a scope opts into. It carries ceiling settings that bind through the merge
lattice, default settings that a scope's own values override, and request-time requirements for the rules no setting can
express, such as a confidential client or an issuer-only assertion audience. A deployment adds a profile by registering
it, and the profile setting's domain is the registered names
([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)). A baseline that a platform,
team or tenant mandates must hold for every scope below it, whichever profile it is. Four forces shape the design.

- **A selection must accumulate.** A child that clears or replaces its parent's selection would keep the parent's
  ceilings, which are already merged into its settings, and silently lose the parent's requirements. A single name also
  cannot select two profiles, although profiles are layered by design: the FAPI 2.0 Message Signing profile builds on
  the FAPI 2.0 Security Profile.
- **Every scope must expand its selection.** If only tenant and client documents passed through the catalog, a profile
  selected higher up would switch its requirements on everywhere and inject none of its ceilings, so discovery would not
  advertise it and the PAR, PKCE, algorithm and lifetime ceilings would go unenforced. The effective-settings previews
  would show something other than what the server enforces.
- **A ceiling must bind.** A ceiling written on a setting that merges as a projection is overridden by a child
  (`Replace`) or ignored (`Keep`), and with several profiles the order of application would decide the outcome.
- **Some FAPI rules suit everyone.** The FAPI 2.0 rules that no ceiling can express fall into two groups: those that
  only a mandated profile should impose, and those that are simply correct for every client.

## Decision

**A scope selects security profiles through a list that accumulates down the cascade, and each selected profile
contributes ceilings, defaults and requirements that every scope's settings pass through the same expansion to receive.**

### Profiles and the catalog

A profile is a registered `ISecurityProfile` with a `ProfileName`, its `Requirements`, `ApplyCeilingSettings` and
`ApplyDefaultSettings`. The one registry is `ISecurityProfileCatalog`, implemented by `DefaultSecurityProfileCatalog`.
The built-in profiles are `fapi2` (`Fapi2SecurityProfile`),
`fapi1-advanced` ([ADR-1202](1202-fapi-1-0-advanced-is-a-registered-profile-and-only-a-request-objects-parameters-count.md)) and
`production` ([ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md)); their names are
in `OpenIdConstants.SecurityProfiles`.

### Selection accumulates

A scope selects profiles through the `security_profiles` setting, a `Union` list that defaults to empty. Selections
accumulate platform, team, tenant, then client, and a child never removes a parent's. A resource server selects none
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)).

### Profiles compose by conjunction

The catalog visits the selected profiles in ordinal name order and merges each profile's ceiling on its own.
Contradictory ceilings leave a set but empty list, which fails closed. A profile builds on another by setting
`security_profiles` in its own ceiling, and the catalog expands the selection to a fixpoint.

### Ceilings

`ISecurityProfileCeilingBuilder` accepts only a setting with a lattice merge (`SettingDescriptor.HasLatticeMerge`:
`Min`, `Max`, `And`, `Or`, `Intersect` or `Union`). A ceiling on a projection throws on expansion. Ceilings are not
filtered by scope, and a test expands every registered profile so that a bad ceiling fails the build.

### Defaults

`ISecurityProfileDefaultsBuilder` accepts only a setting with a non-lattice merge. `Expand` places the defaults beneath
the scope's own settings, and only where `ISettingScopeCollection.AppliesWithin(descriptor, scopeName)` holds for the
expanding scope. When several selected profiles default the same setting, the first name in ordinal order wins. At the
platform root a configured value beats a profile default, which beats the baseline.

A profile supplies defaults because a ceiling can exclude a protocol default: the `fapi2` signing ceilings exclude
`RS256`, the registration default of `id_token_signed_response_alg` and `introspection_signed_response_alg`, so without
a profile default every client under the profile would need an explicit choice and would fail at issuance. The platform
root passes its baseline, the descriptor defaults, the computed `*_supported` lists and the default-settings providers,
to `Expand` separately, because every setting with a descriptor default already carries a value there and a profile
default would otherwise never take effect.

### One expansion for every scope

The layered root, the persisted platform, team, tenant and client documents and the previews all pass through
`Expand(scopeSettings, scopeName)`, `Expand(scopeSettings, scopeName, baselineSettings)` or the document overload. A
scope name that is not registered throws `ArgumentException`.

### Requirements are an open typed set

The marker is `ISecurityProfileRequirement`. The phase seam is `ISecurityProfileRequirement<TContext>.EvaluateAsync`,
with the aliases `IAuthorizationRequestProfileRequirement`, `ITokenRequestProfileRequirement` and
`IClientAuthenticationProfileRequirement`. Each enforcement point calls
`catalog.EvaluateRequirementsAsync(settings, context, ct)`. A requirement shared by several selected profiles runs once,
and a failure names "a selected security profile" rather than one profile.

### The `fapi2` profile

Ceilings:

- Every signing list, including DPoP's, is `PS256`, `ES256` and `Ed25519`. `Ed25519` takes effect with the EdDSA
  package, and there is no polymorphic `EdDSA` ([ADR-1392](1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md)).
- `token_endpoint_auth_methods_supported` is `private_key_jwt`, `tls_client_auth` and `self_signed_tls_client_auth`.
- `response_types_supported` is `code`, and the PKCE methods are `S256`.
- `require_pkce`, `require_pushed_authorization_requests`, `access_token_sender_constraint_required`
  ([ADR-1158](1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md)) and
  `refresh_token_rotation_retry_required` are on.
- The authorization code and `clock_skew` are capped at 60 seconds, the PAR `request_uri` at 90 seconds, a client
  assertion at 5 minutes and a request object at 60 minutes with `nbf`.
- `redirect_uri_kinds_supported` is capped at `https`, `loopback` and `private_use`.

Defaults: the ID token, introspection and authorization response signatures are `PS256`, and
`refresh_token_rotation_policy` is `unbound`.

Requirements:

- `ConfidentialClientRequirement` answers `unauthorized_client` for a client whose method is `none`.
- `IssuerOnlyAssertionAudienceRequirement` accepts only the issuer as a client assertion's audience (FAPI 2.0
  §5.3.2.1).
- `IssuerOnlyRequestObjectAudienceRequirement` does the same for a request object.

### Rules that suit everyone apply to every client

What FAPI requires but is correct for any client is not gated on a profile:

- The authorization response is `303` (`AuthorizationResponseWriter`) and the login challenge is `302`, never `307`.
- `authorization_response_iss_parameter_supported` is a discoverable `Or` setting that defaults to `true` (RFC 9207).
- Client assertions and request objects are validated with the effective `clock_skew`.
- Authorization codes are single-use, and the `redirect_uri` match is exact.

## Options considered

- **One name that a stricter name replaces.** Rejected: a child's choice would drop the parent's requirements, and one
  name cannot select two layered profiles.
- **`Keep` as the cascade merge for the selection.** Rejected: a child's selection would never add to its parent's, and
  a client could not opt into a further profile.
- **Evaluating every profile name seen anywhere in the cascade.** Rejected: a name a child merely mentions would
  enforce a profile the effective selection does not contain.
- **Author-chosen ceiling settings.** Rejected: a profile author could write a ceiling that does not bind, and the
  order of profiles would decide the outcome.
- **Merge-kind metadata on the ceiling.** Rejected: the descriptor already knows whether its merge is a lattice, so
  separate metadata can only disagree with it.
- **Inferring the scope from the settings.** Rejected: the caller knows which scope it expands, and a guess would apply
  defaults to settings that do not apply there.
- **Scope-filtered ceilings.** Rejected: a ceiling on a setting that does not apply to a scope is harmless there, and
  filtering would let a ceiling disappear without a signal.
- **Defaulting only outside the ceiling.** Rejected: a profile default beneath the scope's own settings is simpler and
  cannot conflict with a ceiling that the same merge enforces.
- **Removing the descriptor default of every setting a profile defaults.** Rejected: the descriptor default is the
  protocol default and the value a platform that selects no profile reports, and each profile default would silently
  depend on it being absent.
- **Expanding the configuration alone and laying it over the descriptor defaults.** Rejected: a ceiling would replace a
  computed `*_supported` list instead of narrowing it, and merging instead would stop a configured lifetime from
  exceeding its descriptor default.
- **Closed requirement flags or `if (profile == "fapi2")`.** Rejected: a consumer or sibling package adds a profile
  by registering it, so the requirement set stays open
  ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
- **Chosen: a registered profile whose selection accumulates, expanded identically at every scope into ceilings,
  defaults and open typed requirements.**

## Consequences

- Any scope can mandate a profile, and the mandate holds for every scope below it.
- Discovery, enforcement and the previews agree, because they read the same expansion.
- A bad ceiling or a contradictory pair of profiles fails closed or throws at expansion, and the test that expands every
  registered profile catches an authoring mistake at build time.
- `Intersect` keeps the parent's order.
- A client that alone selects a profile takes its tenant's value of a tenant-only setting, because a profile default
  applies only where the setting applies within the client scope.
- Adding a profile, such as a FAPI 1.0 profile or an organizational baseline, is a registration; the enforcement points
  are profile-agnostic.

## References

- [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md): the profile names are the
  registered ones.
- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md): the registered client-authentication
  methods that the `fapi2` ceiling narrows.
- [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md): the scopes a setting applies within.
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): open vocabularies.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md):
  the merge lattice.
- RFC 9207, FAPI 2.0 Security Profile §5.2.2 and §5.3.2.1.
- Code: `NCode.Identity.OpenId.Settings.ISecurityProfile`, `ISecurityProfileCatalog`, `DefaultSecurityProfileCatalog`,
  `Fapi2SecurityProfile`, `ISecurityProfileCeilingBuilder`, `ISecurityProfileDefaultsBuilder`,
  `ISecurityProfileRequirement`, `ConfidentialClientRequirement`, `IssuerOnlyAssertionAudienceRequirement`,
  `IssuerOnlyRequestObjectAudienceRequirement`, `AuthorizationResponseWriter`.
