# 1382. The BFF sends browser security headers and publishes its client keys only when the host opts in

- **Status:** Accepted (decisions 1 and 5 amended by
  [ADR-1432](1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md): the security headers
  and the JWKS endpoint are per frontend, and the keys it names, such as Bff:Jwks:Enabled and
  Bff:ClientAuthentication:KeyRing, are configured under Bff:Frontends:{name})
- **Date:** 2026-11-02
- **Deciders:** NCode Group

## Context

Phase 4 of the [design](../backend-for-frontend.md) (issue [#396](https://github.com/NCodeGroup/Identity/issues/396))
adds two things a BFF host may need beyond its session and tokens:

- **Security headers for the frontend's pages.** The paved path serves the frontend and the BFF from one origin
  (design §5.9), so a script injected into the frontend calls the BFF with the session cookie. A
  Content-Security-Policy is the only control that limits such a script (RFC 10017 §6.1.4.1). But the BFF does not
  write the frontend's pages: the application serves them, and only it knows which scripts, styles and origins they
  need.
- **A JWKS for `private_key_jwt`.** A BFF that authenticates to the OP with `private_key_jwt` (RFC 7523) registers its
  public keys with the OP. Registered by value, every key rotation needs a new registration. Registered as a
  `jwks_uri`, the OP reads the current keys at runtime
  ([ADR-1106](1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md)), so the BFF rotates by adding a
  key to its key ring.

Four forces shape both:

- The OP's own pages each carry a strict policy that the server writes for them
  ([ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md)).
  The BFF writes no HTML: its endpoints answer JSON, redirects or no content. A policy for the frontend's pages has to
  be one the application can change.
- The client-authentication key ring, `Bff:ClientAuthentication:KeyRing`, also holds the private keys that decrypt
  encrypted ID tokens and UserInfo responses
  ([ADR-1280](1280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md)). The OP needs their public
  halves to encrypt to the BFF.
- The DPoP key ring, `Bff:DPoP:KeyRing`, is different: a DPoP proof carries its own public key, so no party needs it
  in advance ([ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md)).
- `NCode.Identity.Bff` references only abstraction packages
  ([ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md)).

## Decision

**Both are opt-in. `app.UseBffSecurityHeaders()` writes four headers from configuration, and `Bff:Jwks:Enabled` maps
`GET /bff/jwks`, which publishes the public keys of the client-authentication key ring and nothing else.**

1. **The security headers are a middleware the host adds.** `UseBffSecurityHeaders()` writes these headers on every
   response, from the `Bff:SecurityHeaders` configuration section:
   - `Content-Security-Policy`, by default `default-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'`.
   - `X-Content-Type-Options: nosniff`, which is fixed.
   - `Referrer-Policy`, by default `same-origin`, so the frontend's URLs never leave the origin in a `Referer`.
   - `Cross-Origin-Opener-Policy`, by default `same-origin`, so a cross-origin window that the frontend opens, or that
     opens it, gets no handle on it.
2. **A header that the application sets wins.** The middleware writes in `OnStarting` with `TryAdd`, so an endpoint
   or a static-file handler that sets one of these headers keeps its own value. A page that needs a nonce or a hash in
   its policy sets the policy itself.
3. **The default policy names no `form-action`.** The logout form posts to `/bff/logout`, which redirects to the OP's
   end-session endpoint, and a browser may apply `form-action` to the redirects that follow a form submission. With
   `form-action 'self'`, logout would stop at the BFF. A host that knows its OP's origin can add
   `form-action 'self' <OP origin>` to the policy.
4. **The configured values are validated at startup.** The options validator refuses a missing section, a blank
   value, and any character outside printable ASCII, so no configured value can break the header or start another one.
5. **`/bff/jwks` is mapped only when `Bff:Jwks:Enabled` is `true`.** The default is `false`, so a BFF that
   authenticates with a client secret or a TLS certificate, or that registers its keys by value, exposes no key
   endpoint. When it is enabled, the validator requires `Bff:ClientAuthentication:KeyRing`.
6. **It publishes only the public keys of the client-authentication key ring.**
   - Each key is exported through its descriptor's `ExportPublicKey`, which yields only the public parameters. A
     symmetric key has no public half and is left out.
   - The ring's decryption keys are published with its signing keys, so the OP can encrypt ID tokens and UserInfo
     responses to the BFF.
   - The DPoP key ring is never published.
7. **The endpoint is anonymous and is never cached.** It sits in the `/bff` group, which allows anonymous requests,
   answers `application/json; charset=utf-8` as the OP's JWKS endpoint does, and sends `Cache-Control: no-store` as
   every `/bff` endpoint does, so no shared cache holds a key set that a rotation has replaced.
8. **The key set is built with the `NCode.Identity.Jwk` model library**
   ([ADR-1119](1119-first-class-jwk-jwks-and-thumbprint.md)), which `NCode.Identity.Secrets.Abstractions` already
   exposes through `ExportedPublicKey.Jwk`. It is a model, not an implementation, so the BFF still references no
   implementation package, and its dependency test allows it by name.

## Alternatives considered

- **Send the security headers from `UseBff()` by default.** Rejected: the policy depends on the frontend's scripts,
  styles and API origins, which the BFF cannot know. A default that breaks a frontend gets switched off as a whole.
- **Assign the headers, replacing the application's own, as ADR-1256's writer does.** Rejected: ADR-1256 owns the
  pages it writes. Here the application owns them, and a page with a nonce-based policy must be able to send it.
- **Build the policy from typed directives with `HtmlPageSecurityPolicy`.** Rejected: that model lives in
  `NCode.Identity.OpenId.Authentication.Abstractions`, which the BFF does not reference. A host writes its policy as
  one string in configuration, and the validator still guards the header.
- **Default to `form-action 'self'`.** Rejected: it can stop the logout redirect to the OP, as decision 3 explains.
- **Map `/bff/jwks` always.** Rejected: most deployments do not use it, and a BFF should expose no endpoint it does
  not use.
- **Publish the DPoP key ring too.** Rejected: no party needs a DPoP key in advance, and publishing it would tie the
  BFF's DPoP-bound tokens to a published key for no gain.

## Consequences

- A host that calls `UseBffSecurityHeaders()` gets a strict policy for its frontend, and widens it in configuration
  for each origin the frontend loads from or calls.
- A frontend that uses inline scripts or styles must send its own policy, with a nonce or a hash, because the default
  admits neither.
- A BFF registered with its `jwks_uri` rotates its client-authentication and decryption keys by adding a key to its
  key ring. The OP sees the new key when it next reads the key set.
- The public halves of the BFF's decryption keys are public, as the OP needs them to be.

## References

- [`docs/backend-for-frontend.md`](../backend-for-frontend.md) §5.7 and §5.9
- [ADR-1016](1016-implementation-packages-depend-only-on-abstractions.md),
  [ADR-1106](1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md),
  [ADR-1119](1119-first-class-jwk-jwks-and-thumbprint.md),
  [ADR-1256](1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  [ADR-1280](1280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md),
  [ADR-1286](1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md)
- RFC 7517, RFC 7523, RFC 10017 §6.1.4.1, W3C Content Security Policy Level 3
- `src/NCode.Identity.Bff/Hardening/`, `src/NCode.Identity.Bff/Endpoints/BffJwksHandler.cs`
- Issue [#396](https://github.com/NCodeGroup/Identity/issues/396)
