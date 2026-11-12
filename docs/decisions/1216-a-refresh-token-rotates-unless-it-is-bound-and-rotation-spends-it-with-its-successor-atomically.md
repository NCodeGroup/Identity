# 1216. A refresh token rotates unless it is bound, and rotation spends it with its successor atomically

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The refresh token of a public client has to be sender-constrained or rotated
([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) §2.2.2 and §4.14.2, OAuth 2.1 §4.3.1). The refresh token of a
confidential client is bound to its client authentication (RFC 6749 §6), so it needs neither.

Rotation has a cost for the clients that do not need it. FAPI 2.0 §5.3.2.1-9 and its Note 1 forbid it for a
confidential client whose tokens are sender-constrained: it adds no security there, and a client that loses the response
of a rotation loses its session (issue [#268](https://github.com/NCodeGroup/identity/issues/268)).

Rotation also has to be atomic. A refresh presents a token and receives a successor, and a rotated token that is
presented again is a reuse ([ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)). If the
presented grant is revoked by an operation that does nothing when the grant is already revoked, and the successor is
stored in a later unit of work, two concurrent refreshes of one token can both receive tokens, so the reuse goes
undetected (RFC 9700 §4.14.2), or the save of the loser fails and it receives `500`. A lineage revocation that answers a
reuse can also run before the successor of the winner exists, which leaves that successor active (issue
[#260](https://github.com/NCodeGroup/identity/issues/260)).

## Decision

**A refresh token is rotated unless it is bound to a proof-of-possession key or to the authentication of a confidential
client, and a rotation spends the presented token and stores its successor in one unit of work.**

### Which tokens rotate

1. **The policy selects which refresh tokens rotate.** `refresh_token_rotation_policy` is a string setting that applies
   to clients, merges with `Replace`, is not discoverable, and takes `unbound` or `always` (`SettingRules.OneOf`).
   `unbound`, the default, rotates a refresh token that is bound neither to a proof-of-possession key nor to client
   authentication. The key is the confirmation that the grant stores: a DPoP `jkt` or a certificate `x5t#S256`
   (`TokenConfirmation.HasValue`). A client that is public has no client authentication. `always` rotates every refresh
   token. The constants are `OpenIdConstants.RefreshTokenRotationPolicy.Unbound` and `.Always`.
2. **Nothing turns off the rotation of a public client's unbound refresh token.**
   `RefreshTokenRotationRequirement.IsRequired(OpenIdClient, TokenConfirmation)` returns `true` for `always`, and
   otherwise `true` when the client is not confidential and the grant stores no confirmation. The refresh-token grant
   handler asks it for every refresh. It follows `CodeChallengeRequirement`, which always binds the authorization code
   of a public client to PKCE. No coherence rule
   ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)) accompanies it: no registration
   can make the behavior insecure, and whether a token is bound is known per token, not per registration.
3. **The default is `unbound`, not `always`.** FAPI 2.0 forbids the rotation that `always` would apply to a confidential
   client with sender-constrained tokens, and RFC 9700 requires rotation only for a public client's unbound token.
   `unbound` rotates exactly that token. Auth0 and Okta rotate the refresh tokens of single-page and native
   applications by default, which `unbound` matches for a public client without DPoP.
4. **The FAPI 2.0 profile defaults to `unbound`.** `Fapi2SecurityProfile.ApplyDefaultSettings` sets
   `refresh_token_rotation_policy` to `unbound`, so an `always` above the scope that selects the profile is not
   inherited by the clients of a FAPI 2.0 tenant, while that scope or a client may still choose `always`
   ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md) decision 7).
5. **The merge is `Replace`.** A child replaces the value of its parent, as `refresh_token_reuse_policy` does, so no
   tenant can force `always` on its clients through a lattice floor. The floor that matters is decision 2, and a profile
   default needs a setting that does not merge with a lattice operation
   ([ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md)).
6. **The domain is closed.** The value selects a behavior that the server enforces, so an unknown value fails closed
   ([ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md),
   [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)), like the sibling policies of the refresh token.

### How a rotation spends the token

7. **Rotation is one unit of work.** `ITokenService.RotateRefreshTokenAsync` mints the successor and stores it through
   `IPersistedGrantService.ReplaceOnceOrDefault`, which revokes the presented grant (setting `ConsumedWhen`) if it is
   still active and adds the successor in the same unit of work. A presented grant that is missing or not active,
   including one that a concurrent request spent first, stores nothing and returns `null`, and the token service
   audits no issued token.
8. **A spend without a successor revokes the token once.** When rotation applies but the refreshed scopes omit
   `offline_access`, no successor exists, and `IPersistedGrantService.TryRevokeOnceAsync` spends the presented token. It
   returns `false` when the grant is missing or is not active, including when a concurrent request spent it first
   ([ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md)).
9. **A lost spend is answered as a reuse.** Whether the spend ran through `ReplaceOnceOrDefault` or
   `TryRevokeOnceAsync`, the answer is the one for a revoked token that is presented again
   ([ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)). The server cannot tell the
   attacker from the client, so it revokes the token that is still active (RFC 9700 §4.14.2). The lineage revocation
   includes the successor of the winner, because that successor was committed together with the winning spend, and the
   failure of the loser follows that commit.
10. **The spend runs after every check and before any token is minted.** The grant handler validates the presented
    token, its binding and the request first, and spends the token before it creates an access token or an ID token, so
    a lost spend mints and audits no token.
11. **A spend reads again after a write that left the grant active.** `ReplaceOnceOrDefault` and `TryRevokeOnceAsync`
    read the grant again, for up to three attempts in total, when their save loses a race that left it active
    ([ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md)), and the
    last lost save propagates `StoreConcurrencyException`.
12. **A sliding-expiration update tolerates a race.** `IPersistedGrantService.UpdateExpirationAsync` keeps the change of
    the other request when its save loses a race, instead of throwing.

## Options considered

- **Default to `always`.** Rejected: it rotates the sender-constrained tokens of confidential clients, which FAPI 2.0
  forbids and which gain no security from it (decision 3).
- **A boolean flag that enforces the invariant.** Rejected: a `false` that still rotates the refresh token of a public
  client misnames the behavior, and a boolean cannot express the choice between the bound and the unbound token.
- **A coherence rule that refuses a public client without rotation.** Rejected: no registration can make the behavior
  insecure, and the binding of a refresh token is known per token (decision 2).
- **An `Or` floor.** Rejected: a floor binds every client below the scope that sets it, and a profile default cannot
  lower it, so a FAPI 2.0 tenant could not default its clients to `unbound` (decisions 4 and 5).
- **A grace interval for a concurrent loser as the default.** Rejected: RFC 9700 makes no exception for a concurrent
  request, and Auth0 (reuse interval) and Okta (grace period) offer the interval as an opt-in. It is an opt-in
  ([ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md)).
- **Mint, then spend.** Rejected: a lost spend would have minted and audited tokens that are never delivered.
- **Spend and store the successor in separate units of work.** Rejected: the lineage revocation of a loser can run
  between the two and miss the successor.
- **A conditional set-based update that spends the row.** Rejected for the reasons of
  [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md).
- **Chosen:** the `refresh_token_rotation_policy` setting with the default `unbound`, and a rotation that validates, then
  spends and stores the successor in one unit of work, then mints.

## Consequences

- A single-page or native client without DPoP rotates its refresh token by default. A confidential client and a client
  whose token is bound to a DPoP key or a certificate keep their refresh token.
- No tenant can force the rotation on its clients. A client chooses `always` for itself.
- Of two concurrent refreshes of one token, one receives tokens and the other receives `invalid_grant`, and under
  `revoke_all` the client must authenticate again.
- A client that retries a refresh after it lost the response is answered as a reuse, unless it retries within an opt-in
  reuse interval.
- `IPersistedGrantService` declares `TryRevokeOnceAsync` and `ReplaceOnceOrDefault`, and `ITokenService` declares
  `RotateRefreshTokenAsync`, so every implementation of either interface implements them.
- The Entity Framework InMemory provider checks no concurrency token, so the hermetic host cannot show the race; the
  unit tests cover it.
- This implements issues [#268](https://github.com/NCodeGroup/identity/issues/268) and
  [#260](https://github.com/NCodeGroup/identity/issues/260).

## References

- [ADR-1010](1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md) — the merge polarity of the lattice settings.
- [ADR-1217](1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md) — the reuse of a rotated token and
  the retry interval.
- [ADR-1148](1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md) — a profile supplies defaults for the
  settings that do not merge with a lattice operation.
- [ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md) and
  [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — a closed domain fails closed.
- [ADR-1012](1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) — the interceptor-managed concurrency tokens
  that detect the lost race, which surfaces as a `StoreConcurrencyException`.
- [ADR-1222](1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md) — a revocation retries a lost race.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) — OAuth 2.0 Security Best Current Practice (§2.2.2, §4.14.2).
- [FAPI 2.0 Security Profile](https://openid.net/specs/fapi-security-profile-2_0-final.html) (§5.3.2.1).
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken.RefreshTokenRotationRequirement`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken.DefaultRefreshTokenGrantHandler`,
  `NCode.Identity.OpenId.Authentication.Tokens.DefaultTokenService`,
  `NCode.Identity.OpenId.Authentication.Persistence.DefaultPersistedGrantService`,
  `NCode.Identity.OpenId.Settings.Fapi2SecurityProfile`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`.
