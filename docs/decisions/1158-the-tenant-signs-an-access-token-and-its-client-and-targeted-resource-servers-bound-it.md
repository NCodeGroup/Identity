# 1158. The tenant signs an access token, and its client and targeted resource servers bound it

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

An access token is requested by a client and read by the resource servers it targets, its audiences
([ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md)).
Both carry a settings document: a client's effective settings cascade from the platform through the tenant to the
client, and a resource server's from the platform through the tenant to the resource server
([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)). The two are
siblings in the cascade, so neither document is merged into the other.

A resource server is also a secret owner: it can hold an HS256 shared secret as well as a JWE key. That raises the
question of whether the server should ever sign an access token with a resource server's key. RFC 9068 contemplates
only asymmetric signing by the authorization server, validated through its JWKS, and the FAPI security profiles permit
only `PS256` and `ES256` and forbid symmetric signing. A resource server that held a token-signing key could forge its
own tokens. The one piece of per-audience material that belongs to the
audience boundary is encryption: encrypting an access token to a resource server's key, so that only it can read the
token, cannot be expressed as encrypting to the tenant. A compact JWE has exactly one recipient, so a token that
targets several resource servers needs a rule for whose key applies.

Four forces shape the decision:

- **A client's bound must apply to every token.** `access_token_encryption_required`, or narrowed
  `access_token_encryption_{alg,enc}_values_supported`, set on the client or by the ceiling of a security profile it
  selects, binds the token whatever it targets, because a profile's ceiling binds every scope below the one that
  selects it
  ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
- **A resource server must be able to constrain more than encryption.** It requires a sender-constrained token,
  shortens the token's lifetime, or limits its signing algorithms. Auth0 exposes all three per API, and RFC 9728 lets a
  protected resource state `dpop_bound_access_tokens_required`. A resource server selects no security profile
  ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)), so these are
  resource-server settings that combine with the client's.
- **The encryption recipient must not depend on where a requirement came from.** A token that the client requires to be
  encrypted must reach a targeted resource server that decrypts its own tokens in a form it can read. RFC 9068 §4 has a
  resource server decrypt with the keys and algorithms that it specified, so a token encrypted to the tenant's key
  because the client's list excludes the resource server's keys is a token that resource server cannot decrypt.
- **FAPI 2.0's sender-constraint rule needs a setting, not a requirement object.** A setting that says "the token must
  be bound" lets the profile state the rule as a ceiling
  ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).

## Decision

**The tenant signs every access token. The token is bound by its client and by every resource server it targets: their
settings combine by their lattice merges, every requirement among them holds, and the token's credentials are chosen
within every bound, or the request is refused. Nothing is silently weakened or redirected.**

1. **Resolution at issuance.** `IResourceServerCredentialResolver` resolves every enabled resource server whose scopes
   intersect the token's effective scopes, narrowed by the requested resources
   ([ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md)).
   Each is a `ResolvedResourceServer` with its effective settings, `IsSystem`, `Scopes` and a `SecretsProvider` over its
   stored keys. The `ResourceServerCredentialResolution` is `IAsyncDisposable`, and `DefaultTokenService` disposes it
   after issuance so the per-request key material is released.
2. **The tenant signs.** An access token is always signed with the tenant's keys, within the combined
   `access_token_signing_alg_values_supported`. A resource server's stored key never signs one: per-audience key
   material serves encryption only (RFC 9068, FAPI).
3. **Siblings combine; a cascade merges.** `IReadOnlySettingCollection.Combine` joins the settings of two sibling scopes
   that govern one operation. Every setting it contributes must merge with a lattice operation of `SettingMerge`
   (`SettingDescriptor.HasLatticeMerge`) or it throws, so the result does not depend on the order of the collections
   and binds both. `Merge` stays the parent-to-child cascade, where a projection (`Replace`, `Keep`) is meaningful.
   Because a lattice merge is idempotent, the platform and tenant values that both siblings inherit are not counted
   twice: (P ∩ T ∩ C) ∩ (P ∩ T ∩ R) = P ∩ T ∩ C ∩ R.
4. **The settings that govern an access token** are the client's effective settings combined with, for every targeted
   resource server, its effective settings that apply within the `resource_server` scope (`AppliesWithin`). Every
   audience of a multi-audience token binds it, and issuance reads every access-token setting from this one collection.
   A setting that applies within resource servers merges with a lattice operation; a composed-server test pins the
   invariant for every registered descriptor, and `Combine` fails closed for an extension that breaks it.
5. **Resource servers bound the tokens issued for them.**

   | Setting                                              | Merge       | Applies to                |
   | ---------------------------------------------------- | ----------- | ------------------------- |
   | `access_token_encryption_required`                   | `Or`        | clients, resource servers |
   | `access_token_encryption_{alg,enc}_values_supported` | `Intersect` | clients, resource servers |
   | `access_token_lifetime`                              | `Min`       | clients, resource servers |
   | `access_token_signing_alg_values_supported`          | `Intersect` | clients, resource servers |
   | `access_token_sender_constraint_required`            | `Or`        | clients, resource servers |
   | `access_token_confirmation_methods_supported`        | `Intersect` | resource servers          |

   `access_token_sender_constraint_required` requires the token to be bound to a DPoP proof key (RFC 9449) or a
   mutual-TLS client certificate (RFC 8705). `access_token_confirmation_methods_supported` names the `cnf` methods a
   resource server verifies, `jkt` and `x5t#S256` from the IANA JWT Confirmation Methods registry, and is unset by
   default, meaning every method the server binds with. A required sender constraint is met only by a binding whose
   method every targeted resource server verifies.
6. **Preference order is the platform's.** `Intersect` keeps its left operand's order, so every effective list keeps the
   order of the platform's root list down the whole cascade, including profile ceilings, and the client is the left
   operand of the combination. A resource server states a preference by narrowing a list, not by reordering it.
7. **The sender constraint is enforced twice.** The token endpoint refuses a request when the client's effective
   settings require a sender-constrained token and the request presents no binding, before a code is redeemed or a
   refresh token rotated. Issuance enforces the combined requirement and confirmation methods, which only it can know,
   because it alone knows the targeted resource servers. FAPI 2.0 sets `access_token_sender_constraint_required` as a
   ceiling.
8. **An encrypted access token is encrypted to the party that reads it.** A targeted resource server decrypts its own
   tokens when it holds a key it can decrypt with under its own effective settings. A system resource server never
   decrypts its own tokens ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)). When the
   combined settings require encryption:
   - a token for one such resource server is encrypted to its key, chosen within the combined bounds; when none of its
     keys qualifies, issuance fails rather than encrypt to a key it cannot read;
   - a token for several resource servers, any of which decrypts its own tokens, is refused, because a compact JWE has
     one recipient;
   - any other token, with no targeted resource server or none that decrypts its own tokens, is encrypted to the
     tenant's key, and its resource servers introspect it.
9. **Refusals name their cause.** A refusal the request can avoid is `invalid_request`: a missing or unverifiable
   sender constraint, or an encrypted token for several resource servers one of which decrypts its own tokens. A
   configuration that cannot be satisfied is `server_error` (`500`), as for a client's registered algorithms
   ([ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)): no tenant key within the
   permitted signing or encryption algorithms, or no key of a resource server that decrypts its own tokens within the
   combined settings.
10. **Policy lives with issuance; the resolver supplies data.** The public `IAccessTokenPolicyService`, with the
    internal default `DefaultAccessTokenPolicyService`, combines the settings, enforces the requirements and chooses
    the credentials, and returns them as an `AccessTokenPolicy`. `DefaultTokenService` issues the token under it. The
    policy service is a public seam beside `ITokenService`: a replacement token service resolves it rather than
    reproduce or skip the policy, and an extension that bounds access tokens by a further scope replaces it without
    replacing the token service.

## Options considered

- **Sign with a resource server's key.** Rejected: that resource server could forge tokens, a symmetric signing key
  across a trust boundary is forbidden by FAPI, and it has no JWKS rotation story.
- **Use one scope's settings in place of the other's.** Rejected: either choice leaves the other scope's bounds, and
  the profiles selected above it, without effect.
- **Combine with `Merge`.** Rejected: a projection would let the order of the collections decide the value, and a
  `Replace`-merged client setting that the resource server inherits from its tenant would overwrite the client's.
- **Combine inside the resolver.** Rejected: the resolver is a public replacement point, and the policy must not depend
  on every replacement reimplementing it.
- **Keep the policy service internal to the default token service.** Rejected: `ITokenService` is a public replacement
  point, so a replacement would have to reimplement the policy or silently issue tokens outside the client's and
  resource servers' bounds, and an extension could not add a bound without replacing the token service. `ITokenService`,
  the resolver, its resolution and `SecurityTokenContext`, which carries the same credentials, are already public; only
  the default implementation is internal.
- **Fall back to the tenant's key when the client's bounds exclude the resource server's keys.** Rejected: it issues a
  token the resource server cannot decrypt, which RFC 9068 §4 rules out for a resource server that holds keys.
- **Refuse every multi-audience encrypted token when any resource server's own setting requires encryption.** Rejected:
  the outcome would depend on which scope stated the requirement, and the guard would admit a client-required token that
  a resource server decrypting its own tokens cannot read.
- **A sender-constraint requirement without confirmation methods.** Rejected: a token bound by a method a resource
  server does not verify passes as compliant and is, to that resource server, a bearer token.
- **Auth0's single `mechanism` per API.** Rejected in favor of a list: a resource server can verify both DPoP and
  mutual TLS, and the list combines across audiences by `Intersect`.
- **A separate FAPI requirement object beside the setting.** Rejected: one rule would be stated twice, and a ceiling
  binds every scope below the one that selects the profile by construction.
- **Let a resource server select security profiles.** Rejected: every constraint it needs is one of the settings above.
- **A registered contributor collection, so other scopes could bound an access token.** Rejected: there is no second
  contributor and no defined way to resolve one per request. `Combine` is the reusable piece.

## Consequences

- A client's bounds and the ceilings of the profiles it selects bind every access token, whatever it targets. A
  resource server requires sender-constrained tokens, caps their lifetime, limits their signing algorithms and states
  which confirmation methods it verifies. The management API's resource-server settings and effective-settings preview
  accept and show these settings.
- Issuance-time refusals come after a grant is spent. Codes are single-use anyway. A refresh token carries its access
  token's confirmation, so a refused refresh is one whose policy changed after it was issued. The client-level sender
  constraint is still checked at the token endpoint, before anything is spent.
- Every targeted resource server's keys are deserialized per token request and released with the resolution.
- `Combine` throws for a setting without a lattice merge, so an extension cannot weaken a bound by registering one.
- The server reads the access tokens it encrypted under one policy
  ([ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).

## References

- [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §4 and §6; [RFC 9449](https://www.rfc-editor.org/rfc/rfc9449);
  [RFC 8705](https://www.rfc-editor.org/rfc/rfc8705); [RFC 7800](https://www.rfc-editor.org/rfc/rfc7800) and the IANA
  JWT Confirmation Methods registry; [RFC 7662](https://www.rfc-editor.org/rfc/rfc7662);
  [RFC 9728](https://www.rfc-editor.org/rfc/rfc9728) §2; FAPI 2.0 Security Profile.
- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md):
  the merge lattice.
  [ADR-1026](1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md):
  resource servers and their audiences.
  [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md):
  profile ceilings.
  [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md): the scopes a
  setting applies to.
  [ADR-1155](1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md): `server_error` for an unsatisfiable
  configuration.
  [ADR-1167](1167-the-server-validates-the-tokens-it-issued-under-one-policy.md): a system resource server never
  decrypts its own tokens.
- Code: `NCode.Identity.Settings.IReadOnlySettingCollection.Combine`, `DefaultSettingCollection`,
  `NCode.Identity.OpenId.Authentication.Tokens.IAccessTokenPolicyService`, `DefaultAccessTokenPolicyService`,
  `Tokens.Models.AccessTokenPolicy`, `DefaultTokenService`, `IResourceServerCredentialResolver`,
  `ResourceServers.DefaultResourceServerCredentialResolver`, `ResolvedResourceServer`,
  `ResourceServerCredentialResolution`, `Tokens.ProofOfPossession.DefaultProofOfPossessionService`,
  `NCode.Identity.OpenId.Settings.Fapi2SecurityProfile`, `DefaultSettingDescriptorDataSource`, `OpenIdSettingRules`.
