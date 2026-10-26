# 216. A refresh token is rotated unless it is sender-constrained

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

The refresh token of a public client has to be sender-constrained or rotated
([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) §2.2.2 and §4.14.2, OAuth 2.1 §4.3.1). The refresh token of a
confidential client is bound to its client authentication (RFC 6749 §6), so it needs neither.

`refresh_token_rotation_enabled` defaulted to `false` and merged as a floor with `Or`
([ADR-0038](0038-setting-merge-lattice-duals.md)). Nothing required rotation or a sender constraint for a client that is
not confidential, so a public client without DPoP received a reusable bearer refresh token.

Rotation also has a cost for the clients that do not need it. FAPI 2.0 §5.3.2.1-9 and its Note 1 forbid it for a
confidential client whose tokens are sender-constrained: it adds no security there, and a client that loses the response
of a rotation loses its session (issue [#268](https://github.com/NCodeGroup/Identity/issues/268)).

## Decision

**A refresh token is rotated unless it is bound to a proof-of-possession key or to the authentication of a confidential
client, and the `refresh_token_rotation_policy` setting replaces the `refresh_token_rotation_enabled` flag.**

1. **The policy selects which refresh tokens rotate.** `refresh_token_rotation_policy` is a string setting that applies
   to clients, merges with `Replace`, is not discoverable, and takes `unbound` or `always` (`SettingRules.OneOf`).
   `unbound`, the default, rotates a refresh token that is bound neither to a proof-of-possession key nor to client
   authentication. The key is the confirmation that the grant stores: a DPoP `jkt` or a certificate `x5t#S256`
   (`TokenConfirmation.HasValue`). A client that is public has no client authentication. `always` rotates every refresh
   token. The constants are `OpenIdConstants.RefreshTokenRotationPolicy.Unbound` and `.Always`. The setting replaces
   `refresh_token_rotation_enabled`, which is removed with its name, its key, and its public API entries.
2. **Nothing turns off the rotation of a public client's unbound refresh token.**
   `RefreshTokenRotationRequirement.IsRequired(OpenIdClient, TokenConfirmation)` returns `true` for `always`, and
   otherwise `true` when the client is not confidential and the grant stores no confirmation. The refresh-token grant
   handler asks it for every refresh. It follows `CodeChallengeRequirement`, which always binds the authorization code
   of a public client to PKCE. No coherence rule
   ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)) accompanies it: no registration
   can make the behavior insecure, and whether a token is bound is known per token, not per registration.
3. **The default is `unbound`, not `always`.** FAPI 2.0 forbids the rotation that `always` would apply to a confidential
   client with sender-constrained tokens, and RFC 9700 requires rotation only for a public client's unbound token.
   `unbound` rotates exactly that token. Auth0 and Okta rotate the refresh tokens of single-page and native
   applications by default, which `unbound` matches for a public client without DPoP.
4. **The FAPI 2.0 profile defaults to `unbound`.** `Fapi2SecurityProfile.ApplyDefaultSettings` sets
   `refresh_token_rotation_policy` to `unbound`, so an `always` at the server does not rotate the clients of a FAPI 2.0
   tenant unless that tenant or a client chooses `always`
   ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md) decision 7).
5. **The merge is `Replace`.** A child replaces the value of its parent, as `refresh_token_reuse_policy` does, so a
   tenant cannot force `always` on its clients through a floor. The floor that matters is decision 2, and a profile
   default needs a setting that does not merge with a lattice operation
   ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)).
6. **The domain is closed.** The value selects a behavior that the server enforces, so an unknown value fails closed
   ([ADR-0140](0140-setting-values-are-validated-against-their-domain.md),
   [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)), like the sibling policies of the refresh token.
7. **A migration renames the stored setting.** `RenameRefreshTokenRotationSetting` rewrites the settings documents of
   the `Servers`, `Tenants`, and `Clients` tables. `Up` turns a boolean `true` into the policy `always` and removes a
   `false`, because the default policy rotates the refresh token of a public client that is not sender-constrained, and
   no setting turns that off. `Down` turns the string `always` into `refresh_token_rotation_enabled` set to `true` and
   removes any other string, because the flag cannot express `unbound`. A document is rewritten only when it is a JSON
   object, the old key has the expected type, and the target key is absent, and every rewritten row receives a new
   `SettingsConcurrencyToken`.

## Options considered

- **Default to `always`.** Rejected: it rotates the sender-constrained tokens of confidential clients, which FAPI 2.0
  forbids and which gain no security from it (decision 3).
- **Keep the flag and enforce the invariant.** Rejected: a `false` that still rotates the refresh token of a public
  client misnames the behavior.
- **A coherence rule that refuses a public client without rotation.** Rejected: no registration can make the behavior
  insecure, and the binding of a refresh token is known per token (decision 2).
- **Keep the `Or` floor.** Rejected: a floor binds every client below the scope that sets it, and a profile default
  cannot lower it, so a FAPI 2.0 tenant could not default its clients to `unbound` (decisions 4 and 5).

## Consequences

- A single-page or native client without DPoP rotates its refresh token by default. A confidential client and a client
  whose token is bound to a DPoP key or a certificate keep their refresh token.
- A tenant cannot force the rotation on its clients. A client chooses `always` for itself.
- The previous release fails closed on a stored document that names `refresh_token_rotation_policy` until the rollout
  completes, because it does not know the setting
  ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md),
  [ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)).
- **Breaking:** `refresh_token_rotation_enabled` is removed with its name and key constants, and
  `refresh_token_rotation_policy` is added.
- This implements issue [#268](https://github.com/NCodeGroup/Identity/issues/268).

## References

- [ADR-0038](0038-setting-merge-lattice-duals.md) — the merge polarity that the flag used and the policy replaces.
- [ADR-0087](0087-refresh-token-reuse-detection-and-family-revocation.md) — the reuse detection of a rotated token.
- [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md) — a profile supplies defaults for the
  settings that do not merge with a lattice operation.
- [ADR-0140](0140-setting-values-are-validated-against-their-domain.md) and
  [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — a closed domain fails closed.
- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) and
  [ADR-0180](0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md) — the rollout of a
  renamed setting.
- [RFC 9700](https://www.rfc-editor.org/rfc/rfc9700) — OAuth 2.0 Security Best Current Practice (§2.2.2, §4.14.2).
- [FAPI 2.0 Security Profile](https://openid.net/specs/fapi-security-profile-2_0-final.html) (§5.3.2.1).
- Code: `NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken.RefreshTokenRotationRequirement`,
  `NCode.Identity.OpenId.Authentication.Endpoints.Token.RefreshToken.DefaultRefreshTokenGrantHandler`,
  `NCode.Identity.OpenId.Settings.Fapi2SecurityProfile`,
  `NCode.Identity.OpenId.Settings.DefaultSettingDescriptorDataSource`.
