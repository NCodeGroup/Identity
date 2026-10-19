# 148. Security profiles accumulate down the settings cascade

- **Status:** Accepted (amended by [ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md): a resource server
  selects no security profile, and `security_profiles` is rejected on a resource-server document)
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

A security profile ([ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md),
[ADR-0109](0109-fapi-behavioral-rules-and-discovery-validation-details.md)) is a named baseline that a scope opts into:
ceiling settings injected into the merge lattice, and request-time requirements for the rules no ceiling can express (a
confidential client, a sender-constrained access token, an issuer-only assertion audience). A deployment adds a profile
by registering an `ISecurityProfile`, and the profile setting's domain is the registered names
([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)). A baseline that a server or tenant mandates must
hold for every scope below it, whichever profile it is. Three things kept that from holding.

- **The selection was one name, merged by a rule that knew one profile.** A child replaced its parent's selection unless
  the parent's was `fapi2`. The parent's ceilings had already been injected at the parent, so a client that cleared or
  replaced a tenant's profile kept that profile's ceilings but silently lost its requirements; a client that selected
  `fapi2` under a tenant with another profile dropped that profile's requirements the same way. A single name also
  cannot select two profiles, although profiles are layered by design — the FAPI 2.0 Message Signing profile builds on
  the FAPI 2.0 Security Profile.
- **Not every scope expanded its selection.** Tenant and client documents passed through the catalog when they were
  loaded, but the server's configured settings and its persisted document did not. A profile selected at the server
  switched its requirements on everywhere and injected none of its ceilings, so discovery did not advertise it, and the
  PAR, PKCE, algorithm, and lifetime ceilings were not enforced. The management API's effective-settings previews did
  not expand either, so they did not show what the server enforces.
- **A ceiling could be one that does not bind.** A profile wrote its ceiling into a plain setting collection. A ceiling on
  a setting that merges as a projection is overridden by a child (`Replace`) or ignored (`Keep`), and with several
  profiles the order in which they were applied would decide the outcome.

## Decision

**A scope selects a set of security profiles; the selections accumulate down the `server → tenant → client` cascade;
every selected profile applies; and a profile's ceiling may only use settings whose merge binds every scope below the one
that selects it.**

1. **`security_profiles` is a list merged with `Union`.** It is the join of the lattice the cascade already uses
   ([ADR-0038](0038-setting-merge-lattice-duals.md)), so a child can add a profile but never remove one a parent
   selects: the guarantee follows from the merge's algebra, as it does for every other floor, and no profile name
   appears in the merge. The default is the empty list, which selects none; every value names a registered profile, and
   when none is registered the list must be empty.
2. **Every selected profile applies, and profiles compose by conjunction.** The catalog visits the selected profiles in
   the ordinal order of their names. `Expand` merges into a scope's settings the ceiling of each profile the scope
   declares — each ceiling built in its own collection and merged on its own, so one profile's value never replaces
   another's. `EvaluateRequirementsAsync` runs the requirements of every profile in the effective set; a requirement
   instance that several profiles share runs once. Selecting several profiles is at least as strict as each. Profiles
   whose ceilings contradict leave nothing permitted on that dimension — a set but empty list permits nothing; only an
   absent one is unrestricted ([ADR-0010](0010-supported-settings-unset-means-unrestricted.md)) — which fails closed.
3. **A profile builds on another by selecting it.** A profile's ceiling may set `security_profiles`, and expansion
   repeats until every profile the scope carries has contributed its ceiling, so selecting a layered profile applies
   the profile it builds on, ceilings and requirements both. Inclusion only adds constraints; no profile stands in for
   another.
4. **A ceiling binds by construction.** A profile contributes its ceiling through `ISecurityProfileCeilingBuilder`, which
   writes through the registered descriptor and accepts only a setting that merges with a lattice operation of
   `SettingMerge` — `Min`, `Max`, `And`, `Or`, `Intersect`, or `Union` — as `SettingDescriptor.HasLatticeMerge` reports.
   Such a merge is commutative, associative, and idempotent: a value contributed at a scope bounds every scope below it,
   and the ceilings of several profiles reach the same bound in any order. A ceiling on a projection or another function
   throws when the profile is expanded, so a scope that selects such a profile fails closed. Whether a bound tightens
   the server is the profile's choice of setting; that the bound holds is enforced. A composed-server test expands every
   registered profile, so a built-in profile cannot ship with a ceiling that does not bind.
5. **Every scope's settings pass through the catalog.** The root settings — descriptor defaults, default-settings
   providers, and `OpenId:Server:Settings` — are expanded once they are layered; the persisted server, tenant, and
   client documents are expanded when they are loaded and refreshed; and the management API's tenant, client, and
   resource-server effective-settings previews expand the tenant and client documents as the runtime does. A
   resource-server document is merged unexpanded, as the runtime merges it.
6. **Stored documents are translated.** The `TranslateSecurityProfileToSecurityProfiles` migration rewrites every settings
   table: a non-empty single name becomes a one-element `security_profiles`, an empty or `null` one — which selected none
   — is removed, and a value of another type is left to fail closed
   ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)). Its `Down` restores what the single name can express
   and leaves a list of several names to fail closed.

## Options considered

- **A profile declares which profiles it is at least as strict as, and a child may replace its parent's profile only
  with a stricter one.** Rejected. Replacement drops the parent profile's own requirements, so the guarantee rests on
  every author's strictness claim staying true as profiles evolve; a partial order leaves incomparable selections with
  no defined result — refusing them burdens the operator, and keeping the parent's silently ignores the child's
  selection; and it cannot select two complementary profiles. When profiles accumulate, a stricter profile loses nothing
  by also running a weaker one's rules.
- **The parent's selection wins (`Keep`).** Rejected: a scope could not add a profile beneath one its parent selects, so
  a layered profile could not be selected where its base is mandated.
- **Keep one name and evaluate the requirements of every name seen in the cascade.** Rejected: the effective settings
  would no longer say which profiles apply, so the catalog, discovery, and the management previews would each need the
  cascade's history.
- **Leave the choice of ceiling settings to each profile author.** Rejected: one projection-merged ceiling makes that
  ceiling loosenable or inert without any signal — the failure ADR-0139 and ADR-0140 remove for configuration.
- **Declare each descriptor's merge kind as separate metadata.** Rejected: it restates the merge function and can drift
  from it; reading the kind from the `SettingMerge` function the descriptor uses cannot disagree with what it does.

## Consequences

- A server or tenant can mandate any registered profile; a scope below can add profiles but cannot clear or replace one.
  Discovery, request-time enforcement, and the management previews agree on the profiles in force at every scope.
- A profile selected in the server's configuration or its persisted settings tightens discovery and every tenant and
  client, not only the requirements.
- A profile whose ceiling names a setting that does not merge with a lattice operation fails when a scope selects it,
  and the composed-server test fails first for a registered one.
- `Intersect` keeps its left operand's order, so a bounded list's preference order is the parent scope's (the root's,
  for the algorithm lists). The order in which profiles apply never changes which values are permitted, and changes the
  order only for a list that no scope above sets.
- A requirement's error description names "a selected security profile".
- Follow-ups: a resource-server document accepts `security_profiles` but neither expands nor evaluates it, which is part
  of making the setting vocabulary scope-aware (backlog `F60`); and a single targeted resource server governs
  access-token encryption with its own effective settings, without combining the client's (backlog `F61`).
- **Breaking:** the setting, its name and key constants, `ISecurityProfile.ApplyCeilingSettings`, and the abstract
  `SettingDescriptor.HasLatticeMerge` change the public surface; a deployment that stores settings documents in its own
  database applies the same rewrite there.

## References

- [ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md) — security profiles as ceiling injection
  (amended: the selection and its merge).
- [ADR-0109](0109-fapi-behavioral-rules-and-discovery-validation-details.md) — the profile registry and requirement
  objects (amended: every selected profile's requirements run).
- [ADR-0140](0140-setting-values-are-validated-against-their-domain.md) — the profile domain (amended: a list of
  registered names).
- [ADR-0038](0038-setting-merge-lattice-duals.md) — the merge lattice; [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) —
  an absent list is unrestricted, an empty one permits nothing; [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) —
  renaming a setting ships with a data migration; [ADR-0075](0075-runtime-consumption-of-resource-server-secrets.md) —
  resource-server token encryption.
- OpenID Foundation FAPI 2.0 Security Profile and FAPI 2.0 Message Signing.
- Code: `NCode.Identity.OpenId.Settings.ISecurityProfile`, `ISecurityProfileCeilingBuilder`, `ISecurityProfileCatalog`,
  `DefaultSecurityProfileCatalog`, `SecurityProfileCeilingBuilder`, `DefaultSettingDescriptorDataSource`,
  `RootSettingsCollectionDataSource`, `NCode.Identity.OpenId.Servers.DefaultOpenIdServerFactory`,
  `NCode.Identity.Settings.SettingDescriptor.HasLatticeMerge`, `NCode.Identity.Settings.SettingMerge`, and the
  `TranslateSecurityProfileToSecurityProfiles` migration.
