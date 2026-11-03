# 1391. A client secret that is only compared is stored as a one-way verifier

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Refines:** [project-template ADR-0146](project-template/0146-secrets-are-never-logged-compared-in-constant-time-and-shown-once.md)
- **Amends:** [ADR-1110](1110-shared-secret-vs-cryptographic-key-storage.md), [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md)

## Context

A client's shared secret, its `client_secret`, is stored as the UTF-8 bytes of its opaque string inside an `nce1.`
envelope under the tenant master key ([ADR-1110](1110-shared-secret-vs-cryptographic-key-storage.md),
[ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)). The envelope keeps a
database backup from revealing it, but the server can still read every secret back, and whoever holds the root key
reads them all. Template ADR-0146 rule 6 stores a credential the application only verifies as a hash, and ADR-1110
recorded that conflict ([#163](https://github.com/NCodeGroup/Identity/issues/163)).

The client-authentication methods use the secret in two ways:

- `client_secret_basic` and `client_secret_post` (RFC 6749 §2.3.1) only compare the presented secret with the stored
  one. A one-way verifier answers that comparison.
- `client_secret_jwt` (OpenID Connect Core §9) keys an HMAC from the UTF-8 octets of the secret. The server must hold
  the secret itself to verify the client's assertion, so the secret stays reversible, protected by a key held outside
  the store, as template ADR-0146 rule 6 allows for a secret the application uses itself.

The same key verifies an `HS256` request object a client signs with its secret (OpenID Connect Core §10.1); a signed
CIBA request is asymmetric only. A client that authenticates with a public-key method signs its request objects with
its key instead, and the security profiles exclude symmetric signing.

Secrets reach the store in two ways. The server generates one with at least 256 bits of entropy
(`SharedSecretPolicy.MinimumEntropyBits`, [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md))
for dynamic client registration and the management API. An operator supplies one for the bootstrap administrator, the
conformance clients and test seeds, and its entropy is unknown.

## Decision

A shared secret's stored form is chosen when the secret is issued, from the method its client is registered for then:
a method that uses the secret as a key keeps it reversible, and every other method stores only a one-way verifier.

1. **Two stored forms.** A verifier is a `symmetric` `PersistedSecret` with the encoding `verifier`
   (`SecretEncodings.Verifier`). Its value is the envelope of a self-describing verifier text, so the verifier is
   encrypted at rest like every other non-public encoding, and a backup without the root key holds not even the
   verifier. A reversible secret keeps the `symmetric_raw` encoding of ADR-1110. Both store the bit length of the
   secret's UTF-8 octets as `KeySizeBits`, so a rotation keeps the rotated secret's entropy either way.

2. **Registered verifier schemes, each named in the text.** The text is `$<scheme>$<parameters>`, so the scheme and its
   parameters can change for new secrets while every stored verifier keeps verifying. A scheme is an
   `ISecretVerifierScheme` that writes and reads its parameters, registered as a collection and looked up by name
   through `ISecretVerifierSchemeCatalog`, so the vocabulary is open
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)): a package adds a scheme, such as Argon2id or a
   FIPS-only one, by registering it, and a later registration under a name replaces the earlier one. A name is
   printable ASCII without `$`. The family registers two, named by `SecretVerifierSchemes`:
    - `$ncode-sha256$<digest>` for a secret the server generates. A SHA-256 digest of a secret with 256 bits of entropy
      cannot be searched, so no work factor is added and verification costs one hash.
    - `$ncode-pbkdf2-sha256$<iterations>$<salt>$<derived key>` for a secret someone supplies: the format and the
      parameters of the family's password hashes (`DefaultPasswordHasher`): PBKDF2-HMAC-SHA256, 600,000 iterations,
      a 16-byte random salt and a 32-byte derived key, all base64url-encoded. The iteration count is
      `Pbkdf2SecretVerifierOptions.Iterations`. The secret is hashed as the octets the client presents, not
      normalized as a password is, because a `client_secret` is an opaque string.

   `SharedSecretVerifierOptions.GeneratedSecretScheme` and `SuppliedSecretScheme` name the schemes new secrets use,
   `ncode-sha256` and `ncode-pbkdf2-sha256` by default. A scheme's own parameters are its own options. The options are
   validated as the host starts: each named scheme is registered, and the work factor and concurrency are at least 1.
   A stored verifier of a scheme that is not registered, or with invalid parameters, fails the client's resolution as
   an unreadable credential, which authenticates nothing.

3. **The method decides the form.** `ClientAuthenticationMethodDescriptor.UsesClientSecretAsKey` marks a method that
   keys from the secret; `client_secret_jwt` is the only such method the family registers, and a method a package
   contributes declares its own. `ISecretGenerator.GenerateSharedSecret` and `ImportSharedSecret` store a verifier;
   `GenerateReversibleSharedSecret` and `ImportReversibleSharedSecret` store the UTF-8 octets. Every site that issues
   a client's secret picks by the client's effective `token_endpoint_auth_method`: the management API's create and
   rotate, dynamic client registration and its update, the conformance seed, the test seeds, and the bootstrap
   administrator. The bootstrap administrator is seeded for `client_secret_basic`, so its configured secret is stored
   as a PBKDF2 verifier. Its secret is stored again at each start-up in the form the method the existing client is
   registered for then needs, so an operator who moves it to `client_secret_jwt` does not lose the break-glass
   credential; a client whose settings cannot be read gets the verifier its seeded method needs.

4. **Verification is constant-time.** `CommonClientAuthenticationHandler` compares the presented secret with each of
   the client's verifiers (`SecretVerifier.VerifyAsync`), then with each reversible secret it holds. A verifier compares
   fixed-length digests with `CryptographicOperations.FixedTimeEquals`; a reversible secret and the presented secret
   are both hashed with SHA-256 first, so neither comparison returns early on a length or a differing byte. The client
   is authenticated with the credential that matched, which `OpenIdConfidentialClient.CredentialId` names.

5. **The work factor is bounded.** Anyone who knows a `client_id` can make the server verify a presented secret before
   the client is authenticated, and the per-client rate limit applies only after authentication. A PBKDF2 verification
   therefore waits for a turn of one gate that every PBKDF2 verifier of the process shares, so at most
   `Pbkdf2SecretVerifierOptions.MaxConcurrency` derivations run at once, half the processors by default.
   `SecretVerifier.VerifyAsync` is asynchronous for that wait, which holds no thread and ends when the request is
   cancelled. Requests that present guesses for a supplied secret slow only the clients that hold supplied secrets,
   never the token endpoint as a whole; a generated secret's SHA-256 verification never waits. A Production host may
   not set fewer than 600,000 iterations (`Pbkdf2SecretVerifierOptions.MinimumProductionIterations`), the OWASP
   recommendation, which the server's production safety checks as the host starts.

6. **A verifier is not a key.** `ISecretSerializer.DeserializeVerifiers` reads verifiers, and `DeserializeSecrets`
   refuses one, so a verifier is never among a client's `SecretKeys`. `client_secret_jwt` and `HS256` request objects
   use only the reversible secrets. A client coherence rule refuses a symmetric
   `request_object_signing_alg_values_supported` value, such as `HS256`, that a client registers in its own settings
   while its effective method only compares its secret, so dynamic client registration answers
   `invalid_client_metadata` and the management API refuses the settings instead of accepting a client whose request
   objects can never be verified. A value the client inherits from its tenant is not judged.

7. **Changing the registered method.** The form is fixed when the secret is issued and never converted:
    - A client switched **to** `client_secret_jwt` needs a new secret: its verifiers authenticate no assertion until
      the management API issues one, which is then reversible. The management API's `SecretResource` and
      `CreatedSecretResource` carry `IsVerifier`, which the console shows on each secret, so the operator sees that
      the client's secrets cannot sign an assertion. A dynamic client registration update that switches a
      client to a method whose secret none of the client's unexpired shared secrets can serve issues a new
      `client_secret` in its response (RFC 7592 §2.2) and removes the client's other shared secrets, which the client
      discards.
    - A client switched **away** from `client_secret_jwt` keeps its reversible secret, which still authenticates a
      compared method. Its replacement on the next rotation is a verifier.

8. **Existing secrets cut over cleanly.** No migration converts stored secrets: the encoding is a column the schema
   already has, and a `symmetric_raw` secret stored before this decision authenticates as a reversible one until it is
   rotated or deleted. Nothing is shipped ([ADR-1025](1025-pre-release-posture-and-auth0-parity-plus.md)), so no
   deployment has to be rewritten.

9. **Shown once.** A generated secret is still revealed only in the response that issues it
   ([ADR-1050](1050-secret-material-revealed-once-on-create.md)); a verifier gives no read path anything to return.

## Options considered

- **Keep every secret reversible under the envelope.** Rejected: whoever holds the root key reads every secret,
  though nothing needs to read a secret that is only compared (template ADR-0146, rule 6).
- **PBKDF2 for every secret.** Rejected: a generated secret has 256 bits of entropy, so a work factor adds no
  protection and costs a password check on every token request.
- **SHA-256 for every secret.** Rejected: a supplied secret may be guessable, and an unsalted fast digest lets a
  stolen verifier be searched.
- **Store the verifier without the envelope.** Rejected: every non-public encoding is an envelope
  ([ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)), and the envelope
  keeps a supplied secret's verifier from being searched offline without the root key.
- **Convert a reversible secret to a verifier when its client leaves `client_secret_jwt`.** Rejected: whether the
  stored secret was generated or supplied, and so which scheme fits it, is not recorded; the rotation that retires it
  issues a verifier anyway.
- **Refuse a switch to `client_secret_jwt` while the client holds only verifiers.** Rejected: the operator would have
  to delete the client's secrets before switching and issue one after, and the switch already leaves the verifiers
  unable to sign an assertion.
- **Issue a reversible secret to a client whose request objects are signed with `HS256`.** Rejected: whether a
  client's request objects are symmetric is a list of permitted values that its tenant may also supply, so the form
  would depend on settings that change after the secret is issued; a client that signs with its secret registers
  `client_secret_jwt`, and the coherence rule says so.
- **Limit failed client authentications per `client_id` before verifying.** Rejected as the bound on the work factor:
  anyone who knows a `client_id`, such as the bootstrap administrator's, could lock that client out, while the gate
  bounds the processors without refusing a correct secret.
- **Cache a successful verification.** Rejected: it spares only a client that already authenticated, keeps a fast
  check of the secret in memory, and bounds nothing an attacker sends.
- **A closed set of schemes.** Rejected: another scheme, such as Argon2id for supplied secrets or a FIPS-only one, is a
  member a package other than this one could need ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).

## Consequences

- A database backup and the root key together yield no `client_secret` of a client that compares its secret.
- A client registered for `client_secret_basic` or `client_secret_post` cannot sign a request object with `HS256`,
  because its secret is no key, and registering such a value in its own settings is refused.
- Each authentication of a client with a supplied secret costs one PBKDF2 derivation per stored verifier, and waits for
  a turn when `Pbkdf2SecretVerifierOptions.MaxConcurrency` derivations already run; under a flood of guesses, such a
  client's own requests wait with the guesses. A host can raise the work factor, or lower it outside Production, with
  `Pbkdf2SecretVerifierOptions`.
- A client switched to `client_secret_jwt` through the management API fails authentication until a new secret is
  created for it, and each of its secrets reports `IsVerifier` until then.
- A method a package contributes that keys from the secret sets `UsesClientSecretAsKey`, or its clients are issued
  verifiers it cannot use.

## References

- [`ISecretGenerator`](../../src/NCode.Identity.Secrets.Persistence.Abstractions/ISecretGenerator.cs),
  [`SecretVerifierFormat`](../../src/NCode.Identity.Secrets.Persistence/Verifiers/SecretVerifierFormat.cs),
  [`Pbkdf2SecretVerifierScheme`](../../src/NCode.Identity.Secrets.Persistence/Verifiers/Pbkdf2SecretVerifierScheme.cs),
  [`SecretVerifier`](../../src/NCode.Identity.Secrets.Abstractions/Verifiers/SecretVerifier.cs),
  [`ISecretVerifierScheme`](../../src/NCode.Identity.Secrets.Abstractions/Verifiers/ISecretVerifierScheme.cs)
- [`CommonClientAuthenticationHandler`](../../src/NCode.Identity.OpenId.Authentication/Clients/Handlers/CommonClientAuthenticationHandler.cs),
  [`DefaultOpenIdClientResolver`](../../src/NCode.Identity.OpenId.Authentication/Clients/DefaultOpenIdClientResolver.cs)
- [`ClientSecretKeyCoherenceRule`](../../src/NCode.Identity.OpenId.Authentication/Clients/Coherence/ClientSecretKeyCoherenceRule.cs),
  [`ClientApiEndpointHandler`](../../src/NCode.Identity.OpenId.Management/Endpoints/Clients/ClientApiEndpointHandler.cs),
  [`DefaultClientRegistrationEndpointHandler`](../../src/NCode.Identity.OpenId.Authentication/Endpoints/Registration/DefaultClientRegistrationEndpointHandler.cs)
- [ADR-1110](1110-shared-secret-vs-cryptographic-key-storage.md),
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
  [ADR-1235](1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1320](1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md)
- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- Issue [#163](https://github.com/NCodeGroup/Identity/issues/163)
