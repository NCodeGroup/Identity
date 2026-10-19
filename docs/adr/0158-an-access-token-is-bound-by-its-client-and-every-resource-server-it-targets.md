# 158. An access token is bound by its client and every resource server it targets

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

An access token is requested by a client and read by the resource servers it targets — its audiences
([ADR-0027](0027-access-token-audience-from-resource-servers.md)). Both carry a settings document: a client's
effective settings cascade `server → tenant → client`, and a resource server's `server → tenant → resource server`
([ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)). The two are siblings in the cascade, so neither
document is merged into the other.

Access-token issuance read every setting from the client's effective settings, with one exception: when the token
targeted exactly one resource server, its encryption came from that resource server's effective settings **instead
of** the client's ([ADR-0075](0075-runtime-consumption-of-resource-server-secrets.md)). That left four gaps:

- **A client's bound stopped applying to encryption.** `access_token_encryption_required`, or narrowed
  `access_token_encryption_{alg,enc}_values_supported` — set on the client or by the ceiling of a security profile it
  selects — did not bind once a resource server was targeted, although a profile's ceiling binds every scope below the
  one that selects it ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)).
- **A resource server could constrain nothing but encryption.** It could not require a sender-constrained token,
  shorten the token's lifetime, or limit its signing algorithms. Auth0 exposes all three per API, and RFC 9728 lets a
  protected resource state `dpop_bound_access_tokens_required`. ADR-0149 rejected letting a resource server select a
  security profile, and said these must be resource-server settings combined with the client's.
- **The encryption recipient depended on where a requirement came from.** A token for several resource servers was
  refused when one of them required encryption, even one with no key of its own, which received a tenant-encrypted
  token when it was targeted alone. A token the **client** required to be encrypted went to the tenant's key, even when
  a targeted resource server decrypts its own tokens and could not read it. Once the client's narrowing applies, the
  "prefer the resource server's key, fall back to the tenant's" rule would also encrypt to the tenant's key whenever the
  client's list excludes the resource server's keys — a token that resource server cannot decrypt. RFC 9068 §4 has a
  resource server decrypt with "the keys and algorithms that the resource server specified".
- **FAPI 2.0's sender-constraint rule was a requirement object,** because no setting expressed "the token must be
  bound" ([ADR-0109](0109-fapi-behavioral-rules-and-discovery-validation-details.md)).

## Decision

**An access token is bound by its client and by every resource server it targets. Their settings combine by their
lattice merges, every requirement among them holds, and the token's credentials are chosen within every bound — or
the request is refused. Nothing is silently weakened or redirected.**

1. **Siblings combine; a cascade merges.** `IReadOnlySettingCollection.Combine` joins the settings of two sibling
   scopes that govern one operation. Every setting it contributes must merge with a lattice operation of
   `SettingMerge` (`SettingDescriptor.HasLatticeMerge`) or it throws, so the result does not depend on the order of the
   collections and binds both. `Merge` stays the parent-to-child cascade, where a projection (`Replace`, `Keep`) is
   meaningful. Because a lattice merge is idempotent, the server and tenant values both siblings inherit are not
   counted twice: (S ∩ T ∩ C) ∩ (S ∩ T ∩ R) = S ∩ T ∩ C ∩ R.
2. **The settings that govern an access token** are the client's effective settings combined with, for every targeted
   resource server, its effective settings that apply within the `resource_server` scope (`AppliesWithin`). Every
   audience of a multi-audience token binds it. Issuance reads every access-token setting from this one collection.
   A setting that applies within resource servers merges with a lattice operation; a composed-server test pins the
   invariant for every registered descriptor, and `Combine` fails closed for an extension that breaks it.
3. **Resource servers can bound the tokens issued for them.**

   | Setting                                                 | Merge       | Applies to               |
   | ------------------------------------------------------- | ----------- | ------------------------ |
   | `access_token_encryption_required`                      | `Or`        | clients, resource servers |
   | `access_token_encryption_{alg,enc}_values_supported`    | `Intersect` | clients, resource servers |
   | `access_token_lifetime`                                 | `Min`       | clients, resource servers |
   | `access_token_signing_alg_values_supported`             | `Intersect` | clients, resource servers |
   | `access_token_sender_constraint_required`               | `Or`        | clients, resource servers |
   | `access_token_confirmation_methods_supported`           | `Intersect` | resource servers          |

   `access_token_sender_constraint_required` requires the token to be bound to a DPoP proof key (RFC 9449) or a
   mutual-TLS client certificate (RFC 8705). `access_token_confirmation_methods_supported` names the `cnf` methods a
   resource server verifies — `jkt` and `x5t#S256`, from the IANA JWT Confirmation Methods registry — and is unset by
   default, meaning every method the server binds with. A required sender constraint is met only by a binding whose
   method every targeted resource server verifies.
4. **Preference order is the server's.** `Intersect` keeps its left operand's order, so every effective list keeps
   the order of the server's root list down the whole cascade, including profile ceilings; the client is the left
   operand of the combination. A resource server states a preference by **narrowing** a list, not by reordering it.
5. **The sender constraint is enforced twice.** The token endpoint refuses a request when the **client's** effective
   settings require a sender-constrained token and the request presents no binding — before a code is redeemed or a
   refresh token rotated. Issuance enforces the **combined** requirement and confirmation methods, which only it can
   know, because it alone knows the targeted resource servers. FAPI 2.0 sets
   `access_token_sender_constraint_required = true` as a ceiling, and the `SenderConstrainedAccessTokenRequirement`
   object is removed.
6. **An encrypted access token is encrypted to the party that reads it.** A targeted resource server **decrypts its own
   tokens** when it holds a key it can decrypt with under its own effective settings — ADR-0075's opt-in. When the
   combined settings require encryption:
   - a token for one such resource server is encrypted to its key, chosen within the combined settings; when none of
     its keys qualifies, issuance fails rather than encrypt to a key it cannot read;
   - a token for several resource servers, any of which decrypts its own tokens, is refused, because a compact JWE has
     one recipient;
   - any other token — no targeted resource server, or none that decrypts its own tokens — is encrypted to the tenant's
     key, and its resource servers introspect it.

   With no bound from the client, a token for a single resource server is encrypted exactly as ADR-0075 described.
7. **Refusals name their cause.** A refusal the request can avoid is `invalid_request`: a missing or unverifiable
   sender constraint, or an encrypted token for several resource servers one of which decrypts its own tokens. A
   configuration that cannot be satisfied is `server_error`, as for a client's registered algorithms
   ([ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)): no tenant key for the permitted
   signing or encryption algorithms, or no key of a resource server that decrypts its own tokens within the combined
   settings.
8. **Policy lives with issuance; the resolver supplies data.** `IResourceServerCredentialResolver` resolves the
   targeted resource servers with their effective settings and their own keys (each `ResolvedResourceServer` carries a
   `SecretsProvider` the resolution owns and disposes). `IAccessTokenPolicyService` combines the settings, enforces the
   requirements, and chooses the credentials, and returns them as an `AccessTokenPolicy`; `DefaultTokenService` issues
   the token under it. The policy service is a public seam beside `ITokenService`, with an internal default: a
   replacement token service resolves it rather than reproduce — or skip — the policy, and an extension that bounds
   access tokens by a further scope replaces it without replacing the token service.

## Options considered

- **Keep the resource server's settings in place of the client's, or the client's in place of the resource
  server's.** Rejected: either one leaves the other scope's bounds, and the profiles selected above it, without
  effect.
- **Combine with `Merge`.** Rejected: a projection would let the order of the collections decide the value, and a
  `Replace`-merged client setting inherited by the resource server from its tenant would overwrite the client's.
- **Combine inside the resolver.** Rejected: the resolver is a public replacement point, and the policy must not
  depend on every replacement reimplementing it.
- **Keep the policy service internal to the default token service.** Rejected: `ITokenService` is a public
  replacement point, so a replacement would have to reimplement the policy or silently issue tokens outside the
  client's and resource servers' bounds, and an extension could not add a bound without replacing the token service.
  `ITokenService`, the resolver, its resolution, and `SecurityTokenContext` — which carries the same credentials — are
  already public; only the default implementation stays internal.
- **Keep the tenant-key fallback when the client's bounds exclude the resource server's keys.** Rejected: it issues a
  token the resource server cannot decrypt, which RFC 9068 §4 rules out for a resource server that holds keys.
- **Keep refusing a multi-audience token when any resource server's own setting requires encryption.** Rejected: the
  outcome would depend on which scope stated the requirement, and the guard would admit a client-required token that a
  resource server decrypting its own tokens cannot read.
- **A sender-constraint requirement without confirmation methods.** Rejected: a token bound by a method a resource
  server does not verify passes as compliant and is, to that resource server, a bearer token.
- **Auth0's single `mechanism` per API.** Rejected in favor of a list: a resource server can verify both DPoP and
  mutual TLS, and the list combines across audiences by `Intersect`.
- **Keep FAPI's requirement object alongside the setting.** Rejected: one rule would be stated twice, and a ceiling
  binds every scope below the one that selects the profile by construction.
- **Let a resource server select security profiles.** Rejected by ADR-0149; every constraint it needs is one of the
  settings above.
- **A registered contributor collection, so other scopes (an organization) could bound an access token.** Deferred:
  there is no second contributor and no defined way to resolve one per request. `Combine` is the reusable piece.

## Consequences

- A client's bounds and the ceilings of the profiles it selects bind every access token, whatever it targets. A
  resource server can require sender-constrained tokens, cap their lifetime, limit their signing algorithms, and state
  which confirmation methods it verifies. The management API's resource-server settings and effective-settings preview
  accept and show the widened settings; no stored document needs migrating.
- **Behavior changes (breaking):**
  - a client's encryption requirement now encrypts a single-audience token;
  - a multi-audience encrypted token whose resource servers hold no keys is now encrypted to the tenant's key rather
    than refused;
  - a client-required encrypted token targeting several resource servers, one of which decrypts its own tokens, is now
    refused rather than encrypted to the tenant's key;
  - a missing access-token signing or encryption key is now `server_error` rather than an unhandled
    `JoseCredentialsNotFoundException`.
- Issuance-time refusals come after a grant is spent. Codes are single-use anyway. A refresh token carries its access
  token's confirmation, so a refused refresh is one whose policy changed after it was issued. The client-level sender
  constraint is still checked at the token endpoint, before anything is spent.
- Every targeted resource server's keys are deserialized per token request; in practice only resource servers that
  opted into per-audience encryption hold keys.
- **Breaking surface:** `IReadOnlySettingCollection.Combine`; `ResolvedResourceServer.SecretsProvider`; the removed
  `ResourceServerCredentialResolution.SingleSecretsProvider`; the new `IAccessTokenPolicyService` and
  `AccessTokenPolicy`; the two new setting names and keys.
- The server's own readers of access tokens — UserInfo, introspection, token exchange, the management API — do not yet
  decrypt the tokens it encrypts (backlog `F67`).

## References

- [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §4 and §6; [RFC 9449](https://www.rfc-editor.org/rfc/rfc9449);
  [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705); [RFC 7800](https://www.rfc-editor.org/rfc/rfc7800) and the IANA
  JWT Confirmation Methods registry; [RFC 9728](https://www.rfc-editor.org/rfc/rfc9728) §2; FAPI 2.0 Security Profile.
- [ADR-0075](0075-runtime-consumption-of-resource-server-secrets.md) (amended: settings combine, and the recipient
  rule); [ADR-0109](0109-fapi-behavioral-rules-and-discovery-validation-details.md) (amended: the sender constraint is
  a ceiling); [ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md) (extended: the settings that apply to
  resource servers); [ADR-0038](0038-setting-merge-lattice-duals.md) — the merge lattice;
  [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md) — profile ceilings;
  [ADR-0155](0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) — `server_error` for an
  unsatisfiable configuration.
- Code: `NCode.Identity.Settings.IReadOnlySettingCollection.Combine`, `DefaultSettingCollection`,
  `NCode.Identity.OpenId.Authentication.Tokens.IAccessTokenPolicyService`, `DefaultAccessTokenPolicyService`,
  `Tokens.Models.AccessTokenPolicy`, `DefaultTokenService`, `ResourceServers.DefaultResourceServerCredentialResolver`,
  `ResolvedResourceServer`, `ResourceServerCredentialResolution`, `Tokens.ProofOfPossession.DefaultProofOfPossessionService`,
  `NCode.Identity.OpenId.Settings.Fapi2SecurityProfile`, `DefaultSettingDescriptorDataSource`, `OpenIdSettingRules`.
