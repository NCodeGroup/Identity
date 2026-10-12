# 79. Extensible PKCE `code_challenge_method` transformations

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** NCode.Identity maintainers

## Context

PKCE verification at the token endpoint computed the expected `code_challenge` with a hardcoded `switch`
over `plain` and `S256` inside `DefaultValidateAuthorizationCodeGrantHandler`, with a
`// TODO: provide a way to allow custom code challenge methods`. There was no seam for a deployment to add
a transformation, and the only configurability was a single boolean setting
`allow_plain_code_challenge_method` — a special-case flag that could not express "support `S256` and some
future method but not `plain`," and whose supported set was **not advertised** in discovery metadata even
though [OpenID Connect Discovery / RFC 8414](https://datatracker.ietf.org/doc/html/rfc8414) defines
`code_challenge_methods_supported`.

## Decision

Replace the hardcoded switch with a **strategy collection** ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md))
and the single boolean with the standard discoverable set.

- **Strategy seam.** `ICodeChallengeMethod` (abstraction in
  `NCode.Identity.OpenId.Authentication.Abstractions`) exposes a `Method` name and
  `ComputeChallenge(codeVerifier)`. `ICodeChallengeMethodCollectionProvider` resolves a transformation by
  its `code_challenge_method` value. The default implementations `PlainCodeChallengeMethod` and
  `S256CodeChallengeMethod` are registered via `TryAddEnumerable`, so a deployment adds a method by
  registering another `ICodeChallengeMethod`. The verifier compares the computed challenge in constant
  time (`CryptographicOperations.FixedTimeEquals`).
- **Setting cutover (clean, per the pre-release posture — [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).**
  The boolean `allow_plain_code_challenge_method` is **removed** in favor of the collection setting
  `code_challenge_methods_supported` (default `["plain", "S256"]`, merge **Intersect** so a child tenant
  can only narrow, [ADR-0038](0038-setting-merge-lattice-duals.md)). Disallowing `plain` is expressed by
  removing it from the set; `code_challenge_methods_supported` is marked **discoverable**, so it is now
  advertised in the discovery document automatically.
- **Enforcement.** The authorization endpoint rejects a `code_challenge_method` outside the tenant's
  `code_challenge_methods_supported` (`NotSupported`). The token endpoint rejects a verifier whose method
  has no registered transformation (`invalid_grant`).

## Consequences

- Adding a PKCE transformation is a non-breaking DI registration plus a `code_challenge_methods_supported`
  entry — no edit to the verifier.
- The server advertises its true PKCE capability, improving conformance and RP interoperability.
- Operators gain finer control (an explicit method set) than the old single boolean, and the control is a
  ceiling that can only be narrowed down the tenant hierarchy.
- The default behavior is unchanged: both `plain` and `S256` remain supported out of the box.
