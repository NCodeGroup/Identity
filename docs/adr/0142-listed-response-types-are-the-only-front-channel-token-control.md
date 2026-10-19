# 142. The listed response types are the only control over the tokens the authorization endpoint returns

- **Status:** Accepted (extends [ADR-0141](0141-response-types-are-whole-values.md); retires a setting of
  [ADR-0038](0038-setting-merge-lattice-duals.md))
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The response type decides whether the authorization endpoint returns an access token: a response type that includes
the `token` name returns one (OAuth 2.0 Multiple Response Type Encoding Practices). The server accepts exactly the whole
response types listed in `response_types_supported`, and the list merges as a ceiling (intersection) down the
server → tenant → client cascade ([ADR-0141](0141-response-types-are-whole-values.md),
[ADR-0038](0038-setting-merge-lattice-duals.md)). Listing `token`, `id_token token`, `code token`, or
`code id_token token` permits an access token from the authorization endpoint; a tenant or client that lists none of
them forbids it; discovery advertises the tenant's list; and dynamic client registration refuses a response type the
tenant does not list.

A boolean setting, `allow_unsafe_token_response` (default `true`, merged by conjunction), separately rejected every
response type that includes `token`, with `unauthorized_client`. It is a second control for the same capability, and
the two disagree:

- An operator who lists `code token` but leaves the boolean `false` (or the reverse) has a flow that is advertised and
  registrable but refused at every request. [ADR-0083](0083-secure-by-default-discovery-metadata.md)'s guarantee —
  advertisement and enforcement share one source, so there is no second switch to forget — does not hold.
- The refusal is always `unauthorized_client`, even when the tenant imposes it, where RFC 6749 §4.1.2.1 calls for
  `unsupported_response_type`.
- The name describes the response mode ("unsafe" token responses), which is a separate rule — a response that returns
  a token is never encoded in the query string (ADR-0141 point 6) — rather than the capability it controls.

RFC 9700 §2.1.2 says clients SHOULD NOT use response types that issue access tokens in the authorization response. That
is why the default list excludes them; it is not a reason for a second control.

## Decision

**`response_types_supported` is the only control over the tokens the authorization endpoint returns.** The
`allow_unsafe_token_response` setting — its descriptor, setting name, setting key, and check — is removed. A tenant or
client forbids access tokens from the authorization endpoint by listing no response type that includes `token`. The
secure-by-default baseline lists only `code` (ADR-0083), so by default the authorization endpoint returns no token.

**Stored settings documents are rewritten to the setting's meaning, not stripped of it.** Retiring a setting rewrites
the stored documents ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)); a data migration rewrites every stored
server, tenant, client, and resource-server settings document that sets it:

- `true`, or `null` (its default), restricted nothing: the setting is removed.
- `false` becomes a narrowed `response_types_supported` in the same document: the values that document lists that do
  not include `token`, or, when it lists none, every response type that does not (`code`, `id_token`,
  `code id_token`). The list merges by intersection as the boolean merged by conjunction, so the document restricts
  exactly what it restricted before at every level of the cascade.
- Any other value already fails the document when it is read. That document is not rewritten, so it keeps failing until
  an operator corrects it, rather than the migration guessing at its intent.

A rewritten document gets a new settings concurrency token, as any settings write does. The migration has no reverse
step: a rewritten document is valid, and means the same, under the model that had the boolean.

Configuration that names the setting (`OpenId:Server:Settings`) stops the host when it starts, naming the configuration
path (ADR-0139); the operator removes it and narrows `response_types_supported` instead.

## Options considered

- **Keep the boolean and make discovery and registration read it too.** Rejected: every reader of the response types
  would have to combine two settings, the refusal would still not say which scope imposed it, and it is the second
  switch ADR-0083 rules out.
- **Keep the boolean as a server-wide kill switch only.** Rejected: the server's own `response_types_supported` already
  is that switch, since it caps every tenant by intersection.
- **Remove the setting and drop it from stored documents.** Rejected: a document that set it to `false` would silently
  start permitting access tokens from the authorization endpoint — the loosening ADR-0139 forbids.
- **Translate the setting when a document is read** (the upcasting that
  [ADR-0113](0113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md) applies to grant payloads). Rejected:
  settings documents are few, admin-authored, and long-lived, so a one-time rewrite keeps the retired name out of the
  runtime entirely; ADR-0113 leaves settings documents out of its scope for this reason.

## Consequences

- Discovery, dynamic client registration, and the authorization endpoint agree on whether the authorization endpoint
  can return an access token, because they read one list.
- A request that the tenant's list forbids is refused with `unsupported_response_type`; one that only the client's list
  forbids is refused with `unauthorized_client`.
- A tenant or server document that forbade token responses through the boolean carries an explicit
  `response_types_supported`, so its discovery stops advertising the token-bearing response types it never accepted.
- `OpenIdSettingNames.AllowUnsafeTokenResponse` and `OpenIdSettingKeys.AllowUnsafeTokenResponse` leave the public
  surface.
- A deployment that stores settings documents outside the reference host's database applies the same rewrite to its own
  store before it runs this version; otherwise a document that sets the setting fails when it is read.

## References

- [ADR-0141](0141-response-types-are-whole-values.md) — response types are whole values; the list this decision makes
  the only control.
- [ADR-0083](0083-secure-by-default-discovery-metadata.md) — advertisement and enforcement share one source.
- [ADR-0038](0038-setting-merge-lattice-duals.md) — the merge lattice; intersection and conjunction are both ceilings.
- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) — stored documents fail closed; retiring a setting rewrites
  them.
- [ADR-0113](0113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md) — read-time upcasting, scoped to grant
  payloads.
- [ADR-0010](0010-supported-settings-unset-means-unrestricted.md) — an unset list is unrestricted.
- RFC 6749 §4.1.2.1, RFC 9700 §2.1.2, OAuth 2.0 Multiple Response Type Encoding Practices.
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers.DefaultValidateAuthorizationRequestHandler`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`, and the `RetireAllowUnsafeTokenResponse`
  migration in `NCode.Identity.OpenId.Host.Database`.
