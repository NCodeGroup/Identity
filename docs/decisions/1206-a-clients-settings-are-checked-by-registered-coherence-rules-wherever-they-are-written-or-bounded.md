# 1206. A client's settings are checked by registered coherence rules wherever they are written or bounded

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The domain of a setting is checked on its own
([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)), so it cannot relate two
settings. A client's registration depends on such relations: an algorithm that the client registers for a JWT must be in
the effective list that permits it ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md)), a
redirect URI must be of a kind that `redirect_uri_kinds_supported` lists
([ADR-1203](1203-redirect-uris-are-matched-exactly-as-written-and-their-kinds-are-narrowed-by-settings.md)), and the authentication method must be one that
`token_endpoint_auth_methods_supported` lists
([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)). The lists come from the settings cascade
and from the security profiles that a scope selects
([ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md)),
so the client's own document does not carry them.

A check in each place that stores a client drifts: each place checks a different subset, and a settings change that
re-checks only some relations leaves clients that fail when they next request authorization, such as a write that
narrows `redirect_uri_kinds_supported` or selects `fapi2` while a client's redirect URI is excluded (issues
[#231](https://github.com/NCodeGroup/identity/issues/231) and [#246](https://github.com/NCodeGroup/identity/issues/246)).

A client's own value can also be bounded without a relation between two settings. A meet merge (`Min`, `And`,
`Intersect`) never yields a value above either operand, so a ceiling of the tenant, the server or a selected profile can
lower what a client registers: a client that registers a one-hour `access_token_lifetime` receives tokens that live five
minutes, without a word (issue [#262](https://github.com/NCodeGroup/identity/issues/262)). A join (`Max`, `Or`, `Union`)
never yields a value below either operand, so a floor can only raise a value.

## Decision

**A rule checks one relation over the settings of a client, one validator runs every registered rule, and every place
that stores a client, and every change that could make a stored client incoherent, runs the validator. A value that a
client registers in a setting that merges with a meet must survive into its effective settings; a value that a ceiling
lowers is a violation.**

1. **A rule checks one relation.** `IClientCoherenceRule` (`NCode.Identity.OpenId.Authentication.Abstractions`,
   namespace `NCode.Identity.OpenId.Authentication.Clients`) has `SettingNames`, every setting whose effective value the
   rule reads, and `FindViolations(ClientCoherenceContext)`, which returns each broken relation as a
   `ClientCoherenceViolation(SettingName, Value, GoverningSettingName)`: the client setting, the value, and the
   effective setting that does not permit it or that requires it. `ClientCoherenceContext` carries
   `RegisteredSettings`, the client's own document as written, and `EffectiveSettings`, the client resolved onto its
   tenant. A value that a ceiling drops is visible only in the registered settings.

   A violation describes itself through `Description`, which every caller reports. The default is `The '<SettingName>'
   value '<Value>' is not permitted by the effective '<GoverningSettingName>'.`; a rule that checks another relation
   sets its own. `ClientCoherenceViolation.Missing(...)` reports a required value that is absent. Rules are an open
   collection ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)) registered with `TryAddEnumerable`,
   so a package adds a rule with one registration.
2. **Eleven built-in rules** live in `NCode.Identity.OpenId.Authentication.Clients.Coherence`; none names a profile:
   - `ResponseAlgorithmCoherenceRule`: the algorithms registered for the JWTs addressed to the client must be in the
     effective lists. An unset list is unrestricted.
   - `RedirectUriKindCoherenceRule`: `redirect_uris` and `post_logout_redirect_uris` through
     `IRedirectUriKindCatalog.IsPermitted`.
   - `TokenEndpointAuthMethodCoherenceRule`: `token_endpoint_auth_method` against the supported methods.
   - `CertificateBoundAccessTokenCoherenceRule` and `DPoPBoundAccessTokenCoherenceRule`: a client that requires a
     sender-constrained access token needs the effective support for it
     ([ADR-1097](1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md)).
   - `RegisteredValueCoherenceRule`: a registered value survives into the effective settings (decision 3).
   - `IdTokenEncryptionCoherenceRule`: a client that requires ID token encryption has an
     `id_token_encrypted_response_alg`.
   - `BackchannelTokenDeliveryCoherenceRule`: a delivery mode other than `poll` needs a `client_notification_endpoint`,
     and a client permitted the CIBA grant uses a mode that `backchannel_token_delivery_modes_supported` lists.
   - `RefreshTokenRotationRetryCoherenceRule`: a client whose refresh tokens can rotate and whose effective
     `refresh_token_rotation_retry_required` is `true` has a positive `refresh_token_reuse_interval`
     ([ADR-1216](1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md),
     [ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)); the FAPI 2.0 profile
     contributes the requirement as data.
   - `LogoutChannelCoherenceRule`
     ([ADR-1088](1088-front-channel-logout-and-session-management-are-available-but-disabled-by-default.md)) and `ClientSecretKeyCoherenceRule`
     ([ADR-1391](1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).
3. **A value that a ceiling lowers is a violation.** `SettingDescriptor.HasMeetMerge` is `true` for `Min`, `And` and
   `Intersect`, computed by `SettingMerge.IsMeetOperation`. For each registered setting with `HasMeetMerge` that applies
   within the client scope, `RegisteredValueCoherenceRule` compares the registered and the effective value, naming the
   setting as both: a registered list element missing from the effective list, a registered `true` that is effectively
   `false`, or any other value that differs from its effective value is a violation, described with invariant-culture
   formatting (`The 'access_token_lifetime' value '01:00:00' exceeds the effective 'access_token_lifetime' value
   '00:05:00'.`). A join never reports: refusing it would block a tenant from raising a floor. The rule takes an
   `ISettingScopeCollectionProvider`, so `SettingNames` lists only meets that apply within the client scope, and a
   package's own setting is covered by registering its descriptor.
4. **One validator runs the rules and detects a change.** `IClientCoherenceValidator.Validate(tenantSettings,
   clientSettings)` builds both views through `IEffectiveSettingsResolver`, so no caller resolves them, and throws
   `JsonException` for an unreadable document. `ReadsChangedSetting(previous, current)` compares the effective values
   of every rule's `SettingNames`: presence counts, string lists compare as ordinal sets, anything else uses
   `object.Equals`. The gate is generic, so it cannot omit a setting that a rule reads.
5. **Every place runs the validator and reports through `Description`:**
   - Dynamic registration refuses the client before storing it, with `invalid_redirect_uri` for a `redirect_uris`
     violation and `invalid_client_metadata` otherwise; `ValidateCoherence` is its only relation check.
   - A management client create or settings update answers a validation problem whose `SettingError`s are keyed by the
     client setting.
   - `SeededClientValidator` (`NCode.Identity.Server`) checks the bootstrap administrator, developer and conformance
     clients against the setting domains and the committed platform, team and tenant settings, stops the host with an
     `InvalidOperationException` on failure, and leaves an existing client as it is.
   - A platform, team or tenant settings write reports only the violations its change introduces. The `SettingError` is
     keyed by `GoverningSettingName` and says "The change would make the client '<id>' incoherent: …". Each level's walk
     is gated by `ReadsChangedSetting`, and a team write covers the tenants of every team beneath it
     (`DefaultTeamClientCoherenceChecker`). `DefaultTenantClientCoherenceChecker` matches a violation that predates the
     write by setting, value and governing setting, not by description, and pages untracked, secret-free
     `GetSettingsPageAsync` projections under an explicit tenant scope.
   - A change that is not a write is reported by the reconciliation's `ClientCoherenceReportHandler`
     ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)); an unreadable stored document is skipped and logged
     ([ADR-1245](1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md)).
6. **Order and bound.** Domain validation
   ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md)) runs before the rules;
   registration and management writes check key material before the rules, while a seeder imports keys after them. A
   validation problem lists at most ten errors per setting plus one error that counts the rest.

## Options considered

- **A check per caller.** Rejected: callers drift, each checking a different subset.
- **A gate method per rule.** Rejected: a gate can forget a setting its rule reads; the declared `SettingNames` drive
  one gate.
- **Rules over raw documents.** Rejected: the bounding lists come from the cascade and the profiles, which only the
  effective settings carry.
- **A violation-kind enum.** Rejected: a closed set of kinds makes a package edit the enum; a violation carries its own
  `Description`.
- **Callers resolve the views.** Rejected: callers drift; the validator builds both views.
- **A warning for a lowered value.** Rejected: a write answers success or a validation problem, so a warning has no
  channel and the value is still clamped.
- **Reporting a `Max` raise.** Rejected: a join only raises a value to a floor.
- **Order from `IComparable`.** Rejected: whether a merge lowers a value is a fact of the merge, not the value type.
- **A merge-kind enum.** Rejected: one fact on the descriptor suffices.
- **Registered rules behind one validator.** Chosen: every relation is checked once for every caller and stays open.

## Consequences

- A rule is one registration. A client cannot register above its ceilings; to give one client more, raise the tenant's
  value and lower the others.
- A write that lowers a ceiling below a client's registration is refused unless the ceiling already lowers it.
- A client on `none` or `client_secret_basic` blocks a change that selects `fapi2` or `fapi1-advanced`; a FAPI client
  cannot register `authorization_code_lifetime` or `clock_skew` above 60 seconds
  ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
- A seeder refuses a client its tenant's settings contradict, so the host does not start with it.
- Unit tests cover each rule, the change gate, each caller's report and the walks.

## References

- Issues [#231](https://github.com/NCodeGroup/identity/issues/231),
  [#246](https://github.com/NCodeGroup/identity/issues/246) and
  [#262](https://github.com/NCodeGroup/identity/issues/262).
- Code: `NCode.Identity.OpenId.Authentication.Clients.IClientCoherenceRule`,
  `NCode.Identity.OpenId.Authentication.Clients.Coherence.RegisteredValueCoherenceRule`,
  `NCode.Identity.Settings.SettingDescriptor`, `NCode.Identity.Server.SeededClientValidator`.
