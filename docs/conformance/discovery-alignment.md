# T5 — Discovery-metadata alignment audit

Status: **implemented** ([ADR-0083](../adr/0083-secure-by-default-discovery-metadata.md)). The OpenID Provider now
advertises secure-by-default, verified capabilities, overridable by operators through configuration. This document is
retained as the field-by-field reference and rationale. Parent design:
[`docs/conformance-testing.md`](../conformance-testing.md) (prerequisite #4 and Appendix A).

## What shipped

- `response_types_supported` defaults to `["code"]` (no implicit/hybrid combinations); `grant_types_supported` drops
  `implicit`; `code_challenge_methods_supported` defaults to `["S256"]`; `subject_types_supported` defaults to
  `["public"]`.
- The cross-trust-boundary token-signing fields (`id_token`/`userinfo`/`access_token`) advertise asymmetric algorithms
  only (never symmetric `HS256`); `request_object` / `token_endpoint_auth` signing keep the full set for
  `client_secret_jwt`.
- The Playground `appsettings.json` no longer over-advertises `implicit`.
- Because advertisement and request-time enforcement share one settings source, these flows are fail-closed by default;
  an operator re-enables (and simultaneously re-advertises) any of them by widening the setting in host configuration.
  Integration tests cover both the secure default and the operator-override path.

## Goal

Ensure `/.well-known/openid-configuration` advertises **exactly what the OP actually honours** — no more,
no less. The Config OP profile reads the discovery document and **fails on anything advertised that the
OP cannot deliver**, so over-advertising is a certification blocker, not a warning.

## Current state (verified)

- **Discovery is fully data-driven from settings.**
  [`DefaultDiscoverMetadataHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/Handlers/DefaultDiscoverMetadataHandler.cs)
  emits every setting whose descriptor `IsDiscoverable` is true (`Descriptor.Format(setting)`), plus the
  discoverable endpoint URLs from `EndpointDataSource`/`LinkGenerator`. So "alignment" means **auditing
  the discoverable setting values**, not editing the handler.
- **Baseline values come from the settings pipeline.** Defaults are declared in
  [`DefaultSettingDescriptorDataSource`](../../src/NCode.Identity.OpenId.Core/Settings/DefaultSettingDescriptorDataSource.cs)
  and the baseline provider
  ([`DefaultSettingsProvider`](../../src/NCode.Identity.OpenId.Core/Settings/DefaultSettingsProvider.cs)),
  and may be overridden per host via configuration.
- **A known over-advertisement already exists.**
  [`appsettings.json`](../../src/NCode.Identity.OpenId.Playground/appsettings.json) advertises
  `grant_types_supported` including **`implicit`**, and the response-type default set includes implicit/
  hybrid types — but those flows are **not** verified end-to-end. Advertising them will fail Config OP
  unless corrected or deliberately scoped out.
- **Settings are enforced.** `response_types_supported` / `grant_types_supported` are enforced at request
  time (e.g.
  [`DefaultValidateAuthorizationRequestHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultValidateAuthorizationRequestHandler.cs)),
  so the advertised ceiling and the enforced ceiling share one source — the audit is about choosing the
  right values, not reconciling two code paths.

## Audit checklist (Appendix A)

Verify each discoverable field against actual support for the Basic/Config profiles:

| Metadata field                                                           | Target (Basic/Config)                          | Note                                                  |
| ------------------------------------------------------------------------ | ---------------------------------------------- | ----------------------------------------------------- |
| `issuer`                                                                 | exact reached origin                           | Owned by `T3` (byte-match)                            |
| `authorization_endpoint`/`token_endpoint`/`jwks_uri`/`userinfo_endpoint` | origin-relative, present                       | Emitted from `EndpointDataSource`                     |
| `response_types_supported`                                               | `["code"]`                                     | **Drop `implicit`/hybrid** until those flows verified |
| `grant_types_supported`                                                  | `["authorization_code","refresh_token"]`       | **Drop `implicit`**; match the seeded client's grants |
| `subject_types_supported`                                                | `["public"]`                                   | `pairwise` only if implemented (`F9`)                 |
| `id_token_signing_alg_values_supported`                                  | `["RS256"]`                                    | Must match the key served at `jwks_uri`               |
| `scopes_supported`                                                       | incl. `openid`, `profile`, `email`             | Suite requests these in standard-claims tests         |
| `claims_supported`                                                       | the claims actually returned                   | Over-advertising triggers warnings                    |
| `token_endpoint_auth_methods_supported`                                  | `["client_secret_basic","client_secret_post"]` | Must cover both to certify Basic OP                   |
| `code_challenge_methods_supported`                                       | `["S256"]`                                     | Omit `plain` unless intentionally supported           |

## Approach

- Treat this as a **configuration/settings audit**, driven from the conformance profile (`T3`), not a
  handler rewrite. Override discoverable settings to the Basic/Config target values for the conformance
  run.
- **Remove `implicit`** (and any hybrid response types) from the conformance profile's
  `grant_types_supported` / `response_types_supported` until those flows are independently verified; add
  them back only when their profiles are attempted.
- Cross-check every advertised value against a real behavior: each `scope` resolvable, each
  `token_endpoint_auth_method` accepted at the token endpoint, the advertised `id_token` signing alg
  matching the active `jwks_uri` key, and `claims_supported` matching what the claims handlers actually
  emit.
- Where the OP intentionally does **not** support something the suite probes, that becomes a baseline
  entry (`expected-skips.json` / `expected-failures.json`) in `T1`, not an advertised capability.

## Work breakdown

1. Produce the conformance-profile settings overlay that sets each discoverable field to its
   Basic/Config target value.
2. Remove `implicit`/hybrid from advertised grant/response types for the conformance profile.
3. Verify each advertised value against actual behavior (scopes, auth methods, signing alg vs. jwks,
   claims).
4. Diff the live discovery document against the Appendix A checklist; record any intentional deviation as
   a `T1` baseline entry rather than silently advertising it.

## Acceptance criteria

- Every field in the live discovery document is backed by a verified capability (no over-advertising).
- `response_types_supported` and `grant_types_supported` contain only verified values (no `implicit`/
  hybrid for the initial profiles).
- `id_token_signing_alg_values_supported` matches the key served at `jwks_uri`;
  `token_endpoint_auth_methods_supported` covers both `client_secret_basic` and `client_secret_post`.
- The Config OP plan passes with no "advertised-but-unsupported" failures.

## Dependencies & links

- **Depends on** `T3` for the `issuer` byte-match; **pairs with** `T4` (advertised grants must match the
  seeded clients).
- Feeds `T1` (Config OP) and informs the `T1` baseline files for any intentional non-support.
- Field-by-field reference: [`conformance-testing.md`](../conformance-testing.md) Appendix A.
