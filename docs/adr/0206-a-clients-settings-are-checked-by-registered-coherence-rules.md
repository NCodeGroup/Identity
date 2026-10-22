# 206. A client's settings are checked by registered coherence rules

- **Status:** Accepted
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

1. **A rule checks one relation over the effective settings of a client.** `IClientCoherenceRule`
   (`NCode.Identity.OpenId.Authentication.Abstractions`, namespace `NCode.Identity.OpenId.Authentication.Clients`) has
   two members:
   - `SettingNames` lists every setting whose effective value the rule reads.
   - `FindViolations(clientSettings)` returns each value that the client registers and that an effective setting does
     not permit, as a `ClientCoherenceViolation(SettingName, Value, CeilingName)`: the client setting that registers the
     value, the value, and the effective setting that does not permit it.

   Rules are an open strategy collection ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). They are
   registered with `TryAddEnumerable`, which keeps one registration per implementation type, so each rule is its own
   class and a package adds a rule with one registration.
2. **Three built-in rules** live in `NCode.Identity.OpenId.Authentication`, in the namespace
   `NCode.Identity.OpenId.Authentication.Clients.Coherence`:
   - `ResponseAlgorithmCoherenceRule` checks the algorithms that a client registers for the JWTs addressed to it: its ID
     tokens, UserInfo responses, introspection responses, and authorization responses. A registered signing algorithm
     must be in the effective signing list, and a registered key-management algorithm and its content encryption,
     `A128CBC-HS256` unless registered, must be in the effective encryption lists. An unset list is unrestricted.
   - `RedirectUriKindCoherenceRule` checks each URI of `redirect_uris` and of `post_logout_redirect_uris` against
     `redirect_uri_kinds_supported` through `IRedirectUriKindCatalog.IsPermitted`.
   - `TokenEndpointAuthMethodCoherenceRule` checks `token_endpoint_auth_method` against
     `token_endpoint_auth_methods_supported`. An unset list permits every method.
3. **One validator runs the rules and detects a change.** `IClientCoherenceValidator` has two members:
   - `Validate(clientSettings)` returns the violations of every registered rule.
   - `ReadsChangedSetting(previousSettings, currentSettings)` returns `true` when the effective value of any setting
     that a rule declares in `SettingNames` differs. The gate is generic, so it cannot omit a setting that a rule
     reads: a setting that is present in only one of the collections has changed, two lists of strings compare as
     ordinal sets, so a change of order alone is not a change, and any other pair of values compares with
     `object.Equals`.

   The validator works on effective settings that the caller has resolved, and has no store or resolver dependency.
   Callers resolve them through `IEffectiveSettingsResolver`: a client's document onto its tenant's effective settings,
   and a tenant's document onto the server's.
4. **Every place that stores a client, and every change that could make a stored client incoherent, runs the
   validator.** Each reports a violation in its own shape:
   - **Dynamic registration** refuses the request before it stores the client. The description joins the violations,
     each as `The '<setting>' value '<value>' is not permitted by the effective '<ceiling>'.`, and the error code is
     `invalid_redirect_uri` when a violation is of `redirect_uris` (RFC 7591 §3.2.2), else `invalid_client_metadata`.
   - **The management API** checks a client's create and its settings update against the tenant's effective settings,
     and returns a validation problem with a `SettingError` for each violation, keyed by the client setting that
     registers the value.
   - **The conformance seed** resolves the server's, the tenant's, and the client's settings itself, because the
     client's settings materialize only after the seed. A violation stops the host with an `InvalidOperationException`
     that names the client and lists the violations.
   - **A tenant settings write and a server settings write** report only the violations that the change introduces, so
     a client that was already incoherent does not block an unrelated change. A `SettingError` is keyed by the ceiling,
     the tenant or server setting that would not permit the value, and names the client, and for a server write the
     tenant too. A tenant's clients are walked only when `ReadsChangedSetting` reports that the tenant's effective
     settings changed a setting that a rule reads. A server write first checks the server's effective settings the same
     way, and then each tenant. The walk pages through the clients as an untracked projection that carries no secrets,
     under an explicit tenant scope, because the control-plane endpoints are not scoped to the tenant they address.
5. **The rules run after the cheaper checks.** Each place validates the settings document against the setting domains
   ([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)) before it runs the rules, so a rule reads
   only values whose domain is accepted. Dynamic registration and the management settings update also check the key
   material of the client before the rules; the seed imports the keys of the client after its rules.
6. **Some dynamic registration checks stay out of the rules** (backlog `R62`). Dynamic registration checks the request
   values that become the client's own `Intersect` lists (the grant types, the response types, and the algorithms of
   request objects, of token endpoint authentication, and of backchannel authentication), the
   `id_token_encryption_required` requirement, and the delivery-mode, notification-endpoint, and user-code checks of
   backchannel authentication. A rule needs a violation shape for a companion value that is missing, and must not flag
   a client that does not use backchannel authentication, whose delivery mode defaults to `poll`.

## Options considered

- **A check in each caller.** Rejected: the callers drifted, each checking a different subset, and a settings change
  re-checked only the algorithms.
- **A gate method on each rule.** Rejected: a rule could read a setting that its gate forgets, and a change to that
  setting would skip the walk. The names that a rule declares drive one gate, so the gate and the rule cannot disagree.
- **Rules over the raw documents.** Rejected: the lists that bound a client come from the settings cascade and from the
  security profiles, which only the effective settings carry.

## Consequences

- A package adds a coherence rule by registering an `IClientCoherenceRule`. Dynamic registration, the management API,
  the seed, and the walks over tenants and servers run it without a change.
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
- The checks of decision 6 are separate from the rules until backlog `R62` moves them.
- This implements backlog `R40` and `R55`.
