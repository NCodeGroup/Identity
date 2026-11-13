# 1382. The BFF's browser security headers and its client-key endpoint are each opt-in for the host

- **Status:** Accepted
- **Date:** 2026-11-02
- **Deciders:** NCode Group

## Context

A BFF host may need two things beyond its session and tokens
(issue [#396](https://github.com/NCodeGroup/identity/issues/396)):

- **Security headers for the frontend's pages.** The paved path serves the frontend and the BFF from one origin, so a
  script injected into the frontend calls the BFF with the session cookie. A Content-Security-Policy is the only
  control that limits such a script (RFC 10017 §6.1.4.1). The BFF does not write the frontend's pages: the
  application serves them, and only it knows which scripts, styles and origins they need.
- **A JWKS for `private_key_jwt`.** A BFF that authenticates to the OP with `private_key_jwt` (RFC 7523) registers its
  public keys with the OP. Registered by value, every key rotation needs a new registration. Registered as a
  `jwks_uri`, the OP reads the current keys at runtime
  ([ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md)),
  so the BFF rotates by adding a key to its key ring.

Four forces shape both:

- The OP's own pages each carry a strict policy that the server writes for them
  ([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).
  The BFF writes none of the frontend's pages: its endpoints answer JSON, redirects or no content, apart from the
  same-origin reload page. A policy for the frontend's pages has to be one the application can change.
- The client-authentication key ring also holds the private keys that decrypt encrypted ID tokens and UserInfo
  responses
  ([ADR-1280](1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md)).
  The OP needs their public halves to encrypt to the BFF.
- The DPoP key ring is different: a DPoP proof carries its own public key, so no party needs it in advance
  ([ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md)).
- `NCode.Identity.Bff` references only abstraction and foundation packages, never another capability's implementation
  ([ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).

## Decision

**Both are opt-in. `app.UseBffSecurityHeaders()` writes four headers from configuration, and a frontend's
`Jwks:Enabled` maps `GET /jwks` under its `PathPrefix`, which publishes the public keys of the frontend's
client-authentication key ring and nothing else.** Both are configured per frontend
([ADR-1442](1442-a-bff-serves-the-frontends-its-configuration-lists-each-selected-per-request-by-an-open-rule.md)).

1. **The security headers are a middleware the host adds.** `UseBffSecurityHeaders()` writes the selected frontend's
   `SecurityHeaders` on every response, or the strict `BffSecurityHeadersOptions` defaults for a request that selects
   no frontend:
   - `Content-Security-Policy`, by default `default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'`.
   - `X-Content-Type-Options: nosniff`, which is fixed.
   - `Referrer-Policy`, by default `same-origin`, so the frontend's URLs never leave the origin in a `Referer`.
   - `Cross-Origin-Opener-Policy`, by default `same-origin`, so a cross-origin window that the frontend opens, or that
     opens it, gets no handle on it.
2. **A header that the application sets wins.** The middleware writes in `OnStarting` with `TryAdd`, so an endpoint
   or a static-file handler that sets one of these headers keeps its own value. A page that needs a nonce or a hash in
   its policy sets the policy itself.
3. **The default policy names no `form-action`.** The logout form posts to the BFF, which redirects to the OP's
   end-session endpoint, and a browser may apply `form-action` to the redirects that follow a form submission. With
   `form-action 'self'`, logout would stop at the BFF. A host that knows its OP's origin can add
   `form-action 'self' <OP origin>` to the policy.
4. **The configured values are validated at start.** The options validator refuses a missing section, a blank value,
   and any character outside printable ASCII, so no configured value can break the header or start another one.
5. **The policy is one configured string, not an `HtmlPageSecurityPolicy`.** The BFF writes none of the frontend's
   pages, and the application owns them and their nonces. The BFF's only page, the same-origin reload page, sets its
   own strict `HtmlPageSecurityPolicy`
   ([ADR-1292](1292-the-bffs-cookie-authenticates-only-guarded-endpoints-which-refuse-cross-site-requests-and-reload-cookieless-navigations.md)),
   which `TryAdd` leaves in place.
6. **`GET /jwks` under the frontend's `PathPrefix` is mapped only when the frontend's `Jwks:Enabled` is `true`.** The
   default is `false`, so a BFF that authenticates with a client secret or a TLS certificate, or that registers its
   keys by value, exposes no key endpoint. When it is enabled, the validator requires `ClientAuthentication:KeyRing`.
7. **It publishes only the public keys of the client-authentication key ring.**
   - Each key is exported through its descriptor's `ExportPublicKey`, which yields only the public parameters. A
     symmetric key has no public half and is left out.
   - The ring's decryption keys are published with its signing keys, so the OP can encrypt ID tokens and UserInfo
     responses to the BFF.
   - The DPoP key ring is never published.
8. **The endpoint is anonymous, is never cached, and answers any origin.** It allows anonymous requests, answers
   `application/json; charset=utf-8` as the OP's JWKS endpoint does, and sends `Cache-Control: no-store` as every BFF
   endpoint does, so no shared cache holds a key set that a rotation has replaced. It also sends
   `Access-Control-Allow-Origin: *`. A key set of public keys, read without credentials, discloses nothing that a
   direct request would not, and the wildcard lets the OP's setup wizard read the console's keys from the operator's
   browser
   ([ADR-1512](1512-an-instance-is-set-up-once-through-a-one-time-setup-token-that-creates-a-root-global-administrator.md)).
   The response allows no credentials and no other header.
9. **The key set is built with the `NCode.Identity.Jwk` model library**
   ([ADR-1119](1119-a-di-free-jwk-package-models-keys-sets-and-thumbprints-and-key-types-register-their-rules.md)),
   which `NCode.Identity.Secrets.Abstractions` exposes through `ExportedPublicKey.Jwk`. It is a model, not an
   implementation, so the BFF references no implementation package.

## Options considered

- **Send the security headers from `UseBff()` by default.** Rejected: the policy depends on the frontend's scripts,
  styles and API origins, which the BFF cannot know. A default that breaks a frontend gets switched off as a whole.
- **Assign the headers, replacing the application's own, as the OP's page writer does.** Rejected: the OP owns the
  pages it writes. Here the application owns them, and a page with a nonce-based policy must be able to send it.
- **Build the policy from typed directives with `HtmlPageSecurityPolicy`.** Rejected: the application owns its pages
  and their nonces, so a host writes its policy as one string in configuration, and the validator still guards the
  header.
- **Default to `form-action 'self'`.** Rejected: it can stop the logout redirect to the OP, as decision 3 explains.
- **Map the JWKS endpoint always.** Rejected: most deployments do not use it, and a BFF should expose no endpoint it
  does not use.
- **Publish the DPoP key ring too.** Rejected: no party needs a DPoP key in advance, and publishing it would tie the
  BFF's DPoP-bound tokens to a published key for no gain.
- **Chosen:** both opt-in, as the Decision states.

## Consequences

- A host that calls `UseBffSecurityHeaders()` gets a strict policy for its frontend, and widens it in configuration
  for each origin the frontend loads from or calls.
- A frontend that uses inline scripts or styles must send its own policy, with a nonce or a hash, because the default
  admits neither.
- A BFF registered with its `jwks_uri` rotates its client-authentication and decryption keys by adding a key to its
  key ring. The OP sees the new key when it next reads the key set.
- The public halves of the BFF's decryption keys are public, as the OP needs them to be.
- A page on any origin can read the key set from a browser, and so learns only what a direct request would.
  The OP's setup wizard relies on that to register the console from its own origin.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md)
- [ADR-1106](1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md),
  [ADR-1119](1119-a-di-free-jwk-package-models-keys-sets-and-thumbprints-and-key-types-register-their-rules.md),
  [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  [ADR-1280](1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md),
  [ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md),
  [ADR-1442](1442-a-bff-serves-the-frontends-its-configuration-lists-each-selected-per-request-by-an-open-rule.md),
  [ADR-1500](1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md),
  [ADR-1512](1512-an-instance-is-set-up-once-through-a-one-time-setup-token-that-creates-a-root-global-administrator.md)
- RFC 7517, RFC 7523, RFC 10017 §6.1.4.1, W3C Content Security Policy Level 3
- `src/NCode.Identity.Bff/Hardening/`, `src/NCode.Identity.Bff/Endpoints/BffJwksHandler.cs`
- Issue [#396](https://github.com/NCodeGroup/identity/issues/396)
