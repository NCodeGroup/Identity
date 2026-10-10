# 0058. Asymmetric public key material is exposed through a dedicated read endpoint (JWK + PEM)

- **Status:** Accepted
- **Date:** 2026-10-10
- **Deciders:** NCode.Identity maintainers

## Context

[ADR-0011](0011-secret-management-api.md) makes every secret **retrieval** path metadata-only:
`SecretResource` exposes `Use`, `Algorithm`, `CreatedWhen`, `ExpiresWhen`, `SecretType`, and
`KeySizeBits`, but never the `EncodedValue`. [ADR-0050](0050-secret-material-revealed-once-on-create.md)
refines this with a single, create-only reveal of **symmetric** client-secret material, and explicitly
reserves "asymmetric **public**-material exposure" as a deliberate, additive future option (public
material only; private keys stay unreachable). This ADR adopts that option.

An asymmetric secret (`rsa`, `ecc`, or a certificate-backed key) has two halves. The **private** half
is sensitive and must never cross the API boundary — the metadata-only invariant guards it. The
**public** half is, by definition, publishable: the server already hands tenant signing keys to relying
parties through the JWKS endpoint (RFC 7517). Yet the management API had **no** read path that returns a
single secret's public material. An operator inspecting or wiring up a server/tenant/client signing key
in an admin UI had no way to obtain its public key to paste into a verifier, pin in a relying party, or
compare against a published JWKS entry.

The forces in tension:

- A public key is safe to return on **read** — unlike the private half, it is not a standing liability,
  so the metadata-only invariant (which exists to protect **private** material) does not need to cover it.
- Two distinct audiences want two distinct representations. A **programmatic** consumer wants a
  **JWK** (RFC 7517), the same shape the JWKS endpoint emits, so a management read lines up with
  discovery. A **human operator** in a presentation UI wants **PEM** (`-----BEGIN PUBLIC KEY-----`,
  SubjectPublicKeyInfo) — the near-universal copy-paste format that `openssl`, most crypto libraries,
  and JWT tooling accept directly.
- A **symmetric** secret has no public half at all; a public-key read against one is meaningless.
- The server already has a **single source of truth** for turning a `SecretKey` into a public JWK — the
  `IJsonWebKeyConverter` strategy collection the JWKS endpoint resolves. A second, bespoke JWK
  serializer would drift from it (curve names, member encoding).

## Decision

Add a dedicated, read-only **public-key sub-resource** on every secret owner that returns an asymmetric
secret's public material in **both** JWK and PEM form. Private material stays unreachable by
construction; symmetric secrets have no public-key sub-resource.

Concretely:

- **One endpoint per owner, addressed as a sub-resource of the secret.**
  `GET .../servers/{serverId}/secrets/{secretId}/public-key`,
  `GET .../tenants/{tenantId}/secrets/{secretId}/public-key`, and
  `GET .../clients/{clientId}/secrets/{secretId}/public-key` each return a `PublicKeyResource`. The
  public key is its own addressable resource, parallel to how JWKS is its own endpoint; it is **not**
  folded into `SecretResource`, so the single-secret `GET` and the secrets **list** stay metadata-only
  (ADR-0011 is unchanged on those paths).

- **Both representations are returned, each for its audience.** `PublicKeyResource` carries `SecretId`,
  `SecretType`, a `Jwk` (the RFC 7517 JSON Web Key, public members only, as a `JsonElement` — the same
  loose-JSON shape `ClientEffectiveSettingsResource.Settings` uses), and a `Pem`
  (SubjectPublicKeyInfo, `-----BEGIN PUBLIC KEY-----`). JWK aligns a management read with the JWKS
  discovery document; PEM is the copy-paste format an operator UI needs.

- **The JWK comes from the existing converter pipeline; only PEM is added.** The implementation resolves
  the registered `IJsonWebKeyConverter` strategy collection — the same one the JWKS endpoint uses — to
  produce the JWK, so the two surfaces cannot drift. PEM is derived from the deserialized
  `AsymmetricSecretKey`'s exported public key (`ExportSubjectPublicKeyInfoPem`). The private key is
  unprotected only transiently in memory, **after** authorization succeeds, and only its public half is
  emitted.

- **Symmetric secrets return `404 Not Found`.** A symmetric secret has no public-key sub-resource, so
  the endpoint reports it as absent — the SecretType is checked without unprotecting any material, and
  the deserialize/convert step runs only for asymmetric secrets after authorization.

- **Authorization is the same `Operations.Read` gate as reading the secret.** The public-key read is a
  read on a secret the caller can already read; it introduces no new scope. It is scoped by owner
  exactly as the sibling `GET secrets/{secretId}` is (server secret authorizes against the secret;
  tenant/client secrets authorize against the owning resource node).

## Options considered

- **A `PublicKey` field on `SecretResource` (returned on every `GET`/list).** Rejected: it bloats the
  shared read contract and the list response with material only sometimes wanted, and couples the
  metadata view to key export. A distinct sub-resource keeps `SecretResource` metadata-only and makes
  the public key an explicit, addressable fetch.

- **JWK only.** Rejected for the default: it is correct for programmatic/JWKS alignment but forces a UI
  or an `openssl` user to convert JWK→PEM themselves. PEM is the format a human pastes.

- **PEM only.** Rejected: it drops the JWK shape that lines a management read up with the JWKS discovery
  document and that a programmatic consumer expects, and it would not reuse the existing JWK converter.

- **A second, bespoke JWK serializer in the management layer.** Rejected: it would duplicate the
  `IJsonWebKeyConverter` logic (curve-name mapping, member encoding) and drift from the JWKS endpoint.
  Reusing the registered converter keeps one source of truth for "a `SecretKey`'s public JWK."

- **Return raw DER / an `X509Certificate`.** Rejected as the default surface: JWK and PEM cover the
  programmatic and human audiences; a raw-DER or certificate representation can be an additive option if
  a concrete need appears.

## Consequences

- An operator can read any asymmetric server/tenant/client secret's public key — as JWK for tooling and
  PEM for copy-paste — without ever exposing the private half, which remains unreachable by construction.
- `PublicKeyResource` is new public surface in `NCode.Identity.OpenId.Management.Abstractions`
  (`NCode.Identity.OpenId.Management.Contracts.Secrets`). It carries no private material, and no read
  path on any owner returns private material — ADR-0011's private-material invariant and ADR-0050's
  one-time symmetric reveal are both unchanged.
- The management public-key JWK and the JWKS endpoint share the `IJsonWebKeyConverter` pipeline, so the
  two cannot disagree; publishing a new asymmetric key type (a new converter) lights up both surfaces.
- The management implementation now depends, at runtime, on the JWK converters being registered (they
  are, whenever the authentication/JWKS endpoint is present — the standard server composition). A key
  type with no registered converter fails the public-key read with a clear error rather than emitting a
  partial result.
- Symmetric secrets report `404` on the public-key path, which is the honest answer — they have no
  public half.

## References

- [ADR-0011](0011-secret-management-api.md) — server-side generation and the metadata-only retrieval
  invariant this decision extends (public material only).
- [ADR-0050](0050-secret-material-revealed-once-on-create.md) — the one-time symmetric reveal that
  reserved asymmetric public-material exposure as the additive option adopted here.
- [ADR-0024](0024-control-plane-and-per-tenant-planes.md) — the server/tenant/client ownership planes the
  read scope follows.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the Auth0-parity-plus philosophy that
  frames a per-key public-material read as parity with familiar admin workflows.
- RFC 7517 (JSON Web Key) and RFC 7468 (PEM / SubjectPublicKeyInfo) — the two returned representations.
