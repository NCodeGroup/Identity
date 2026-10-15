# @ncode/app-conformance-runner

Headless OAuth 2.x / OpenID Connect **conformance probe runner** — Phase 3 of the spec-coverage harness
([ADR-0084](../../../../docs/adr/0084-spec-coverage-conformance-harness.md)).

Where the browser dashboard ([`apps/conformance`](../conformance)) verifies the CORS-reachable, credential-free
endpoints, this runner exercises the **confidential and server-initiated legs a browser cannot drive**:

- `client_credentials` grant (RFC 6749 §4.4)
- token **introspection** and **revocation** with client authentication (RFC 7662 / RFC 7009)
- **device-code** issuance + polling (RFC 8628)
- **CIBA** backchannel authentication (gap: `F3`)
- `private_key_jwt` client authentication (gap: `F16` / `S3` — needs a configured client key)
- **DPoP** sender-constrained tokens and **mTLS** (gap: `F7`)
- **back-channel logout** advertisement

Each probe reports `verified`, `failed`, or `skipped` (with an actionable reason — unadvertised capability, or a
missing credential). Unimplemented backend features surface as `skipped`, and light up automatically as they land, so
the runner is both a gap finder and a regression detector. It exits non-zero when any probe **fails**, so it is a
usable CI pre-flight for the external OpenID Foundation Conformance Suite (Phase 4 / `T1`).

## Run

```sh
# from src/frontend
pnpm --filter @ncode/app-conformance-runner start -- --issuer https://localhost:5001

# with a confidential client (credentials from the environment, never the command line)
CONFORMANCE_CLIENT_ID=svc CONFORMANCE_CLIENT_SECRET=… \
  pnpm --filter @ncode/app-conformance-runner start -- --issuer https://localhost:5001 --json
```

See `--help` for every option.

> The asymmetric legs (`private_key_jwt`, DPoP) accept an injected signer so their logic is unit-tested; wiring a real
> Node-crypto signer from a PEM/JWK is a follow-up (`T7`) that lands when the backend advertises those capabilities.
