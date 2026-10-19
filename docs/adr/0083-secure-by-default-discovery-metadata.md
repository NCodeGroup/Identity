# 83. Secure-by-default discovery metadata, overridable by operators

- **Status:** Accepted (amended by [ADR-0140](0140-setting-values-are-validated-against-their-domain.md): symmetric
  signing of a token that crosses a trust boundary is outside the signing settings' domain, so it cannot be re-enabled;
  and by [ADR-0141](0141-response-types-are-whole-values.md): `response_types_supported` lists whole response types,
  advertised as listed, so an operator re-enables one implicit or hybrid flow by listing exactly that flow)
- **Date:** 2026-10-12
- **Deciders:** NCode.Identity maintainers

## Context

The OpenID Provider's discovery document (`/.well-known/openid-configuration`) is data-driven from the
discoverable server settings ([`DefaultDiscoverMetadataHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Discovery/Handlers/DefaultDiscoverMetadataHandler.cs)),
and those same settings are enforced at request time
([`DefaultValidateAuthorizationRequestHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Authorization/Handlers/DefaultValidateAuthorizationRequestHandler.cs)).
The advertised ceiling and the enforced ceiling therefore share one source of truth.

Several defaults over-advertised capabilities that modern guidance (the OAuth 2.0 Security BCP
[RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700) and the OAuth 2.1 direction) removes or discourages:

- `grant_types_supported` included `implicit`, and `response_types_supported` was `["code", "id_token", "token"]`
  — the response-type formatter expands that into **all seven** combinations, i.e. the implicit (`id_token`,
  `token`, `id_token token`) **and** hybrid (`code id_token`, `code token`, `code id_token token`) front-channel-token
  flows.
- `code_challenge_methods_supported` advertised `plain` in addition to `S256`.
- `subject_types_supported` had no default, so it was omitted from discovery even though OpenID Connect
  Discovery defines it as a required metadata field.
- `id_token_signing_alg_values_supported` / `userinfo_signing_alg_values_supported` /
  `access_token_signing_alg_values_supported` advertised **every** registered signature algorithm, including the
  symmetric `HS256`/`HS384`/`HS512` — tokens that cross a trust boundary must not be symmetrically signed.

This was a discovery over-advertisement audit (backlog item `T5`): the conformance Config OP profile fails on
anything advertised that the OP will not deliver, and advertising deprecated/insecure flows contradicts the
project's secure-by-default posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

## Decision

**Ship opinionated, secure defaults; keep every capability operator-overridable through configuration.** The
root settings pipeline already applies host configuration **last** and `Set`-overwrites
([`RootSettingsCollectionDataSource`](../../src/NCode.Identity.OpenId.Core/Settings/RootSettingsCollectionDataSource.cs)),
so lowering a default does not remove the capability — an operator re-widens it in `appsettings.json`. No new
mechanism is introduced.

- **Authorization-code only by default.** [`DefaultSettingsProvider`](../../src/NCode.Identity.OpenId.Core/Settings/DefaultSettingsProvider.cs)
  now seeds `grant_types_supported = ["authorization_code", "refresh_token", urn:…:device_code]` and
  `response_types_supported = ["code"]`. The implicit/hybrid authorization code paths still exist (the ticket
  handler can emit `token`/`id_token` responses), so they are **fail-closed** by default — rejected as
  `unsupported_response_type` — and an operator re-enables them only by widening the settings.
- **PKCE `S256` only by default.** The `code_challenge_methods_supported` default drops `plain`
  ([ADR-0079](0079-extensible-pkce-code-challenge-methods.md) made this a discoverable, narrowable set).
- **`subject_types_supported = ["public", "pairwise"]`** is now a default so the required discovery field is present;
  both subject types are implemented ([ADR-0093](0093-pairwise-subject-identifiers.md)).
- **Cross-boundary token signing is asymmetric-only.** `LoadSupportedAlgorithms` partitions registered signature
  algorithms and advertises **only** asymmetric ones (those whose `KeyType` derives from `AsymmetricSecretKey`)
  for `id_token`, `userinfo`, and `access_token` signing. `request_object_signing_alg_values_supported` and
  `token_endpoint_auth_signing_alg_values_supported` keep the full set, because `client_secret_jwt` legitimately
  uses a symmetric key on the client→server leg.
- **host no longer over-advertises.** The host `appsettings.json` grant-types override (which
  re-added `implicit`) is removed.

## Consequences

- The out-of-the-box discovery document advertises only verified, modern-secure capabilities, so a default
  deployment is secure-by-default and closer to OpenID Foundation Basic/Config OP conformance.
- Because advertisement and enforcement share one source, a deployment that widens a setting to re-enable a
  legacy flow automatically both advertises **and** accepts it — there is no second switch to forget.
- A tenant that relied on `HS256` id-token signing will no longer have it selected automatically; this is the
  intended secure outcome, and it remains available by explicitly widening the signing-alg setting via
  configuration.
- The implicit/hybrid handler code is retained but unreachable by default; fully removing it is tracked
  separately rather than done here.
