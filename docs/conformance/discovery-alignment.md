# Discovery-metadata alignment

`/.well-known/openid-configuration` advertises **exactly what the OP honours** — no more, no less — with secure-by-default
values an operator overrides through configuration ([ADR-1083](../decisions/1083-secure-by-default-discovery-metadata.md)).
The Config OP profile reads the discovery document and **fails on anything advertised that the OP cannot deliver**, so
over-advertising is a certification blocker, not a warning. This document is the field-by-field reference and
rationale. Parent design: [`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #4 and Appendix A).
Tracking issue: [#128](https://github.com/NCodeGroup/identity/issues/128).

## Secure-by-default values

- `response_types_supported` defaults to `["code"]` (no implicit/hybrid combinations); `grant_types_supported` lists
  `authorization_code`, `refresh_token`, the device-code grant and CIBA, and never `implicit`;
  `code_challenge_methods_supported` defaults to `["S256"]`; `subject_types_supported` defaults to
  `["public", "pairwise"]`.
- The cross-trust-boundary token-signing fields (`id_token`/`userinfo`/`access_token`) advertise asymmetric algorithms
  only (never symmetric `HS256`); `request_object` / `token_endpoint_auth` signing keep the full set for
  `client_secret_jwt`.
- The host's `appsettings.json` advertises none of the front-channel-token flows.
- Because advertisement and request-time enforcement share one settings source, these flows are fail-closed by default;
  an operator enables (and so advertises) any of them by widening the setting in host configuration. Integration tests
  cover both the secure default and the operator-override path.

## How discovery is built

- **Discovery is data-driven from settings.**
  [`DefaultDiscoverMetadataHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/Handlers/DefaultDiscoverMetadataHandler.cs)
  emits every setting whose descriptor `IsDiscoverable` is true (`Descriptor.Format(setting)`), plus the discoverable
  endpoint URLs from `EndpointDataSource`/`LinkGenerator`. So alignment means **auditing the discoverable setting
  values**, not editing the handler.
- **Baseline values come from the settings pipeline.** Defaults are declared in
  [`DefaultSettingDescriptorDataSource`](../../src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs)
  and the baseline provider
  ([`DefaultSettingsProvider`](../../src/NCode.Identity.OpenId.Core/Settings/DefaultSettingsProvider.cs)), and a host
  overrides them through configuration.
- **Settings are enforced.** `response_types_supported` / `grant_types_supported` are enforced at request time (e.g.
  [`DefaultValidateAuthorizationRequestHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultValidateAuthorizationRequestHandler.cs)),
  so the advertised ceiling and the enforced ceiling share one source — the audit is about choosing the right values,
  not reconciling two code paths.
- **The conformance profile widens only what it exercises.** `appsettings.Conformance.json` adds the grants the suite's
  modules use (`client_credentials`, token exchange) to `grant_types_supported`; it never advertises implicit or hybrid
  response types.

## Audit checklist (Appendix A)

Each discoverable field, checked against actual support for the Basic/Config profiles:

| Metadata field                                                           | Target (Basic/Config)                          | Note                                                                                 |
| ------------------------------------------------------------------------ | ---------------------------------------------- | ------------------------------------------------------------------------------------ |
| `issuer`                                                                 | exact reached origin                           | Owned by [#126](https://github.com/NCodeGroup/identity/issues/126) (byte-match)      |
| `authorization_endpoint`/`token_endpoint`/`jwks_uri`/`userinfo_endpoint` | origin-relative, present                       | Emitted from `EndpointDataSource`                                                    |
| `response_types_supported`                                               | `["code"]`                                     | Implicit/hybrid are not advertised                                                   |
| `grant_types_supported`                                                  | includes `authorization_code`, `refresh_token` | Never `implicit`; matches the seeded clients' grants                                 |
| `subject_types_supported`                                                | `["public","pairwise"]`                        | Both implemented ([#47](https://github.com/NCodeGroup/identity/issues/47), ADR-1093) |
| `id_token_signing_alg_values_supported`                                  | asymmetric algorithms only                     | Must match the key served at `jwks_uri`                                              |
| `scopes_supported`                                                       | incl. `openid`, `profile`, `email`             | Suite requests these in standard-claims tests                                        |
| `claims_supported`                                                       | the claims actually returned                   | Over-advertising triggers warnings                                                   |
| `token_endpoint_auth_methods_supported`                                  | `["client_secret_basic","client_secret_post"]` | Must cover both to certify Basic OP                                                  |
| `code_challenge_methods_supported`                                       | `["S256"]`                                     | `plain` is not offered (ADR-1144)                                                    |

## Approach

- Alignment is a **configuration/settings audit**, driven from the conformance profile
  ([#126](https://github.com/NCodeGroup/identity/issues/126)), not a handler rewrite.
- Implicit and hybrid response types are advertised only for a profile that exercises them.
- Every advertised value is backed by real behavior: each `scope` resolvable, each `token_endpoint_auth_method` accepted
  at the token endpoint, the advertised `id_token` signing alg matching the active `jwks_uri` key, and
  `claims_supported` matching what the claims handlers emit.
- Where the OP intentionally does **not** support something the suite probes, that is a baseline entry
  (`expected-skips.json` / `expected-failures.json`, [#124](https://github.com/NCodeGroup/identity/issues/124)), not an
  advertised capability.

## Acceptance criteria

- Every field in the live discovery document is backed by a verified capability (no over-advertising).
- `response_types_supported` and `grant_types_supported` contain only verified values (no `implicit`/hybrid for the
  Basic/Config profiles).
- `id_token_signing_alg_values_supported` matches the key served at `jwks_uri`;
  `token_endpoint_auth_methods_supported` covers both `client_secret_basic` and `client_secret_post`.
- The Config OP plan passes with no "advertised-but-unsupported" failures.

## Dependencies & links

- **Depends on** [#126](https://github.com/NCodeGroup/identity/issues/126) for the `issuer` byte-match; **pairs with**
  [#127](https://github.com/NCodeGroup/identity/issues/127) (advertised grants must match the seeded clients).
- Feeds [#124](https://github.com/NCodeGroup/identity/issues/124) (Config OP) and its baseline files for any intentional
  non-support.
- Field-by-field reference: [`conformance-testing.md`](../conformance-testing.md) Appendix A.
