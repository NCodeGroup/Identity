# 140. Setting values are validated against their domain, not only their type

- **Status:** Accepted (response-type domain amended by [ADR-0141](0141-response-types-are-whole-values.md): a
  response type is a whole value in canonical form, enforced exactly; PKCE-method domain amended by
  [ADR-0144](0144-authorization-codes-are-bound-to-pkce-by-default.md): it is the registered `ICodeChallengeMethod`
  transformations, not a fixed vocabulary, and excludes `plain`; client-authentication-method domain amended by
  [ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md): `token_endpoint_auth_method` is always a
  registered method, `client_secret_basic` by default, never empty; security-profile domain amended by
  [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md): `security_profiles` is a list whose every
  value names a registered profile; extended by [ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md): the same
  entry points also reject a setting at a scope that never reads it; client-authentication-method domain amended by
  [ADR-0207](0207-client-authentication-methods-are-named-by-registered-descriptors.md): the names of the registered
  method descriptors)
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

Setting names are a closed vocabulary and every value must convert to its descriptor's type
([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)). A value of the right type can still be one the server does
not honor, and it is then accepted without effect — the same silent failure ADR-0139 removes for names:

- **A name that matches no registered strategy.** `security_profile` is a string; a typo of `fapi2` is a string too. If
  it were loaded, the security-profile catalog would find no profile for it and apply none, and the client would run
  without the FAPI 2.0 ceiling and requirements it was configured for. `token_endpoint_auth_method` names a
  client-authentication handler; an unknown method matches no handler.
- **An enumerated value outside its vocabulary.** `subject_type`, `backchannel_token_delivery_mode`,
  `refresh_token_expiration_policy`, `refresh_token_reuse_policy`, `access_token_type`, and the protocol lists
  (`response_types_supported`, `prompt_values_supported`, `code_challenge_methods_supported`, …) accept any string.
  A misspelled value in a ceiling list narrows it to nothing and is never reported.
- **A number or duration outside its range.** A negative lifetime is a type-correct `TimeSpan`. A zero
  `request_uri_fetch_timeout` disables the bound on an outbound fetch. A `user_code_length` of 4 makes a device code
  guessable; one of `int.MaxValue` exhausts memory.
- **An algorithm the setting cannot use.** The signing lists for tokens that cross a trust boundary accepted a symmetric
  algorithm, and every algorithm list accepted `none` and `dir`, which the server never offers.
- **A URI or identity the runtime cannot use.** A `jwks_uri` that is not https is never fetched; a host allow-list
  entry written as `10.0.0` never equals the host of a parsed URI; a redirect URI written as a path is not absolute.

## Decision

**Every setting declares its domain, and a value outside it is rejected wherever a setting enters the system — exactly
as an unregistered name or a value of the wrong type is.** Value-domain validation completes the closed vocabulary of
ADR-0139: name, type, and domain are checked together, at the same entry points, with the same outcome.

1. **The descriptor carries the rule.** `SettingDescriptor<TValue>.Rule` is a `SettingRule<TValue>` — a description of
   the accepted values and a `TryValidate` that returns a failure phrase — and `SettingDescriptor.TryValidate` evaluates
   it. A descriptor that declares no rule accepts every value of its type. `SettingRules` provides the built-in rules:
   `Any`, `Must` (a predicate, evaluated on every validation so it can consult registered strategies), `OneOf`
   (ordinal), `NonEmpty`, `EmptyOr` (the empty string a string setting uses for "unset"), `AtLeast`, `Between`, and
   `Each` (a list whose every value satisfies a rule; the failure names the first offending value). The rule vocabulary
   lives in `NCode.Identity.Settings.Abstractions` beside `SettingMerge`: every package that authors descriptors
   references only abstractions ([ADR-0016](0016-implementation-packages-depend-only-on-abstractions.md)), and the
   rules are pure functions over values with no services.
2. **A domain defined by registered strategies consults them.**
   - `security_profile` is empty or the name of a registered `ISecurityProfile`.
   - `token_endpoint_auth_method` is empty or, like every value of `token_endpoint_auth_methods_supported`, the name of
     a registered client-authentication method descriptor, which the authentication slice registers beside each handler
     ([ADR-0207](0207-client-authentication-methods-are-named-by-registered-descriptors.md)).
   - Every algorithm list names registered algorithms of its kind (signature, key management, content encryption,
     compression), never `none` or `dir`. The advertised defaults and the accepted values come from one classification,
     so they cannot drift apart.
   - The signing lists for tokens that cross a trust boundary — `id_token`, `userinfo`, `access_token`, and DPoP
     proofs — accept only asymmetric signature algorithms. A symmetric algorithm is not an operator option for them
     (RFC 9449 forbids it for DPoP; a shared key cannot authenticate the issuer to a third party), which amends
     [ADR-0083](0083-secure-by-default-discovery-metadata.md). Request-object and client-assertion signing keep
     symmetric algorithms, because `client_secret_jwt` legitimately uses the client's secret.
   - A grant type is one the server names (`authorization_code`, `implicit`, `password`, `client_credentials`,
     `refresh_token`; a hybrid flow uses `authorization_code` and `implicit` —
     [ADR-0141](0141-response-types-are-whole-values.md)) or an absolute URI, which is how RFC 6749 §4.5 names an
     extension grant — the device code, CIBA, and token exchange grants among them — so an extension grant needs no
     catalog change.
3. **Fixed protocol vocabularies are enumerated.** Subject types, CIBA delivery modes, response types (whole values in
   canonical form, which the authorization endpoint enforces exactly —
   [ADR-0141](0141-response-types-are-whole-values.md)), response modes, prompt and display values, PKCE methods,
   and claim types (only `normal` is implemented). `access_token_type` is the RFC 9068 type `at+jwt`, with or without
   its `application/` prefix, so explicit typing (RFC 8725 §3.11) cannot be configured away.
   `refresh_token_expiration_policy` is `absolute` or `sliding`: a refresh token always expires (RFC 9700 §4.14.2), so
   no never-expiring policy exists.
4. **Shapes are checked the way the runtime uses the value.** A URI must be written with its scheme and contain no
   whitespace (`Uri.TryCreate` otherwise accepts an implicit file path and trims). `jwks_uri`, `sector_identifier_uri`,
   and `client_notification_endpoint` are https; a redirect URI is absolute without a fragment (any scheme, for native
   applications); a logout URI is http or https without a fragment; an informational link is http or https. A host
   allow-list entry is written exactly as a URI reports its host; the mutual-TLS alias authority is an https origin;
   `request_uri_expected_content_type` is a media type with no parameters, because it is compared verbatim. The
   `tls_client_auth` identities are a parsable distinguished name, a DNS name, an absolute URI, an IP address in its
   canonical form, and a bare email address. `jwks` is a JSON Web Key Set object with a `keys` array.
5. **Durations and numbers have ranges.** A lifetime or interval, which the protocol expresses in whole seconds, is at
   least one second; a timeout is positive; a setting where zero means "disabled" or "no tolerance" is not negative; a
   size cap is at least one; `user_code_length` is between 8 and 32 (RFC 8628 §6.1: eight characters from the default
   alphabet give about 34.5 bits).
6. **Every entry point enforces the domain.** Configuration (`OpenId:Server:Settings`) on start and on reload, through
   the configuration reader; management writes, through `ISettingSerializer.ValidateSettings` (`400`, keyed by setting
   name); stored documents, through `ISettingSerializer.DeserializeSettings` (fail closed); dynamic client registration,
   which validates the document it would store (`invalid_redirect_uri` for the redirection URIs,
   `invalid_client_metadata` otherwise) rather than storing a client that fails every request that loads it; and the
   conformance and developer seeders, which validate the document built from their configuration and stop with an
   error naming the client. A list containing `null` does not conform to its type.
7. **Values set in code are not checked when they are set.** Defaults, security-profile ceilings, and default-settings
   providers are code, not input — and a ceiling may deliberately name a value outside the live domain (the FAPI 2.0
   profile lists `EdDSA` forward-compatibly; an `Intersect` ceiling drops what the server lacks). A test pins every
   registered default of the composed server to its own rule. As a second line of defense, the security-profile
   catalog throws for a non-empty profile name that is not registered instead of applying no profile.

## Options considered

- **An allowed-values list on the descriptor.** Rejected: it expresses enumerations but not ranges, shapes, or a domain
  that follows registered strategies.
- **Validate in `ISettingCollection.Set`.** Rejected: code-set values are deliberately allowed outside the live domain
  (forward-compatible ceilings), and `Set` already guards the name and type.
- **Validate the enumerated scalars only and leave the ceiling lists.** Rejected: a misspelled ceiling value is still
  configuration accepted without effect, and it narrows a capability without a signal.
- **Derive the grant-type domain from the registered token grant handlers.** Rejected: resolving every grant handler to
  build or validate the descriptor catalog couples the catalog to the construction graph of the whole token pipeline,
  and RFC 6749 already defines the domain while leaving extension grants open.
- **Keep symmetric signing widenable for tokens that cross a trust boundary.** Rejected: a shared key does not
  authenticate the issuer to anyone holding it, and RFC 9449 forbids it for DPoP proofs.
- **Keep a never-expiring refresh-token policy.** Rejected: RFC 9700 §4.14.2 expects refresh tokens to expire.

## Consequences

- A configured value outside its domain stops the host with an error naming the configuration path and the domain; a
  management write of one returns `400`; a stored one fails the request that loads it; a registration with one is
  refused with `invalid_client_metadata` or `invalid_redirect_uri`.
- A descriptor author gives each descriptor a rule, and its default must satisfy it.
- Narrowing a domain — unregistering a security profile or an algorithm, removing a vocabulary value — makes stored
  documents that use the removed value fail closed, so it ships with a data migration, as renaming a setting does
  (ADR-0139).
- A response type is a whole value, so dynamic client registration stores a combined `response_types` value such as
  `code id_token` as registered and the authorization endpoint enforces exactly it
  ([ADR-0141](0141-response-types-are-whole-values.md)).
- `client_notification_endpoint` has an empty default, so a ping or push client that registered none is reported as a
  missing endpoint rather than failing to read the setting.
- Follow-ups: issue [#93](https://github.com/NCodeGroup/Identity/issues/93) (a parent's security profile is un-loosenable only when it is `fapi2`, decided in
  [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)) and [#94](https://github.com/NCodeGroup/Identity/issues/94)
  (registration of combined `response_types`, decided in [ADR-0141](0141-response-types-are-whole-values.md)).

## References

- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) — the closed vocabulary of names and types this completes.
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md) — validate a value the way it is used.
- [ADR-0083](0083-secure-by-default-discovery-metadata.md) — secure-by-default metadata, amended for symmetric signing.
- [ADR-0108](0108-fapi-security-profile-merge-lattice-ceiling-injection.md) — security profiles.
- [ADR-0063](0063-dynamic-client-registration.md) — dynamic client registration.
- RFC 6749 §4.5, RFC 7591 §3.2.2, RFC 8628 §6.1, RFC 8725 §3.11, RFC 9068 §2.1, RFC 9449, RFC 9700 §4.14.2, OpenID
  Connect CIBA Core §4.
- Code: `NCode.Identity.Settings.SettingRule<TValue>`, `NCode.Identity.Settings.SettingRules`,
  `NCode.Identity.OpenId.Settings.OpenIdSettingRules`, `NCode.Identity.OpenId.Settings.SettingAlgorithms`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`,
  `NCode.Identity.OpenId.Authentication.Settings.DefaultClientSettingDescriptorDataSource`,
  `NCode.Identity.Settings.DefaultSettingSerializer`, `NCode.Identity.OpenId.Settings.DefaultSettingConfigurationReader`,
  `NCode.Identity.OpenId.Settings.DefaultSecurityProfileCatalog`.
