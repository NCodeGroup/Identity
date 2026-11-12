# 1140. Setting values are validated against their domain, not only their type

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0106](project-template/0106-configuration-binds-strictly-and-is-validated-at-startup.md)

## Context

Setting names are a closed vocabulary and every value converts to its descriptor's type
([ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)).
A value of the right type can still be one the server does not honor, and it is then accepted without effect, the same
silent failure that an unregistered name would be:

- **A name that matches no registered strategy.** A security profile name is a string, and a typo of `fapi2` is a
  string too. If it were loaded, the security-profile catalog would find no profile for it and apply none, and the
  client would run without the FAPI 2.0 ceiling and requirements it was configured for. `token_endpoint_auth_method`
  names a client-authentication handler, and an unknown method matches no handler.
- **An enumerated value outside its vocabulary.** `subject_type`, `backchannel_token_delivery_mode`,
  `refresh_token_expiration_policy`, `refresh_token_rotation_policy`, `access_token_type` and the protocol lists
  (`response_types_supported`, `prompt_values_supported`, `code_challenge_methods_supported`, and the like) would accept
  any string. A misspelled value in a ceiling list narrows it to nothing and is never reported.
- **A number or duration outside its range.** A negative lifetime is a type-correct `TimeSpan`. A zero
  `request_uri_fetch_timeout` disables the bound on an outbound fetch. A `user_code_length` of 4 makes a device code
  guessable, and one of `int.MaxValue` exhausts memory.
- **An algorithm the setting cannot use.** The signing lists for tokens that cross a trust boundary could name a
  symmetric algorithm, and every algorithm list could name `none` and `dir`, which the server never offers.
- **A URI or identity the runtime cannot use.** A `jwks_uri` that is not https is never fetched, a host allow-list
  entry written as `10.0.0` never equals the host of a parsed URI, and a redirect URI written as a path is not
  absolute.

## Decision

**Every setting declares its domain, and a value outside it is rejected wherever a setting enters the system, exactly
as an unregistered name or a value of the wrong type is.** Name, type and domain are checked together at every entry
point with the same outcome.

1. **The descriptor carries the rule.** `SettingDescriptor<TValue>.Rule` is a `SettingRule<TValue>`: a description of
   the accepted values and a `TryValidate` that returns a failure phrase. `SettingDescriptor.TryValidate` evaluates it,
   and a descriptor that declares no rule accepts every value of its type. `SettingRules` in
   `NCode.Identity.Settings.Abstractions` provides `Any`, `Must` (a predicate evaluated on every validation, so it can
   consult registered strategies), `OneOf`, `NonEmpty`, `EmptyOr`, `AtLeast`, `Between` and `Each`. The OpenID rules are
   `OpenIdSettingRules` and `SettingAlgorithms`. The rules are pure functions over values with no services, and every
   package that authors descriptors references only abstractions
   ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).
2. **A domain defined by registered strategies consults them.**
   - `security_profiles` values name registered `ISecurityProfile`s.
   - `token_endpoint_auth_method` (default `client_secret_basic`, never empty) and
     `token_endpoint_auth_methods_supported` name registered client-authentication method descriptors
     ([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)).
   - Algorithm lists name registered algorithms of their kind, never `none`, `dir` or `RSA1_5`. The defaults and the
     accepted values come from one classification, so they cannot drift apart.
   - The signing settings for tokens that cross a trust boundary (ID token, UserInfo, access token, introspection and
     authorization responses, DPoP proofs, CIBA requests) accept only asymmetric algorithms. RFC 9449 forbids a
     symmetric one for DPoP, and a shared key cannot authenticate the issuer. Request-object and client-assertion
     signing keep symmetric algorithms for `client_secret_jwt`. The asymmetric-only encryption lists are decided in
     [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md).
   - A grant type is one the server names (`authorization_code`, `implicit`, `password`, `client_credentials`,
     `refresh_token`) or an absolute URI (RFC 6749 §4.5), so an extension grant needs no catalog change.
3. **Fixed vocabularies are enumerated.** Subject types, CIBA delivery modes, response modes, prompt and display values
   and claim types (of which only `normal` is implemented) are enumerated. Response types are whole canonical values
   ([ADR-1141](1141-a-response-type-is-a-whole-value-and-the-listed-values-alone-control-front-channel-tokens.md)). PKCE methods are the registered `ICodeChallengeMethod`
   transformations, without `plain`
   ([ADR-1144](1144-authorization-codes-are-bound-to-an-s256-pkce-challenge-unless-an-attested-client-uses-the-nonce.md)). `access_token_type` is `at+jwt` with or
   without its `application/` prefix. `refresh_token_expiration_policy` is `absolute` or `sliding`, so a refresh token
   always expires (RFC 9700 §4.14.2), and `refresh_token_rotation_policy` is `unbound` or `always`.
4. **Shapes are checked as the runtime uses the value.**
   - A URI has a scheme and no whitespace.
   - `jwks_uri` and `sector_identifier_uri` are https. `client_notification_endpoint` is empty by default or an https
     URI, so a ping or push client that registered none is reported as a missing endpoint
     ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md))
     rather than failing to read the setting.
   - A redirect URI is absolute without a fragment.
   - A logout URI and an informational link are http or https.
   - A host allow-list entry is written as a URI reports its host.
   - `request_uri_expected_content_type` is a media type with no parameters.
   - The `tls_client_auth` identities are a distinguished name, a DNS name, a URI, a canonical IP address and an email
     address.
5. **Durations and numbers have ranges.** A lifetime or interval is at least one second, a timeout is positive, a value
   where zero means disabled is not negative, a size cap is at least one, and `user_code_length` is between 8 and 32
   (RFC 8628 §6.1).
6. **Every entry point enforces the domain.**
   - Configuration is checked at start and on reload.
   - Management writes answer `400` keyed by setting name.
   - Stored documents fail closed.
   - Dynamic client registration answers `invalid_redirect_uri` for the redirection URIs and `invalid_client_metadata`
     otherwise.
   - The conformance and developer seeders stop with an error naming the client.
   - A list that contains `null` does not conform.
7. **Values set in code are not checked when they are set.** Descriptor defaults, profile ceilings and
   default-settings providers are code, not input, and a ceiling may name a value outside the live domain, as an
   `Intersect` ceiling naming `Ed25519` without the EdDSA package does. A test pins every registered default to its own
   rule, and the profile catalog throws for an unregistered profile name instead of applying none.

## Options considered

- **An allowed-values list on the descriptor.** Rejected: it expresses enumerations but not ranges, shapes or a domain
  that follows registered strategies.
- **Validate in `Set`.** Rejected: code-set values are deliberately allowed outside the live domain, and `Set` already
  guards the name and type.
- **Validate only the enumerated scalars.** Rejected: a misspelled ceiling value is still configuration accepted
  without effect, and it narrows a capability without a signal.
- **A grant domain from the grant handlers.** Rejected: resolving every handler to validate the catalog couples it to
  the construction graph of the whole token pipeline, and RFC 6749 already defines the domain while leaving extension
  grants open.
- **Widenable symmetric signing at a trust boundary.** Rejected: a shared key does not authenticate the issuer to
  anyone holding it, and RFC 9449 forbids it for DPoP proofs.
- **A never-expiring refresh-token policy.** Rejected: RFC 9700 §4.14.2 expects refresh tokens to expire.
- **Chosen: a rule on every descriptor, enforced at every entry point.**

## Consequences

- A configured value outside its domain stops the host with an error naming the configuration path and the domain, a
  management write of one returns `400`, a stored one fails the request that loads it, and a registration with one is
  refused.
- A descriptor author gives each descriptor a rule, and its default satisfies it.
- Narrowing a domain makes a stored document that uses the removed value fail closed, and no migration rewrites it
  ([ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)).
- A test pins every registered default to its own rule.

## References

- [ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md):
  the closed vocabulary of names and types this completes.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md):
  validate a value the way it is used.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md):
  secure-by-default metadata.
- RFC 6749 §4.5, RFC 8628 §6.1, RFC 9449, RFC 9700 §4.14.2.
- Code: `NCode.Identity.Settings.SettingRule<TValue>`, `NCode.Identity.Settings.SettingRules`,
  `NCode.Identity.OpenId.Settings.OpenIdSettingRules`, `NCode.Identity.OpenId.Settings.SettingAlgorithms`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`,
  `NCode.Identity.Settings.DefaultSettingSerializer`, `NCode.Identity.OpenId.Settings.DefaultSettingConfigurationReader`,
  `NCode.Identity.OpenId.Settings.DefaultSecurityProfileCatalog`.
