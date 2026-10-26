# @ncode/app-conformance-runner

Headless OAuth 2.x / OpenID Connect **conformance probe runner** — Phase 3 of the spec-coverage harness
([ADR-0084](../../../../docs/adr/0084-spec-coverage-conformance-harness.md)).

Where the browser dashboard ([`apps/conformance`](../conformance)) verifies the CORS-reachable, credential-free
endpoints, this runner exercises the **confidential and server-initiated legs a browser cannot drive**:

- `client_credentials` grant (RFC 6749 §4.4)
- token **introspection** and **revocation** with client authentication (RFC 7662 / RFC 7009)
- **device-code** issuance + polling (RFC 8628)
- **CIBA** backchannel authentication (gap: [#41](https://github.com/NCodeGroup/Identity/issues/41))
- `private_key_jwt` client authentication (gap: [#54](https://github.com/NCodeGroup/Identity/issues/54) / [#161](https://github.com/NCodeGroup/Identity/issues/161) — needs a configured client key)
- **DPoP** sender-constrained tokens and **mTLS** (gap: [#45](https://github.com/NCodeGroup/Identity/issues/45))
- **back-channel logout** advertisement

Each probe reports `verified`, `failed`, or `skipped` (with an actionable reason — unadvertised capability, or a
missing credential). Unimplemented backend features surface as `skipped`, and light up automatically as they land, so
the runner is both a gap finder and a regression detector. It exits non-zero when any probe **fails**, so it is a
usable CI pre-flight for the external OpenID Foundation Conformance Suite (Phase 4 / [#124](https://github.com/NCodeGroup/Identity/issues/124)).

## Run

```sh
# from src/frontend
pnpm --filter @ncode/app-conformance-runner start -- --issuer https://localhost:5001

# with a confidential client (credentials from the environment, never the command line)
CONFORMANCE_CLIENT_ID=svc CONFORMANCE_CLIENT_SECRET=… \
  pnpm --filter @ncode/app-conformance-runner start -- --issuer https://localhost:5001 --json

# with asymmetric client keys for the private_key_jwt and DPoP legs (PEM or JWK)
pnpm --filter @ncode/app-conformance-runner start -- --issuer https://localhost:5001 \
  --public-client-id app --private-key ./client.pem --dpop-key ./dpop.pem
```

See `--help` for every option.

The asymmetric legs (`private_key_jwt`, DPoP) are driven by a real Node-`crypto` signer: supply a PEM- or JWK-encoded
private key via `--private-key` (the `private_key_jwt` client assertion, with `--public-client-id`) and/or `--dpop-key`
(the DPoP proof). The signing algorithm is inferred from the key — the curve for EC keys (`ES256`/`ES384`/`ES512`),
`EdDSA` for Ed25519/Ed448, and `PS256` for RSA — and can be overridden with `--private-key-alg` / `--dpop-key-alg`
(e.g. `RS256`). RSA keys must be ordinary `rsaEncryption` keys: an RSA-PSS-restricted (`id-RSASSA-PSS`) key is rejected
up front because its public key cannot be published as a JWK — `PS256`/`PS384`/`PS512` already sign an ordinary RSA key
with PSS padding. The DPoP proof embeds the key's public JWK in its header (RFC 9449 §4). These legs still `skip` until
the backend advertises the capability (`private_key_jwt`: [#54](https://github.com/NCodeGroup/Identity/issues/54) / [#161](https://github.com/NCodeGroup/Identity/issues/161); DPoP: [#45](https://github.com/NCodeGroup/Identity/issues/45)).

The process exits `0` when no probe fails, `1` when any probe fails, and `2` for invalid arguments or a run that could
not complete (for example, unreachable discovery metadata or an unloadable key).
