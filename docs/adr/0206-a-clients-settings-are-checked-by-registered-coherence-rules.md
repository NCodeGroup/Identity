# 206. A client's settings are checked by registered coherence rules

- **Status:** Accepted (amended in place: a rule reads the client's registered and effective settings, a violation
  describes itself, every configuration seeder runs the validator, and dynamic registration keeps no relation check of
  its own, and by
  [ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md): a change of the server's
  settings that is not a write is reported; a client whose refresh tokens rotate where a retry is required has a reuse
  interval)
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

The domain of a setting is checked on its own
([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)), so it cannot relate two settings. A client's
registration depends on such relations: an algorithm that the client registers for a JWT must be in the effective list
that permits it ([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)), a redirect URI must
be of a kind that `redirect_uri_kinds_supported` lists ([ADR-0203](0203-redirect-uris-are-matched-exactly.md)), and the
method that the client authenticates with must be one that `token_endpoint_auth_methods_supported` lists
([ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md)). The lists come from the settings cascade
and from the security profiles that a scope selects
([ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md)), so the client's own document does not
carry them.

Each place that stores a client checked a different subset of those relations. Dynamic registration, the management API,
and the conformance seed each checked some of the response algorithms, the redirect URI kinds, and the
client-authentication method, and none checked all of them. A change to a tenant's or the server's settings
re-checked only the response algorithms of the clients that it affected. A review (backlog `R40` and `R55`) found the
consequence: a management write that narrowed `redirect_uri_kinds_supported`, or that selected `fapi2` or
`fapi1-advanced`, was accepted, and each client whose redirect URI the change excluded failed when it next requested
authorization.

## Decision

1. **A rule checks one relation over the settings of a client.** `IClientCoherenceRule`
   (`NCode.Identity.OpenId.Authentication.Abstractions`, namespace `NCode.Identity.OpenId.Authentication.Clients`) has
   two members:
   - `SettingNames` lists every setting whose effective value the rule reads.
   - `FindViolations(ClientCoherenceContext context)` returns each relation that the settings of the client break, as a
     `ClientCoherenceViolation(SettingName, Value, GoverningSettingName)`: the client setting that registers the value
     or that is required, the value, and the effective setting that does not permit the value or that requires it.

   `ClientCoherenceContext` carries two views of the client. `RegisteredSettings` is the client's own settings document
   as written, without its tenant's settings and without the ceilings or defaults of the security profiles it selects.
   `EffectiveSettings` is the client's settings as they resolve onto its tenant's. A value that the client lists and
   that a ceiling drops is visible only in the registered settings.

   A violation describes itself through `Description`, which names the settings and the values involved. Its default is
   `The '<SettingName>' value '<Value>' is not permitted by the effective '<GoverningSettingName>'.`, and a rule that
   checks another relation sets its own. `ClientCoherenceViolation.Missing(settingName, governingSettingName,
   governingValue)` reports a required value that is absent: `Value` is empty and the description is
   `The effective '<governingSettingName>' value '<governingValue>' requires a '<settingName>' value.`. The governing
   setting is not always a ceiling: for a missing value it is the effective setting that requires the companion. Every
   caller reports `Description`, so a rule describes its own relation and the vocabulary of relations stays open.

   Rules are an open strategy collection ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). They are
   registered with `TryAddEnumerable`, which keeps one registration per implementation type, so each rule is its own
   class and a package adds a rule with one registration.
2. **Nine built-in rules** live in `NCode.Identity.OpenId.Authentication`, in the namespace
   `NCode.Identity.OpenId.Authentication.Clients.Coherence`:
   - `ResponseAlgorithmCoherenceRule` checks the algorithms that a client registers for the JWTs addressed to it: its ID
     tokens, UserInfo responses, introspection responses, and authorization responses. A registered signing algorithm
     must be in the effective signing list, and a registered key-management algorithm and its content encryption,
     `A128CBC-HS256` unless registered, must be in the effective encryption lists. An unset list is unrestricted.
   - `RedirectUriKindCoherenceRule` checks each URI of `redirect_uris` and of `post_logout_redirect_uris` against
     `redirect_uri_kinds_supported` through `IRedirectUriKindCatalog.IsPermitted`.
   - `TokenEndpointAuthMethodCoherenceRule` checks `token_endpoint_auth_method` against
     `token_endpoint_auth_methods_supported`. An unset list permits every method.
   - `CertificateBoundAccessTokenCoherenceRule` checks `require_tls_client_certificate_bound_access_tokens` against
     `tls_client_certificate_bound_access_tokens`. A client that requires certificate-bound access tokens while its
     effective support is `false` is incoherent, so a tenant or server write that turns the support off is refused while
     a client requires binding ([ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)).
   - `DPoPBoundAccessTokenCoherenceRule` checks `require_dpop_bound_access_tokens` against
     `dpop_signing_alg_values_supported`. A server advertises DPoP support through that list, so a client that requires
     DPoP-bound access tokens while the effective list is set and empty could not present an acceptable proof
     ([ADR-0097](0097-sender-constrained-access-tokens-dpop-and-mtls.md)). An unset list is unrestricted.
   - `RegisteredValueCoherenceRule` checks that a value which the client's own document registers survives into its
     effective settings. For every registered setting that merges with a lattice operation, a string-list value that is
     not in the effective list, and a registered `true` whose effective value is `false`, is a violation named by the
     setting itself. `Union` and `Or` merges cannot drop a value, so only `Intersect` and `And` ceilings report. The
     rule reads the registered settings, because a value that a ceiling dropped is absent from the effective list. It
     is generic over the registered setting descriptors, so a package's own list setting is covered by registering its
     descriptor. A `Min` or `Max` clamp is not covered (backlog `R71`).
   - `IdTokenEncryptionCoherenceRule` checks that a client whose effective `id_token_encryption_required` is `true` has
     a non-empty effective `id_token_encrypted_response_alg`, and reports the missing algorithm otherwise
     ([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).
   - `BackchannelTokenDeliveryCoherenceRule` checks the delivery of a client's backchannel authentication tokens. The
     effective `backchannel_token_delivery_mode` is `poll` when it is unset or empty. A mode other than `poll` needs a
     non-empty `client_notification_endpoint`, because the server notifies that endpoint (CIBA Core §4), whether or not
     the client is permitted the CIBA grant. When the client is permitted the CIBA grant, that is, its effective
     `grant_types_supported` is unset or lists `urn:openid:params:grant-type:ciba`, and
     `backchannel_token_delivery_modes_supported` is set, the mode must be one that the list contains. A client that
     cannot use CIBA keeps its default `poll` unchecked.
   - `RefreshTokenRotationRetryCoherenceRule` checks that a client whose effective
     `refresh_token_rotation_retry_required` is `true` and whose refresh tokens can rotate has a positive effective
     `refresh_token_reuse_interval`, and reports the interval otherwise. The refresh tokens of a client can rotate
     (`RefreshTokenRotationRequirement.CanRotate`) under the `always` rotation policy, and those of a public client,
     whose `token_endpoint_auth_method` is `none`, under the `unbound` policy
     ([ADR-0216](0216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md)). FAPI 2.0 §5.3.2.1 Note 1 requires
     a server that rotates refresh tokens to let a client retry, so the FAPI 2.0 security profile sets
     `refresh_token_rotation_retry_required` through its ceiling. The rule names no profile: the profile contributes the
     requirement as data, and no relation among the members of a vocabulary is encoded in code
     ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md),
     [ADR-0217](0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md)).
3. **One validator runs the rules and detects a change.** `IClientCoherenceValidator` has two members:
   - `Validate(tenantSettings, clientSettings)` returns the violations of every registered rule. It reads the
     registered settings from the client's settings document and resolves the effective settings through
     `IEffectiveSettingsResolver` onto the tenant's effective settings, so every caller gives every rule the same two
     views and no caller resolves a client's settings for a check.
   - `ReadsChangedSetting(previousSettings, currentSettings)` returns `true` when the effective value of any setting
     that a rule declares in `SettingNames` differs. The gate is generic, so it cannot omit a setting that a rule
     reads: a setting that is present in only one of the collections has changed, two lists of strings compare as
     ordinal sets, so a change of order alone is not a change, and any other pair of values compares with
     `object.Equals`.

   The validator has no store dependency. A caller gives it the tenant's effective settings, which the caller resolves
   through `IEffectiveSettingsResolver` from the tenant's document and the server's effective settings.
4. **Every place that stores a client, and every change that could make a stored client incoherent, runs the
   validator.** Each reports a violation through its `Description`, in its own shape:
   - **Dynamic registration** refuses the request before it stores the client. The description joins the descriptions
     of the violations, and the error code is `invalid_redirect_uri` when a violation is of `redirect_uris`
     (RFC 7591 §3.2.2), else `invalid_client_metadata`.
   - **The management API** checks a client's create and its settings update against the tenant's effective settings,
     and returns a validation problem with a `SettingError` for each violation, keyed by the client setting that
     registers the value.
   - **Every configuration seeder** runs `SeededClientValidator` (`NCode.Identity.Server`) for a client that it
     creates: the bootstrap administrator, the developer clients, and the conformance clients. A client's settings
     materialize only after the seed, so the validator checks the document against the setting domains, resolves the
     server's committed settings and the tenant's, and runs the coherence validator. A failure stops the host with an
     `InvalidOperationException` that names the kind of client and the client and lists the messages. A seeder leaves an
     existing client as it is, and the bootstrap seeder runs the validator before it imports the secret.
   - **A tenant settings write and a server settings write** report only the violations that the change introduces, so
     a client that was already incoherent does not block an unrelated change. A `SettingError` is keyed by the
     `GoverningSettingName` of the violation, the tenant or server setting that would not permit the value, and says
     "The change would make the client '<id>' incoherent: <description>", with the tenant too for a server write. A
     tenant's clients are walked only when `ReadsChangedSetting` reports that the tenant's effective settings changed a
     setting that a rule reads. A server write first checks the server's effective settings the same way, and then each
     tenant. The walk pages through the clients as an untracked projection that carries no secrets, under an explicit
     tenant scope, because the control-plane endpoints are not scoped to the tenant they address.
   - **A configuration change** — a deployment or a reload of `OpenId:Server:Settings` — cannot be refused, so the
     reconciliation of the server's settings reports it
     ([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)):
     `ClientCoherenceReportHandler` checks every stored client of every tenant against the server's effective settings
     and logs each incoherent client with the description of every violation. The bootstrap administrator is checked
     there as every stored client is; its seeder leaves an existing client as it is.

   A validation problem lists at most ten errors per setting: the first ten in order, and then one error for each
   setting over the limit that counts the rest. A walk over many clients can report one error per client, and it still
   visits every client, so the count is exact.
5. **The rules run after the cheaper checks.** Each place validates the settings document against the setting domains
   ([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)) before it runs the rules, so a rule reads
   only values whose domain is accepted. Dynamic registration and the management settings update also check the key
   material of the client before the rules; a seeder imports the keys or the secret of the client after its rules.
6. **Dynamic registration keeps no relation check of its own.** The request values that become the client's own
   `Intersect` lists (the grant types, the response types, and the algorithms of request objects, of token endpoint
   authentication, and of backchannel authentication), the `id_token_encryption_required` requirement, and the
   delivery-mode, notification-endpoint, and user-code checks of backchannel authentication are checked by rules:
   `RegisteredValueCoherenceRule`, `IdTokenEncryptionCoherenceRule`, and `BackchannelTokenDeliveryCoherenceRule`.
   `ValidateCoherence` is the only relation check of the registration handler, and its descriptions name client
   settings, such as `grant_types_supported`, not request members, such as `grant_types`.

## Options considered

- **A check in each caller.** Rejected: the callers drifted, each checking a different subset, and a settings change
  re-checked only the algorithms.
- **A gate method on each rule.** Rejected: a rule could read a setting that its gate forgets, and a change to that
  setting would skip the walk. The names that a rule declares drive one gate, so the gate and the rule cannot disagree.
- **Rules over the raw documents.** Rejected: the lists that bound a client come from the settings cascade and from the
  security profiles, which only the effective settings carry. A rule reads the registered settings only for the values
  that the effective settings do not show.
- **A violation kind enum.** Rejected: a closed set of kinds would make a package that adds a relation edit the enum,
  and the vocabulary of relations is open ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). A
  violation carries its own `Description` instead.
- **Each caller resolves the registered settings.** Rejected: callers drift, each giving the rules a different view of
  the client. The validator reads both views itself.

## Consequences

- A package adds a coherence rule by registering an `IClientCoherenceRule`. Dynamic registration, the management API,
  the seeders, and the walks over tenants and servers run it without a change.
- A tenant or server settings change that selects `fapi2` or `fapi1-advanced`, or that narrows
  `redirect_uri_kinds_supported`, is refused when it leaves a client with a redirect URI that it excludes. This closes
  backlog `R55`.
- Dynamic registration answers a refused redirect URI with `invalid_redirect_uri` and any other violation with
  `invalid_client_metadata`, and it describes every violation in one form.
- `post_logout_redirect_uris` is checked at every write, as `redirect_uris` is
  ([ADR-0203](0203-redirect-uris-are-matched-exactly.md)). A violation of it is reported as `invalid_client_metadata`,
  because RFC 7591 §3.2.2 reserves `invalid_redirect_uri` for `redirect_uris`.
- A change that selects `fapi2` or `fapi1-advanced`, or that narrows `token_endpoint_auth_methods_supported`, is
  also refused while a client registers a method that the ceiling excludes. A public client that registers `none`, and
  a client that registers `client_secret_basic`, such as the bootstrap administrator, therefore block the change until
  their method changes.
- A write to a client checks its whole effective settings, so a stored client that registers a value that its ceilings
  do not permit is reported at its next write, whatever the write changes.
- A tenant or server change that narrows any list or flag a client registers is refused while it would drop that value.
- A seeder refuses a client that its tenant's settings contradict, so the host does not start with a client that the
  server cannot honor.
- A deployment or a reload that makes a stored client incoherent is logged, client by client, once per change of the
  server's effective settings.
- This implements backlog `R40`, `R55`, `R62`, `R63`, `R72` and `R81`.
