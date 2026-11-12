# 1391. A client secret is a one-way verifier unless its method keys from its UTF-8 octets

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0146](project-template/0146-secrets-are-never-logged-compared-in-constant-time-and-shown-once.md)

## Context

The family holds two kinds of symmetric secret, and they differ in what is done with them.

A tenant's or resource server's symmetric secret is a cryptographic key: raw CSPRNG bytes that are revealed as base64url
(the JWK `k` member). A client's symmetric secret is its `client_secret`, a URL-safe random string whose UTF-8 octets
client authentication compares or keys from. The two are not interchangeable: UTF8(base64url(raw)) is not the raw bytes,
so one generation path for both would hand a keying method the wrong key.

What a server must keep of a client secret follows from how the client's authentication method uses it. A method that
only compares the presented secret (`client_secret_basic`, `client_secret_post`) never needs the secret again, so storing
it reversibly only adds a way to leak it. A method that keys from the secret (`client_secret_jwt`, which also verifies
`HS256` request objects) needs the octets themselves. Project-template ADR-0146 says a secret that is only verified is
stored as a hash and compared in constant time; this record applies that to client secrets and keeps the keying methods
working.

A work-factor hash costs CPU on every authentication, so verification is also a denial-of-service surface that needs a
bound. RFC 6749 section 5.2 answers a failed client authentication with `invalid_client`; a server that cannot afford the
check answers that it is temporarily unavailable, which is not a statement about the credential.

## Decision

**A client secret is stored as a one-way verifier unless its authentication method keys from its UTF-8 octets, in which
case it is stored as reversible octets.**

### Two kinds of secret

- A client's symmetric secret is always its `client_secret`.
- A tenant's or resource server's symmetric secret is a cryptographic key, generated through
  `ISecretGenerator.GenerateSecret` as raw CSPRNG bytes and revealed as base64url (JWK `k`).

### Stored forms

- A method that keys from the secret keeps `symmetric_raw` octets.
- Every other method holds only a `SecretEncodings.Verifier`, the envelope of a verifier text. Like every non-public
  encoding it is encrypted at rest under the key hierarchy of
  [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md).
- Both forms store the octets' bit length as `KeySizeBits`, and every `PersistedSecret` records its `Origin`
  (`SecretOrigins.Generated` or `SecretOrigins.Supplied`).

### Verifier text and schemes

- The verifier text is `$<scheme>$<parameters>`.
- An `ISecretVerifierScheme` registers into `ISecretVerifierSchemeCatalog`. The catalog is open: a later registration of
  a name replaces the earlier one, and a name is printable ASCII without `$`.
- The family's `SecretVerifierSchemes` are `ncode-sha256` for generated secrets, whose entropy makes a fast hash
  sufficient, and `ncode-pbkdf2-sha256` for supplied ones: 600,000 iterations, a 16-byte salt, a 32-byte key, and the
  octets not normalized.
- `SharedSecretVerifierOptions.GeneratedSecretScheme` and `SuppliedSecretScheme`, and
  `Pbkdf2SecretVerifierOptions.Iterations`, are validated at start. Production refuses fewer than
  `MinimumProductionIterations` (600,000).
- An unregistered or malformed stored verifier authenticates nothing.

### Creating secrets

- `GenerateSharedSecretVerifier`, `ImportSharedSecretVerifier`, `GenerateReversibleSharedSecret`,
  `ImportReversibleSharedSecret` and `ConvertToSharedSecretVerifier` each take one `SharedSecretRequest` (`EntropyBits`).
- Generate and import share one construction, so a seeded secret is stored in the same shape as a generated one.

### Methods declare their use of the secret

- `ClientAuthenticationMethodDescriptor.RequiresClientSecret` and `UsesClientSecretAsKey` are `required`.
- The catalog refuses a method that keys from a secret it does not require.
- `ClientSecretKeyCoherenceRule` refuses a symmetric `request_object_signing_alg_values_supported` in the own settings of
  a client whose method compares.

### Issuing

`IClientSecretIssuer` resolves a client's effective `token_endpoint_auth_method` and issues a secret in that method's
form; when the method cannot be resolved it issues a verifier. Its callers are management create (a symmetric secret;
an asymmetric one goes to `GenerateSecret`), rotate and settings update, DCR register and update, the seeds, and the
bootstrap administrator, whose secret is stored again on each start.

Every stored client names its method in its own settings, and a server or tenant change never moves it.
`IClientSecretIssuer.ReconcileClientSecrets` runs on every DCR update and on a management update that changes the
method. It converts reversible secrets to verifiers by their `Origin`, or issues a new secret that the DCR response or a
management `200` `CreatedSecretResource` reveals (`204` otherwise). It rewrites nothing when the method cannot be
resolved. The management API's answer to a lost race is
[ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md)'s.

A rotated secret overlaps its replacement as
[ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md) says.
`SecretResource` and `CreatedSecretResource` carry `IsVerifier`, which the management console shows.

### Authenticating

- `ISecretSerializer.DeserializeVerifiers` reads verifiers; `DeserializeSecrets` refuses them.
- `CommonClientAuthenticationHandler` checks a comparing method against verifiers only and a keying method against
  reversible secrets only. It checks verifiers without a work factor first (`SecretVerifier.HasWorkFactor`), compares
  with `FixedTimeEquals`, and names the match in `OpenIdConfidentialClient.CredentialId`.

### Bounding the work factor

- Work-factor verifications share one `ISecretVerificationGate`, configured by `SecretVerificationGateOptions`:
  `MaxConcurrency` is half the processors, `MaxQueueLength` is 16 and `QueueTimeout` is 2 seconds.
- A refusal is `SecretVerificationResult.Unavailable`. It is answered `503` `temporarily_unavailable` with
  `Retry-After: 1` and `no-store`, never `invalid_client`, because the server did not judge the credential.
- Failures are also limited per remote address, as
  [ADR-1399](1399-failed-client-authentications-are-limited-per-remote-address-by-default.md) says.

## Options considered

- **One generation path for keys and secrets.** Rejected: UTF8(base64url(raw)) is not raw, so a key made for one use is
  the wrong key for the other.
- **Everything reversible.** Rejected: a comparing method never needs the secret again, and a reversible store is a
  store that can leak every secret.
- **One scheme for all verifiers.** Rejected: a generated secret has the entropy for a fast hash, and a supplied one
  needs a work factor.
- **No envelope around the verifier text.** Rejected: a verifier would not be encrypted at rest like every other
  non-public encoding.
- **Converting inheriting clients when a server or tenant default changes.** Rejected: every client names its method in
  its own settings, so an inherited change never moves it.
- **Comparing reversible secrets too.** Rejected: a comparing method checks verifiers only, so a reversible secret never
  authenticates it.
- **Refusing a method switch.** Rejected: reconciliation converts or issues secrets, so a switch is safe.
- **A gate per scheme.** Rejected: the work-factor cost competes for the same processors, so one shared gate bounds it.
- **Caching verification results.** Rejected: a cache of accepted secrets is a store of them.
- **Closed schemes.** Rejected: the catalog is open so a package adds a scheme by registering it, as
  [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md) does
  for key types.
- **Chosen:** a verifier unless the method keys from the octets, in open schemes, behind one shared gate.

## Consequences

- A leaked database discloses no comparing client's secret, and a keying client's octets are protected by the key
  hierarchy.
- A work-factor attack is bounded: excess verifications are refused with `503` instead of exhausting the processors, and
  a client can retry after a second.
- Switching a client's method keeps its credential usable: reconciliation converts or reveals a secret once.
- A package adds a verifier scheme by registering it, and a deployment tunes the generated and supplied schemes and the
  gate through options validated at start.
- Smells: a client authentication handler that compares a reversible secret for a comparing method; a stored verifier
  whose scheme is not registered; `invalid_client` for a refused verification.
- Tests cover each scheme, the catalog's refusal of an inconsistent method, the handler's choice of stored form,
  reconciliation on a method change, and the gate's `503` answer.

## References

- `ISecretGenerator`, `SharedSecretRequest`, `SecretEncodings.Verifier`, `SecretOrigins`, `ISecretVerifierScheme`,
  `ISecretVerifierSchemeCatalog`, `SecretVerifierSchemes`, `SharedSecretVerifierOptions`, `Pbkdf2SecretVerifierOptions`
- `IClientSecretIssuer`, `ClientAuthenticationMethodDescriptor`, `ClientSecretKeyCoherenceRule`,
  `CommonClientAuthenticationHandler`, `ISecretVerificationGate`, `SecretVerificationGateOptions`
- [project-template ADR-0146](project-template/0146-secrets-are-never-logged-compared-in-constant-time-and-shown-once.md)
- [ADR-1235](1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md),
  [ADR-1399](1399-failed-client-authentications-are-limited-per-remote-address-by-default.md),
  [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md),
  [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)
- RFC 6749 section 5.2 (`invalid_client`)
