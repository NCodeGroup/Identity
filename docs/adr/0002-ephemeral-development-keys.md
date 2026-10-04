# 2. Ephemeral development keys are an explicit opt-in, never a silent default

- **Status:** Accepted
- **Date:** 2026-09-27
- **Deciders:** NCode Group

## Context

The default registrations wire the OpenID pipeline but deliberately do not provide signing keys or
seed any stores. Consequently, a fresh environment has an empty tenant secret collection: the
`OpenIdTenant.SecretsProvider` is materialized from persisted secrets, and with none present the
collection is empty. This surfaced concretely while building integration tests — the JWKS endpoint
returned `{ "keys": [] }`, and (more importantly) token signing had no key, because
`DefaultTokenService` reads the signing key from `openIdContext.Tenant.SecretsProvider.Collection`, the
same source the JWKS endpoint publishes.

This raised the question: should the default registrations (or the sample host) ship a fully-valid,
runnable environment — generating local keys, in-memory stores, self-signed material — so the
off-the-shelf configuration "just works"?

## Decision

**The default registrations remain explicit and never silently create keys or seed stores.**
Auto-generating signing keys in the default path is rejected because it is a production footgun:

- keys that regenerate on every restart invalidate all previously issued tokens;
- multiple instances would each mint different keys, breaking validation;
- it hides a security-critical decision (key provenance and lifecycle) that operators must own.

Instead, a **fully-valid ephemeral environment is offered as an explicit, clearly-named opt-in** that
development and test hosts choose to apply. This mirrors the established industry pattern — Duende
IdentityServer's `AddDeveloperSigningCredential()` and OpenIddict's `AddDevelopment*Certificate()` are
opt-in conveniences, never defaults.

### Implementation

- `AddEphemeralDeveloperKeys()` replaces the static-single tenant provider with
  `EphemeralStaticSingleOpenIdTenantProvider`, which overrides `OpenIdTenantProvider.GetTenantSecretsAsync`
  to build a **static, in-memory** secret collection containing one generated `RSA` signing key
  (`use=sig`, `alg=RS256`) via `ISecretKeyFactory`, instead of loading persisted secrets.
- Because both token signing and the JWKS endpoint read the tenant's `SecretsProvider`, this single
  seed makes the whole server runnable (sign tokens, publish JWKS) with no configured or persisted
  secrets and no external dependencies.
- The Playground calls `AddEphemeralDeveloperKeys()`; the integration tests boot the Playground and
  therefore inherit the same fully-valid environment.

## Options considered

- **Auto-seed ephemeral keys in the default registration.** Rejected — the production footgun above.
- **Require operators to always supply keys, with no dev convenience.** Rejected — makes the sample and
  tests painful and hides the "how do I make this runnable?" answer.
- **Explicit opt-in composing an ephemeral environment (chosen).** Safe-by-default yet trivially
  runnable for development and testing via one deliberate call.

## Consequences

- The off-the-shelf server is safe-by-default (no silent keys) and still trivially runnable for
  dev/test with one explicit, discoverable call.
- Integration tests get a deterministic, dependency-free environment; the JWKS/token behavior can be
  asserted over the wire.
- The ephemeral key is regenerated whenever the tenant is (re)materialized and is never persisted —
  acceptable for development, unacceptable for production.

### Follow-ups

- Done: the opt-in now lives in the `NCode.Identity.Server` composition root (a development extension) so any
  host can consume it, rather than each app re-implementing it. The Playground is just the EXE host that wires it in.
- Provide first-class production key-management registration and guidance.
- A related latent issue was discovered while building the harness: the server/tenant
  "create-if-missing" path is **not idempotent under concurrency** (a duplicate-key race when two
  first-requests create the row simultaneously). This should be hardened (idempotent upsert or
  retry-on-conflict) and is tracked separately.

## References

- `NCode.Identity.Server/DevelopmentEnvironment/EphemeralOpenIdTenantFactory.cs`
- `NCode.Identity.Server/DevelopmentEnvironment/DeveloperKeysRegistration.cs`
- `NCode.Identity.OpenId.Playground/Startup.cs` (opt-in wired in by the EXE host)
- `NCode.Identity.OpenId.Authentication/Tokens/DefaultTokenService.cs` (signing key source = tenant secrets)
- `NCode.Identity.OpenId.IntegrationTests/` (asserts JWKS publishes the ephemeral signing key over HTTP)
