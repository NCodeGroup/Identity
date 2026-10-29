# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to adhere to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versioning is stamped in lockstep across the family by
Nerdbank.GitVersioning ([ADR-0003](docs/decisions/0003-central-package-management-and-lockstep-versioning.md)); a breaking
change to the public API is a **major** version bump.

## [Unreleased]

### Added

- **Dynamic client registration accepts back-channel logout metadata.** `RegisterClientRequest` and
  `RegisterClientResponse` carry `BackchannelLogoutUri` and `BackchannelLogoutSessionRequired`, stored as the
  `backchannel_logout_uri` and `backchannel_logout_session_required` client settings, so a self-registered client
  receives logout tokens; the URI must be an absolute https URI without a fragment
  ([ADR-0063](docs/decisions/0063-dynamic-client-registration.md),
  issue [#389](https://github.com/NCodeGroup/Identity/issues/389)).
- **The OpenID protocol client binds tokens with DPoP.** `ClientRegistration.DPoPKeyRing` names the proof key ring,
  and `DPoPBoundAccessTokens` makes DPoP required. Token requests carry proofs and require a `DPoP` token type, and
  authorization requests send `dpop_jkt`. A `use_dpop_nonce` challenge is retried once, with nonces cached per origin.
  `TokenRequest`, `TokenResult` and `AuthorizationRequestResult` carry `DPoPJwkThumbprint`, and `IDPoPProofFactory`
  creates resource proofs. `OpenIdProviderMetadata.DpopSigningAlgValuesSupported` is renamed
  `DPoPSigningAlgValuesSupported`
  ([ADR-0286](docs/decisions/0286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md),
  issue [#123](https://github.com/NCodeGroup/Identity/issues/123)).

- **A JSON Web Key Set types the keys of contributed key types.** The constructor
  `JsonWebKeySet(JsonElement, IEnumerable<JsonWebKeyTypeDescriptor>)` and
  `JsonWebKeySet.Parse(string, IEnumerable<JsonWebKeyTypeDescriptor>)` build each key whose `kty` names a supplied
  descriptor with that descriptor, so a contributed key read from a set is thumbprinted and screened for private
  members by its own rules; pass the `JwkKeyType` of each `ISecretKeyTypeCatalog.Descriptors` entry that has one (it is
  `null` for a key type with no JSON Web Key form). A set deserialized through `System.Text.Json` uses the standard
  descriptors only
  ([ADR-0281](docs/decisions/0281-the-jwk-package-takes-contributed-key-types-and-hash-names-from-its-caller.md),
  issue [#329](https://github.com/NCodeGroup/Identity/issues/329)).

- **A JWK Thumbprint URI names every registered hash the framework computes.** `JsonWebKey.ComputeThumbprintUri`
  takes the hash's name from the IANA Named Information Hash Algorithm Registry (RFC 9278 §4) and adds SHA3-256,
  SHA3-384 and SHA3-512 (`sha3-256`, `sha3-384`, `sha3-512`) to SHA-256, SHA-384 and SHA-512. The new
  `ComputeThumbprintUri(HashAlgorithmName, string)` takes the registry name of any other hash; a truncated hash name
  is never produced
  ([ADR-0281](docs/decisions/0281-the-jwk-package-takes-contributed-key-types-and-hash-names-from-its-caller.md),
  issue [#328](https://github.com/NCodeGroup/Identity/issues/328)).

- **Key wrappers share one decoder for base64url ciphertext.** `WrappedKeyCiphertext.GetByteCount` and
  `WrappedKeyCiphertext.Decode` in `NCode.Identity.Secrets.KeyWrapping` decode a `WrappedKey.Ciphertext` stored as
  base64url into a caller-supplied span or `IBufferWriter<byte>` and throw `CryptographicException` for empty or
  malformed text. The AES-GCM, AWS KMS, Azure Key Vault and Google Cloud KMS wrappers use it, so every wrapper reports
  empty ciphertext as "The wrapped key is empty." `NCode.Identity.Secrets.Abstractions` now references
  `NCode.Base64Url`
  ([ADR-0279](docs/decisions/0279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md),
  issue [#373](https://github.com/NCodeGroup/Identity/issues/373)).

- **The OpenID protocol client decrypts encrypted ID tokens and logout tokens.**
  `ClientRegistration.IdTokenKeyManagementAlgorithms` and `IdTokenContentEncryptionAlgorithms` mirror the registered
  `id_token_encrypted_response_alg` and `id_token_encrypted_response_enc` (`enc` defaults to `A128CBC-HS256`). When they
  are set, `IIdTokenValidator` and `ILogoutTokenValidator` decrypt with the asymmetric keys of the client's `KeyRing`,
  accept only RSA-OAEP and ECDH-ES and no compression, require every token to be encrypted, and check a replicated
  `iss` header. An undecryptable token fails with the new `TokenValidationFailureCodes.DecryptionFailed`
  (`decryption_failed`), and a client that registers no encryption refuses an encrypted token with
  `algorithm_not_permitted`
  ([ADR-0280](docs/decisions/0280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md), issue [#347](https://github.com/NCodeGroup/Identity/issues/347)).

- **A token validation failure carries a machine-readable code.** `JwtValidationFailureCodes` is the open vocabulary of
  the JWT validator's codes (`invalid_signature`, `invalid_issuer`, `invalid_audience`, `expired`, `not_yet_valid`,
  `algorithm_not_permitted`, …). A handler fails with `JwtValidationContext.Fail(failureCode, reason)`, and
  `ClaimValueRequirement.FailureCode` names a claim check's code. `JwtValidationException.FailureCode` and
  `JwtValidationResult.FailureCode` report the first failure's code. `IdTokenValidationResult` and
  `LogoutTokenValidationResult` gain a `FailureCode` from the open `TokenValidationFailureCodes`, which adds
  `subject_mismatch`, `nonce_mismatch`, `max_age_exceeded`, `token_hash_mismatch` and `token_replayed`.
  **Breaking:** `JwtValidationContext.Fail(string reason)` is removed, so a handler passes a code first, and the failure
  constructors of `IdTokenValidationResult` and `LogoutTokenValidationResult` take `(failureCode, failureReason)`.
  `NCode.Identity.OpenId.Client.Abstractions` now references `NCode.Identity.Jwt.Abstractions`. `FailureReason` stays
  text without the token
  ([ADR-0278](docs/decisions/0278-token-validation-failures-carry-a-machine-readable-code.md), issue [#343](https://github.com/NCodeGroup/Identity/issues/343)).

- **The JWK package names the JSON Web Key vocabulary.** `NCode.Identity.Jwk` adds `JsonWebKeyUses`,
  `JsonWebKeyOperations`, `JsonWebKeyTypes` and `JsonWebKeySetParameterNames`. The protocol client, the OP's client key
  import and registration, and ECDH key management use them instead of string literals, and `SecretKeyUses` takes its
  values from `JsonWebKeyUses` (issue [#357](https://github.com/NCodeGroup/Identity/issues/357)).

- **The OpenID protocol client pushes authorization requests.** `IAuthorizationRequestClient.CreateAuthorizationUriAsync`
  takes a `ClientRegistration` and an `AuthorizationRequest` (redirect URI, `state`, PKCE verifier, optional `nonce`,
  `scope`, `response_mode`, resources and open additional parameters) and returns an `AuthorizationRequestResult`. It
  always uses the code flow with PKCE `S256`, pushes the request (RFC 9126) whenever the provider advertises a PAR
  endpoint, and refuses the front channel when the provider or `ClientRegistration.RequirePushedAuthorizationRequests`
  requires PAR. A PAR error is returned as data
  ([ADR-0275](docs/decisions/0275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md), issue [#123](https://github.com/NCodeGroup/Identity/issues/123)).

- **The OpenID protocol client validates back-channel logout tokens.** `ILogoutTokenValidator.ValidateAsync` takes a
  `ClientRegistration` and a `LogoutTokenValidationRequest` and returns a `LogoutTokenValidationResult` with
  `IsValid`, the `TokenId`, `ExpiresAt`, `Issuer`, `Subject`, `SessionId` and a `FailureReason`. It applies the ID-token
  signature policy, then requires the `logout+jwt` `typ`, the back-channel logout `events` claim and a `sid` or `sub`,
  refuses a `nonce`, and bounds a token's lifetime to 10 minutes. `ClientRegistration.BackchannelLogoutSessionRequired`
  makes the `sid` mandatory. `ValidateAsync` refuses a token whose `jti` is recorded in `IExpiringKeySet`, and
  `TryRecordHandledAsync` records it once the client has ended the token's sessions, so a delivery the client failed to
  handle validates again when the OP retries it; that needs a SharedState adapter registered
  ([ADR-0274](docs/decisions/0274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md),
  [ADR-0284](docs/decisions/0284-a-logout-token-is-recorded-only-after-the-client-handles-it.md), issues
  [#123](https://github.com/NCodeGroup/Identity/issues/123),
  [#349](https://github.com/NCodeGroup/Identity/issues/349)).

- **The OpenID protocol client validates ID tokens.** `IIdTokenValidator.ValidateAsync` takes a `ClientRegistration`
  and an `IdTokenValidationRequest` and returns an `IdTokenValidationResult` with `IsValid`, the `Payload` and a
  `FailureReason`; `AddOpenIdClientLibrary()` registers it. The signature algorithm policy is asymmetric-only, never
  `none` or HMAC, and is limited by `ClientRegistration.IdTokenSigningAlgorithms` and the provider's
  `id_token_signing_alg_values_supported`. An unknown `kid` triggers one rate-limited JWKS refresh. The
  `at_hash`/`c_hash`/`s_hash` claims are checked in constant time through `SignatureAlgorithm.GetTokenHashFunction`.
  The shared `TokenHash` helper in `NCode.Identity.Jose.Algorithms` does that check, and the server's ID-token payload
  handler now uses it too
  ([ADR-0273](docs/decisions/0273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md), issue [#335](https://github.com/NCodeGroup/Identity/issues/335)).

- **The EdDSA package registers the fully-specified `Ed25519` and `Ed448` algorithms.** RFC 9864 deprecates the
  polymorphic `EdDSA`, which stays registered for interop. One `EdDsaSignatureAlgorithm` serves all three codes, and
  `KeyedAlgorithm.IsKeyCompatible(SecretKey)` now decides whether a key fits an algorithm, for the credential selector
  and DPoP proofs alike. `SignatureAlgorithm.GetTokenHashFunction` supplies the ID-token `at_hash`/`c_hash`/`s_hash`
  hash: SHA-512 for Ed25519 and SHAKE256 with 114 bytes of output for Ed448. The FAPI 2.0 signing ceiling becomes PS256,
  ES256, EdDSA and Ed25519
  ([ADR-0272](docs/decisions/0272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md)).

- **An OpenID protocol client exists.** Phase 1a of the BFF adds `NCode.Identity.OpenId.Client`, which
  `AddOpenIdClientLibrary()` registers on the JWT library builder. `ITokenClient` makes token requests and
  `ITokenRevocationClient` makes revocation requests; `IProviderMetadataResolver` and `IProviderKeySetResolver` resolve
  and cache a provider's discovery document and JWKS. Client authentication is an open `IClientAuthenticator`
  collection with `client_secret_basic`, `client_secret_post` and `private_key_jwt`. A `ClientRegistration` names its
  signing keys through a `KeyRing`, which selects a keyed `ISecretKeyCollectionProvider`. mTLS remains
  in phase 1b, and the BFF itself is not yet delivered
  ([ADR-0270](docs/decisions/0270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md), issue [#123](https://github.com/NCodeGroup/Identity/issues/123)).

- **Shared state holds versioned expiring values and membership indexes.** `IExpiringValueSet` stores an opaque,
  versioned `ExpiringValue` with an atomic take and a compare-and-replace, and `IExpiringIndexSet` keeps expiring
  membership indexes. `AddInProcessExpiringValueSet` and `AddInProcessExpiringIndexSet` register the in-process
  adapters, and `AddValkey` registers both Valkey adapters. This is phase 0 of the BFF; the BFF itself is not yet
  delivered
  ([ADR-0267](docs/decisions/0267-replicas-share-versioned-expiring-values-and-membership-indexes.md), issue [#123](https://github.com/NCodeGroup/Identity/issues/123)).

- **The in-process dispatcher is configured from `OpenId:InProcessJobDispatcher`.** `AddConfiguration` binds
  `InProcessJobDispatcherOptions` (`QueueCapacity`, `FullMode`, `MaxConcurrentJobs`) strictly from the OpenID section's
  `InProcessJobDispatcher` subsection, which `InProcessJobDispatcherOptions.SectionName` names, and the host refuses to
  start on an unknown key, a capacity or concurrency below 1, or a `FullMode` that names no member of
  `BoundedChannelFullMode`; each failure names its key
  ([ADR-0242](docs/decisions/0242-the-in-process-dispatcher-is-configured-under-the-openid-section.md), issue [#294](https://github.com/NCodeGroup/Identity/issues/294)).

- **The management API lists a tenant's incoherent clients.** `GET api/tenants/{tenantId}/incoherent-clients` checks a
  page of the tenant's stored clients (`cursor`, `limit` up to 100) against the tenant's effective settings and returns
  each client they make incoherent with every violation (`settingName`, `value`, `governingSettingName`,
  `description`), or the `documentError` of a settings document that cannot be honored; a page can list no client while
  `continuationToken` remains. `IClientCoherenceReporter` finds them for the API and for the reconciliation's report,
  and `IncoherentClient`, `IncoherentClientResource` and `ClientCoherenceViolationResource` describe them
  ([ADR-0233](docs/decisions/0233-the-management-api-lists-a-tenants-incoherent-clients.md), issue [#278](https://github.com/NCodeGroup/Identity/issues/278)).

- **An end user can change their password.** The self-service page `account/password` takes the current password and
  a new one; a change ends every other session of the principal with `password_changed`, which sends back-channel
  logout, revokes every grant issued under no session, and keeps the current session and the consents. "Sign out
  everywhere else" also revokes the grants issued under no session. A credential backend offers the change by
  registering `IInteractivePasswordChanger`, as the local accounts do. **Breaking:** `IInteractivePasswordChanger`,
  `InteractivePasswordChangeResult`, `InteractiveAuthenticationDefaults.PasswordPath`,
  `SessionTerminationReasons.PasswordChanged` and `GrantFilter.WithoutSession` are added
  ([ADR-0230](docs/decisions/0230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md), issue [#290](https://github.com/NCodeGroup/Identity/issues/290)).

- **Secret protection key hierarchy** ([ADR-0235](docs/decisions/0235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).
  Stored private and symmetric secrets are AES-256-GCM envelopes (`nce1.`) under a fresh data key, wrapped by an
  HKDF-derived purpose key of the owner's master key (the server or the tenant), which is wrapped by a root key held by a
  root key provider (`ephemeral`, `local`, or a registered one) and, optionally, by an offline recovery RSA key. New
  public contracts: `ISecretProtectorProvider`, `ISecretProtector`, `IRootKeyProvider`, `IMasterKeyStore`,
  `SecretOwner`, `SecretProtectionOptions`. New `MasterKeys` table (migration `AddMasterKeys`). Implements issue [#167](https://github.com/NCodeGroup/Identity/issues/167).

- **Protect your own data.** `ISecretProtector` is buffer-first (writes into caller-supplied `IBufferWriter<T>`s,
  with `GetProtectedLength` for sizing, and a `ProtectToString` helper for text columns), so applications protect any
  sensitive value under a tenant's or the server's master key with a namespaced purpose.

- `ManagementApiEndpointHandler.ProcessGetAsync` has an overload whose asynchronous mapper runs only after authorization
  succeeds; the secret public-key endpoints use it, so a caller who may not read a secret never loads its owner's master
  keys.

- **`NCode.Identity.Secrets.Persistence.AwsKms`** — an AWS KMS root key provider (`AddAwsKmsRootKeyProvider`,
  section `SecretProtection:RootKeyProviders:AwsKms`, key or alias ARN) that binds each wrap to its owner through the
  KMS encryption context and takes credentials from the AWS SDK's default chain; wired into the reference host and
  the Helm chart, which federates the pod's service-account token with AWS (`secretProtection.awsKms`) only when
  `aws-kms` is selected, and refuses a malformed key ARN.

- **`NCode.Identity.Secrets.Persistence.GoogleCloudKms`** and **`NCode.Identity.Secrets.Persistence.AzureKeyVault`** —
  a Google Cloud KMS root key provider (`google-cloud-kms`, `AddGoogleCloudKmsRootKeyProvider`, crypto key resource
  name in `SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName`) that binds each wrap to its owner through the
  additional authenticated data and checks CRC32C checksums on every request and response, and an Azure Key Vault root
  key provider (`azure-key-vault`, `AddAzureKeyVaultRootKeyProvider`, key identifier in
  `SecretProtection:RootKeyProviders:AzureKeyVault:KeyId`) that wraps with `RSA-OAEP-256` and refuses an RSA key under
  3072 bits. Both take keyless credentials from their SDK's default chain (Application Default Credentials,
  `DefaultAzureCredential`), never from configuration; wired into the reference host and the Helm chart, whose
  `secretProtection.googleCloudKms` / `secretProtection.azureKeyVault` values mount workload identity credentials only
  for the selected provider and refuse a malformed key name, key identifier or workload identity pool provider
  ([ADR-0260](docs/decisions/0260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md), issue [#173](https://github.com/NCodeGroup/Identity/issues/173)).

- **An administrator can block any end user.** `POST api/tenants/{tenantId}/principals/{principalId}/block` blocks a
  principal whatever its connections, `POST api/tenants/{tenantId}/principals/{principalId}/unblock` lifts the block,
  and `GET api/tenants/{tenantId}/principals/{principalId}` reports it. Blocking terminates every session of the
  principal with `user_blocked`, which revokes its grants and sends back-channel logout, and keeps its consents; every
  grant refuses a blocked principal with `access_denied`, and the authorization request and the interactive sign-in
  refuse it with `access_denied` (issue [#289](https://github.com/NCodeGroup/Identity/issues/289)). The change is audited as `PrincipalChangedAuditEvent` (resource type
  `principal`). **Breaking:** `PersistedFederatedPrincipal.IsBlocked`, `FederatedPrincipalEntity.IsBlocked`,
  `SessionTerminationReasons.UserBlocked`, `OpenIdConstants.EndpointTags.Principals` and
  `ManagementResourceTypes.Principal` are added, and the `AddFederatedPrincipalIsBlocked` migration adds a column to the
  federated principals table
  ([ADR-0226](docs/decisions/0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md),
  issue [#284](https://github.com/NCodeGroup/Identity/issues/284)).

- **`token.refresh.retry` audits a rotated refresh token retried within its reuse interval.** When the retry is
  answered with another refresh token of its rotation family, `RefreshTokenRetryAuditEvent` is published with outcome
  `success`, so an operator can tell a client's retry from a rotation and from a reuse (`token.refresh.replay`); a
  refused retry publishes none. **Breaking:** `IAuditEventRecorder.RecordRefreshTokenRetryAsync` is a member a custom
  recorder implements
  ([ADR-0217](docs/decisions/0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md), issue [#275](https://github.com/NCodeGroup/Identity/issues/275)).

- **The grant retention and the usage ledger are configured through `OpenId:GrantRetention` and
  `OpenId:TenantUsage`.** `AddConfiguration` binds `GrantRetentionOptions` (`Retention`, `PruneInterval`) and
  `TenantUsageOptions` (`FlushInterval`, `Retention`) strictly and validates them when the host starts;
  `GrantRetentionOptions.SectionName` and `TenantUsageOptions.SectionName` name the subsections
  ([ADR-0221](docs/decisions/0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md),
  issue [#274](https://github.com/NCodeGroup/Identity/issues/274)).

- **The host refuses to start with an interval or a lifetime that a timer cannot run.** The server's and the tenants'
  settings and secrets refresh intervals, `OpenId:Server:ReconciliationLeaseDuration`,
  `OpenId:Tenant:TenantCacheLifetime`, `GrantRetentionOptions.PruneInterval` and `TenantUsageOptions.FlushInterval`
  must be between one millisecond and 4,294,967,294 milliseconds (about 49.7 days), and a retention must not be
  negative; the error names the option and its configuration key. `NCode.Time.TimerPeriod` states the range
  ([ADR-0221](docs/decisions/0221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md),
  issue [#277](https://github.com/NCodeGroup/Identity/issues/277)).

- **A client whose refresh tokens rotate under the FAPI 2.0 profile must be able to retry a rotation.** The client
  setting `refresh_token_rotation_retry_required` (`false` by default; a child scope cannot un-require it) is required
  by the FAPI 2.0 security profile, and the coherence rule `RefreshTokenRotationRetryCoherenceRule` refuses a client
  that requires it while its refresh tokens rotate (`refresh_token_rotation_policy` `always`, or `unbound` for a public
  client) and its `refresh_token_reuse_interval` is zero (FAPI 2.0 §5.3.2.1)
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md),
  [ADR-0217](docs/decisions/0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md), issue [#272](https://github.com/NCodeGroup/Identity/issues/272)).

- **A server settings write takes effect at once on every replica.** After the server settings PATCH commits, the
  replica that handled it reads the committed document again before it answers and announces the change on the
  `server-settings-changed` replica broadcast channel, so every replica that receives the announcement reads it again.
  `OpenIdServer.RefreshSettingsAsync` reads the document on demand, and the poll
  (`OpenId:Server:SettingsPeriodicRefreshInterval`, 5 minutes) is the fallback for a replica that missed the
  announcement
  ([ADR-0219](docs/decisions/0219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issue [#248](https://github.com/NCodeGroup/Identity/issues/248)).

- **Every tenant and stored client is reconciled with the server's effective settings when they change.** Each replica
  runs `ServerSettingsReconciliationService`, which reacts to a management write, a configuration reload, and a
  deployment that changes the server's effective settings. One replica, holding a lease in the shared state for
  `OpenId:Server:ReconciliationLeaseDuration` (10 minutes by default), sends `ReconcileServerSettingsCommand`:
  `TenantKeyReconciliationHandler` seeds every tenant that holds keys but lacks one its settings use, and
  `ClientCoherenceReportHandler` logs every stored client that the settings make incoherent, the bootstrap
  administrator included. `PersistedServer.ReconciledSettingsFingerprint`, which the
  `AddServerReconciledSettingsFingerprint` migration adds, records what was reconciled, so a pass that a restart
  interrupted runs again and a start that changes nothing runs none
  ([ADR-0219](docs/decisions/0219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issues [#252](https://github.com/NCodeGroup/Identity/issues/252)
  and [#263](https://github.com/NCodeGroup/Identity/issues/263)).

- **A replica that may have missed a broadcast drops what the message would have invalidated.**
  `IReplicaBroadcastHandler.HandleMissedMessagesAsync` is called by the Valkey subscriber once it subscribes a channel
  and whenever its connection is restored. The tenant handler evicts every cached tenant through
  `IOpenIdTenantCache.ClearAsync`, and the server handler reads the server's settings again
  ([ADR-0205](docs/decisions/0205-replicas-announce-changes-through-a-broadcast-port.md),
  [ADR-0219](docs/decisions/0219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issue [#249](https://github.com/NCodeGroup/Identity/issues/249)).

- **`refresh_token_reuse_interval` lets a client retry a refresh with its rotated refresh token.** The interval is off
  by default and at most 60 seconds. A rotated refresh token that is presented within the interval is answered as the
  rotation was, and the retry receives a new refresh token in the same rotation family, so a client that lost the
  response, or two tabs that refresh at once, keep their session. A rotation marks the spent grant consumed, and the
  revocation endpoint revokes the whole family of a refresh token.
  `IPersistedGrantService.AddToRefreshTokenLineageOrDefault`, `ITokenService.ReissueRefreshTokenAsync`, and
  `IGrantStore.GetActiveByRefreshTokenLineageAsync` store and read the branch
  ([ADR-0217](docs/decisions/0217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md), issue [#270](https://github.com/NCodeGroup/Identity/issues/270)).

- **Grants are removed after a retention period.** `GrantPruneService` runs hourly in every replica and removes, in
  bounded batches, the grants that expired or were revoked more than `GrantRetentionOptions.Retention` ago (14 days by
  default), so the grants table holds live grants and those that ended within the retention.
  `IGrantStore.RemoveEndedBeforeAsync` stages one batch for removal, and the `AddGrantRevokedWhenIndex` migration
  indexes `Grants.RevokedWhen`
  ([ADR-0218](docs/decisions/0218-ended-grants-are-removed-after-a-retention-period.md), issue [#267](https://github.com/NCodeGroup/Identity/issues/267)).

- **`ClientCoherenceContext` gives a coherence rule the settings a client registers and its effective settings, and
  three more built-in rules read it.** `IClientCoherenceRule.FindViolations` receives the client's own settings
  document as written, without its tenant's settings and without any security profile's ceilings and defaults, beside
  the effective settings that result, so a rule sees a value that a ceiling dropped.
  `ClientCoherenceViolation.Missing` reports a required value that is absent. `RegisteredValueCoherenceRule` reports a
  value that a client lists, or a `true` that it registers, that an `Intersect` or `And` ceiling drops from its
  effective settings. `IdTokenEncryptionCoherenceRule` reports a client that must encrypt its ID tokens and registers
  no `id_token_encrypted_response_alg`. `BackchannelTokenDeliveryCoherenceRule` reports a client that has no
  `client_notification_endpoint` while its delivery mode is not `poll`, and a client that may use the CIBA grant while
  `backchannel_token_delivery_modes_supported` excludes its delivery mode
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#253](https://github.com/NCodeGroup/Identity/issues/253)).

- **Dynamic client registration reads and reports `dpop_bound_access_tokens`, and
  `DPoPBoundAccessTokenCoherenceRule` checks the requirement.** The RFC 9449 §5.2 client metadata member is the client's
  requirement `require_dpop_bound_access_tokens`, as `tls_client_certificate_bound_access_tokens` is the requirement
  `require_tls_client_certificate_bound_access_tokens`. The rule reports a client that requires DPoP-bound access
  tokens while its effective `dpop_signing_alg_values_supported` is set and empty, because that list is how a server
  advertises DPoP support (RFC 9449 §5.1), so no proof could be accepted
  ([ADR-0097](docs/decisions/0097-sender-constrained-access-tokens-dpop-and-mtls.md),
  [ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#256](https://github.com/NCodeGroup/Identity/issues/256)).

- **`StoreConcurrencyException` reports a lost optimistic-concurrency race.** The store manager throws it from
  `SaveChangesAsync` when another unit of work changed an entity after it was read, and a store throws it when the
  concurrency token that its caller presents differs from the stored one. A caller handles a lost race without a
  reference to Entity Framework, and a store that is not built on it reports a lost race the same way
  ([ADR-0208](docs/decisions/0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md), issue [#257](https://github.com/NCodeGroup/Identity/issues/257)).

- **`DistinguishedName` matches a distinguished name as RFC 4517 `distinguishedNameMatch` does.**
  `DistinguishedName.TryParse` reads an RFC 4514 string and what an operator pastes: whitespace around `=`, `,`, and
  `+`, RFC 2253 quoted values, `\` escapes, `\XX` UTF-8 hex escapes, and `#` hex BER values. An attribute type is a
  keyword from a closed table, a dotted OID, or `OID.` followed by a dotted OID. `DistinguishedName.FromX500` reads a
  certificate's name, and `Matches` compares two names: the RDN count and order are equal, a multi-valued RDN is a set,
  and a character-string value is compared after NFKC normalization, deletion of the characters that map to nothing,
  whitespace collapse, and case folding, while any other value is compared by its exact encoding. The
  `tls_client_auth_subject_dn` setting rule, the `tls_client_auth` handler, and the CRL issuer check use it
  ([ADR-0173](docs/decisions/0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md), issue [#209](https://github.com/NCodeGroup/Identity/issues/209)).

- **`IClientCertificateRevocationChecker` and `ClientCertificateRevocationStatus`.** `CheckAsync` answers whether an
  issuer revoked a certificate with `Good`, `Revoked`, or `Unknown`, and `Unknown` is the zero value, so a checker that
  cannot tell fails closed. The chain validator asks it about every element of a chain except the trust anchor, and the
  chain passes only when every answer is `Good`. The default is registered with `TryAddSingleton`, so a host replaces
  it, for example to check OCSP. The default reads the certificate's `http` and `https` CRL distribution points,
  fetches each CRL through the named client `NCode.Identity.OpenId.ClientCertificateRevocation`, accepts only a current
  DER CRL that the issuing certificate signed, and caches it until its `nextUpdate` or one hour. It answers `Unknown`
  for a certificate with no usable CRL, such as an OCSP-only PKI or a delta, partitioned, or indirect CRL
  ([ADR-0173](docs/decisions/0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md), issues [#207](https://github.com/NCodeGroup/Identity/issues/207), [#255](https://github.com/NCodeGroup/Identity/issues/255)).

- **`require_tls_client_certificate_bound_access_tokens` is a client's requirement that its access tokens be bound to
  its certificate.** As `require_dpop_bound_access_tokens` is separate from the server's advertised
  `dpop_signing_alg_values_supported`, the requirement is separate from the advertised support,
  `tls_client_certificate_bound_access_tokens` (RFC 8705 §3.3). It applies to clients, and a server or tenant may set
  it as a default for every client. It merges with `Or`, defaults to `false`, and is not advertised. The token endpoint
  binds the certificate only for a client whose requirement is `true`, and a client that requires binding and presents
  no certificate is refused with `invalid_request`. Dynamic client registration reads and reports the RFC 8705 §3.4
  client metadata `tls_client_certificate_bound_access_tokens` as the requirement. A coherence rule,
  `CertificateBoundAccessTokenCoherenceRule`, reports a client that requires binding while its effective support is
  `false`
  ([ADR-0097](docs/decisions/0097-sender-constrained-access-tokens-dpop-and-mtls.md),
  [ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#233](https://github.com/NCodeGroup/Identity/issues/233)).

- **`IInitialAccessTokenValidator.TryConsumeAsync` spends an initial access token, and
  `PersistedGrant<TPayload>.ConsumedWhen` records when a grant was spent.** Dynamic registration spends the token once
  every check has passed, immediately before the client is stored, and refuses with `401` `invalid_token`, storing
  nothing, a token that cannot be spent. `ValidateAsync` answers `null` for a grant whose `ConsumedWhen` is set. The
  default spends the grant row, whose concurrency token makes the spend atomic across replicas. **Breaking:** a
  replacement validator implements `TryConsumeAsync` and returns `false` when the token no longer authorizes
  registration
  ([ADR-0192](docs/decisions/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md),
  [ADR-0063](docs/decisions/0063-dynamic-client-registration.md), issue [#208](https://github.com/NCodeGroup/Identity/issues/208)).

- **Client coherence rules.** `IClientCoherenceRule` names the settings it reads and reports each value a client
  registers that an effective setting does not permit as a `ClientCoherenceViolation`, and `IClientCoherenceValidator`
  runs every registered rule over a client's effective settings and tells a caller whether a change touches a setting a
  rule reads. The four built-in rules check a client's response algorithms, its redirect URI kinds, its token endpoint
  authentication method, and a required certificate-bound access token against the server's support for it, and a
  package adds a rule by registering it with `TryAddEnumerable`
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#231](https://github.com/NCodeGroup/Identity/issues/231)).

- **Client-authentication method descriptors.** `ClientAuthenticationMethodDescriptor` names a method and says whether
  it requires a client secret. A package contributes one through
  `ICollectionDataSource<ClientAuthenticationMethodDescriptor>`, and `IClientAuthenticationMethodCatalog` lists the
  registered names and finds a descriptor without constructing a handler
  ([ADR-0207](docs/decisions/0207-client-authentication-methods-are-named-by-registered-descriptors.md), issue [#251](https://github.com/NCodeGroup/Identity/issues/251)).

- **`IEffectiveSettingsResolver` resolves a scope's effective settings in one place.** Each caller used to rebuild the
  merge of the server's settings, a tenant's document, and a client's or resource server's document, with the security
  profiles' ceilings and defaults. The registrable resolver does it: `ReadScopeSettings` and `Resolve` read a scope's
  document and merge it onto its parent's effective settings, and the two `ResolveServerAsync` overloads resolve a
  proposed or the committed server document onto the configured settings. Its default is registered with
  `TryAddSingleton`, so a host replaces it
  ([ADR-0204](docs/decisions/0204-a-scopes-effective-settings-are-resolved-by-one-service.md), issue [#230](https://github.com/NCodeGroup/Identity/issues/230)).

- **`IReplicaBroadcaster` and `IReplicaBroadcastHandler` announce a change to every replica.** A replica publishes a
  message on a channel, and every replica's handlers for that channel receive it, the publisher's included. Delivery is
  best effort, at most once, and unordered, so a handler is idempotent, and a handler's failure is logged and never
  reaches the publisher. `AddInProcessReplicaBroadcaster()` is the default, and `AddIdentityServer` registers it. The
  Valkey adapter publishes with `PUBLISH` and `SUBSCRIBE` on the key prefix plus the channel, so its access-control
  user needs both commands. The `tenant-changed` channel evicts a changed tenant on every replica
  ([ADR-0205](docs/decisions/0205-replicas-announce-changes-through-a-broadcast-port.md), issue [#240](https://github.com/NCodeGroup/Identity/issues/240)).

- **`ServerSettingsChangedCommand`.** The server settings PATCH sends it after it commits, carrying the previous and the
  new document. Its default handler reads the document again on this replica and announces the change to every replica,
  and the reconciliation of the server's settings then seeds each tenant that holds keys but lacks one its lists name,
  outside the request ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-0219](docs/decisions/0219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issue [#241](https://github.com/NCodeGroup/Identity/issues/241)).

- **`ITenantStore.GetSettingsPageAsync` and `IClientStore.GetSettingsPageAsync`.** Each reads a page of settings
  documents without their secrets and without change tracking, for a pass over every tenant or client (issue [#212](https://github.com/NCodeGroup/Identity/issues/212)).

- **Redirect URI kinds, which a client's settings narrow.** The new `redirect_uri_kinds_supported` setting lists the
  kinds of redirect URI a client may register and use. It applies to tenants and clients, merges with `Intersect`,
  is not advertised, and permits every kind while unset. A kind is a registered `IRedirectUriKind`, judged through
  `IRedirectUriKindCatalog`, so a package adds one by registering it. The built-in kinds, named in
  `OpenIdConstants.RedirectUriKinds`, sort a redirect URI into at most one: `https`, `loopback` (an `http` URI whose
  host is `127.0.0.1` or `[::1]`, RFC 8252 §7.3), `http` (any other `http` URI, `localhost` included), and
  `private_use` (a reverse-domain scheme such as `com.example.app`, RFC 8252 §7.1)
  ([ADR-0203](docs/decisions/0203-redirect-uris-are-matched-exactly.md), issue [#244](https://github.com/NCodeGroup/Identity/issues/244)).

- **`IOpenIdEndpointMeteredMetadata` and `WithOpenIdMetered(bool)` mark whether an endpoint is the tenant's metered
  usage.** A metered endpoint's requests spend the daily request quota and are recorded in the usage ledger; an
  endpoint without the metadata is metered. A package that adds a public metadata endpoint opts it out with
  `WithOpenIdMetered(false)`.

- **`HealthCheckTags.Readiness`.** `NCode.Identity.Abstractions` publishes the tag (`"ready"`) of every health check
  that decides whether a replica can serve requests. The Valkey adapter tags its check with it and the reference host's
  `/health/ready` endpoint runs the checks that carry it, so a package that adds a readiness check uses the constant
  instead of repeating the string.

- **FAPI 1.0 Advanced conformance harness.** `build/conformance/run-conformance.ps1 -Fapi1Advanced` runs the
  conformance suite's `fapi1-advanced-final-test-plan` with `private_key_jwt` and `self_signed_tls_client_auth`
  clients, pushed authorization requests, and plain and JARM responses.
  Like the default run, it runs the in-house probe-runner pre-flight first (`-SkipPreflight` skips it). It generates
  each client's key and self-signed certificate on first use into a gitignored folder, so no private key is committed
  ([ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).
  - `ConformanceClientSeed` gains `Jwks`, the public key set a client registers by value, and `SecurityProfiles`. A
    client with a key set and no secret is seeded with exactly the keys the client-key policy imports, and a key set
    the policy refuses stops the seeding with an error naming the client.
  - The reference host receives mutual-TLS client certificates when it terminates TLS itself: its HTTPS defaults
    accept any client certificate an endpoint requests (`Kestrel:EndpointDefaults:ClientCertificateMode`), and the OP
    validates it against the client it authenticates. It is inert while no endpoint requests a certificate. A host
    that terminates TLS itself and serves mutual-TLS clients does the same in its own `ConfigureHttpsDefaults`.
- **FAPI 1.0 Advanced security profile (`fapi1-advanced`).** A tenant or client that selects it through
  `security_profiles` gets the following ceilings:
  - `code` with a JWT-secured (JARM) response mode, or `code id_token` once the tenant lists it;
  - PAR, PKCE `S256`, and signed request objects that carry `nbf`, live at most 60 minutes, and are addressed to the
    issuer;
  - access tokens bound to the client's certificate through mutual TLS;
  - `private_key_jwt`, `tls_client_auth`, or `self_signed_tls_client_auth` client authentication, with public clients
    refused;
  - PS256 or ES256 signing only, with PS256 the default for ID tokens, introspection, and authorization responses;
  - authorization codes that live at most 60 seconds and PAR `request_uri`s at most 90.

  A request for plain `code` without a `.jwt` response mode is refused with `invalid_request`.
  `AuthorizationRequestProfileContext` now carries the composed `IAuthorizationRequest` for request-time requirements.
  The spec-coverage matrix gains a `profile-fapi1-advanced` row. See
  [ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md).

- **JWT-secured authorization responses (JARM).** The authorization endpoint serves the `query.jwt`, `fragment.jwt`,
  `form_post.jwt`, and `jwt` response modes. They deliver the response, an error included, as one JWT in the `response`
  parameter. The JWT is issued by the tenant, addressed to the client, and expires after 5 minutes. It is signed with
  the client's new `authorization_signed_response_alg` (default `RS256`, `PS256` under `fapi2`) and encrypted when the
  client registers `authorization_encrypted_response_alg`/`_enc`. Discovery advertises the modes and the new
  `authorization_signing_alg_values_supported` and `authorization_encryption_alg_values_supported`/`_enc_values_supported`
  ceilings, and Dynamic Client Registration accepts and reports the client metadata. `jwt` resolves to `fragment.jwt`
  when the response type returns a token, and `query.jwt` is refused for such a response type. See ADR-0201.

- **Response modes are an open vocabulary.** The authorization result executor delivers each response through the
  `IAuthorizationResponseModeHandler` registered for its response mode instead of a fixed switch, so a package adds a
  response mode by registering a handler. The `response_mode` setting rule accepts any whitespace-free name, and a
  request whose response mode no handler serves is refused with `400` and no redirect. `AuthorizationResult` now
  carries the request's `OpenIdContext` and `OpenIdClient`. See ADR-0201.

- **No CORS on the front-channel endpoints.** The authorization, continue, and end-session endpoints and the
  interactive login/consent surface carry the new `IOpenIdEndpointFrontChannelMetadata` (`WithOpenIdFrontChannel()`),
  and `UseDeveloperCors` applies the developer policy only to endpoints without it, so in Development they no longer
  answer a cross-origin request with `Access-Control-Allow-Origin` (FAPI 2.0 Security Profile §5.3.2.1). The marker
  also makes routing send a CORS preflight to the endpoint itself, which refuses it with `405` and no CORS headers,
  instead of to routing's metadata-less 405 endpoint, which the CORS middleware answered; `UseDeveloperCors` also
  answers no preflight that selects no endpoint, such as one for a method a front-channel path does not map. A host
  that adds its own CORS middleware should skip both. See the ADR-0109 amendment.

- **Strong client-authentication methods at Dynamic Client Registration.** A self-registered client can register
  `private_key_jwt`, `client_secret_jwt`, `tls_client_auth`, or `self_signed_tls_client_auth`: registration accepts
  every method the tenant's effective `token_endpoint_auth_methods_supported` permits instead of a fixed list, so a
  `fapi2` tenant refuses `client_secret_*` when the client registers. `private_key_jwt` and
  `self_signed_tls_client_auth` require `jwks` or `jwks_uri` (a `jwks` by value with an `x5c` certificate for the
  latter); `tls_client_auth` requires exactly one new `tls_client_auth_*` identity parameter, which the read and update
  responses report. Only the `client_secret_*` methods are issued a `client_secret`. See the ADR-0145 amendment.

- **Billed overage past a daily quota.** A plan tier can admit requests past its daily quota and bill them instead of
  refusing at the quota: `quota_tenant_requests_overage_per_day` and `quota_tenant_token_requests_overage_per_day` set
  how many more requests (or confidential clients' token requests) are served past the matching quota in a UTC day.
  Unset or `0` refuses at the quota as before; a set allowance is also the hard ceiling, past which the tenant is
  refused until midnight UTC. Like the quotas, a server-scope allowance is every tenant's default and caps a
  tenant-scope one. Each request admitted past a quota is recorded in the usage ledger under the new meters
  `requests_overage` and `token_requests_overage`, which the usage report and the admin UI's Usage tab show with the
  others, and is counted in `ncode.identity.rate_limiting.requests` with the result `overage`. See
  [ADR-0200](docs/decisions/0200-a-tier-admits-a-bounded-overage-past-its-quota-and-bills-it-from-the-ledger.md).
- **Per-tenant usage ledger.** Every tenant's daily usage is recorded durably for billing and customer reports in a new
  `TenantUsage` table (migration `AddTenantUsage`), one row per tenant, UTC day, and meter: `requests` (requests
  admitted by the rate limits and the daily quota) and `token_requests` (confidential clients' token requests admitted
  by the token quota). The meter vocabulary is open: a package records its own through `ITenantUsageRecorder`. Each
  replica counts in memory and adds to the ledger every minute (`TenantUsageOptions.FlushInterval`), and the ledger
  keeps 400 days (`TenantUsageOptions.Retention`). `GET /api/tenants/{tenantId}/usage?from=&to=` reads a tenant's
  usage for up to 366 days, the last 30 by default; the admin UI shows it on the tenant page's **Usage** tab, a column
  per meter with a total row, over a chosen UTC date range. See
  [ADR-0199](docs/decisions/0199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md).
- **Per-tenant daily quotas.** Two tenant settings bound a tenant's consumption per UTC calendar day for plan tiers:
  `quota_tenant_requests_per_day` (requests to its protocol endpoints, judged after the per-minute rate limits) and
  `quota_tenant_token_requests_per_day` (token requests from confidential clients, judged at the token endpoint after
  client authentication, so a caller that knows only a public `client_id` cannot spend it). A request over a quota is
  refused with `429 Too Many Requests`, `temporarily_unavailable`, and `Retry-After` until midnight UTC. Both are
  unlimited when unset and merged with `Min`, so the server's value caps every tenant. They are counted in the shared
  `IExpiringCounterSet` and reported under the rate-limiting partitions `daily_requests` and `daily_token_requests`.
  `IRequestRateLimiter` gains `AcquireForTokenRequestAsync`. See
  [ADR-0198](docs/decisions/0198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md).
- **Protocol-endpoint metrics.** The protocol endpoints tag ASP.NET Core's `http.server.request.duration` with
  `ncode.identity.openid.endpoint` (the endpoint's registered name, stable when routes or tenant routing change),
  `ncode.identity.openid.error` (the OAuth error code of every error response, including the rate limiter's `429` and an
  authorization error redirected to the client), and, on token requests that reach grant-handler selection,
  `ncode.identity.openid.grant_type` (a registered grant type, else `_OTHER`); the attribute
  names are constants on `OpenIdMetricAttributes`. `NCode.Identity.OpenId.Authentication` owns the meter
  `OpenIdAuthenticationMetrics.MeterName` and the counter `ncode.identity.rate_limiting.requests` (`{request}`) by
  `ncode.identity.rate_limiting.partition` and `ncode.identity.rate_limiting.result` (`admitted`, `rejected`,
  `store_unavailable`). `NCode.Identity.SharedState.Valkey` owns the meter `ValkeyMetrics.MeterName` and the histogram
  `ncode.identity.shared_state.operation.duration` (`s`) of each Valkey round trip by operation, adapter, and
  `error.type`. An event adds bounded attributes to `ncode.identity.events.published` by implementing
  `ISupportMetricAttributes`: `TokenIssuedAuditEvent` adds `ncode.identity.token.type` and
  `ClientAuthenticationFailedAuditEvent` adds `ncode.identity.client_authentication.method`. `NCode.Identity.Server`
  adds `AddOpenTelemetryMetrics(configuration)`, which the reference host calls: the family's meters, ASP.NET Core,
  Kestrel, `System.Net.Http` (bounded attributes only), and the runtime, exported over OTLP when
  `OTEL_EXPORTER_OTLP_ENDPOINT` is set; the exporter is added to the metrics pipeline only, so logs and traces stay the
  host's choice. Every meter comes from `IMeterFactory`. The catalog is
  [`docs/metrics.md`](docs/metrics.md) ([ADR-0197](docs/decisions/0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md),
  issue [#119](https://github.com/NCodeGroup/Identity/issues/119)).
- **Per-tenant rate limits.** Three settings limit the requests per minute a tenant's protocol endpoints serve:
  `rate_limit_tenant_per_minute` (every request), `rate_limit_address_per_minute` (one remote address, an IPv6 address
  counted by its `/64`), and `rate_limit_client_per_minute` (one authenticated confidential client, set on the tenant or
  the client). Each is unset (unlimited)
  by default and merges with `Min`, so a tenant or client can only lower the limit above it. The limiter counts in the
  shared `IExpiringCounterSet` with a two-window sliding estimate, so the limits hold across replicas that share Valkey.
  A refused request is answered `429 Too Many Requests` with `temporarily_unavailable`, `Retry-After`, and
  `Cache-Control: no-store`, and is neither handled nor audited. A counter store that fails admits the request and logs
  a warning ([ADR-0196](docs/decisions/0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md), issue
  [#84](https://github.com/NCodeGroup/Identity/issues/84)).
- **A shared expiring counter set for rate limiting.** `NCode.Identity.SharedState.Abstractions` adds
  `IExpiringCounterSet`: `IncrementAsync(key, expiresWhen)` atomically increments a counter that spans one fixed window
  and returns its count, and `GetAsync(key)` reads a count. `AddInProcessExpiringCounterSet` registers the in-process
  default, which `AddIdentityServer` now registers beside the in-process key set, and `AddValkey` replaces it with a
  Valkey adapter that runs `INCR` and `PEXPIRE` as one script, so every replica counts against one counter. It is the
  foundation of per-tenant rate limiting
  ([ADR-0196](docs/decisions/0196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md), issue [#84](https://github.com/NCodeGroup/Identity/issues/84)).

- **The management console shows and edits a tenant's mTLS endpoint alias (RFC 8705 §5).** The `@ncode/api` mirrors
  carry `mtlsEndpointAliasAuthority` on `TenantResource`, `CreateTenantRequest`, and `UpdateTenantFields`, and
  `TenantAddressResource` gains it with `isMtlsEndpointAliasAuthorityDerived`. The store's `getTenantAddress` takes an
  optional `mtlsEndpointAliasAuthority`. The tenant dialog edits the alias beside the issuer and shows the derived
  default as its hint and placeholder, the tenant table shows it in its own column, and `TenantsPage` previews a
  proposed alias, re-judges an existing tenant's address only when its issuer or alias changes, and sends the alias on
  create and update, where an empty alias restores the derived one
  ([ADR-0195](docs/decisions/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md), issue [#157](https://github.com/NCodeGroup/Identity/issues/157)).
- **A tenant's mTLS endpoint alias takes a derived default, as its issuer does (RFC 8705 §5).** The new
  `DynamicByHost:MtlsEndpointAliasTemplate` (for example `https://{tenantId}.mtls.example`) gives every tenant written
  without an alias its expansion, and `Root:MtlsEndpointAliasAuthority` and `StaticSingle:MtlsEndpointAliasAuthority`
  configure the root and static tenants' alias beside their issuers. `ITenantStrategy` and `ITenantResolver` gain
  `GetDefaultMtlsEndpointAliasAuthorityAsync`. Management create and update, and the provisioner, store the derived alias
  so a later template change never moves a tenant, and an update that clears the alias restores the derived one. The
  template is checked at startup: an `https` origin template with `{tenantId}` in its host, whose host the host pattern
  maps to itself and which differs from the issuer template's host. The address preview
  (`GET /tenants/{tenantId}/address`) takes an optional `mtlsEndpointAliasAuthority` and returns the effective
  `MtlsEndpointAliasAuthority` and `IsMtlsEndpointAliasAuthorityDerived` on `TenantAddressResource`
  ([ADR-0195](docs/decisions/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md), issue [#117](https://github.com/NCodeGroup/Identity/issues/117)).
- **A tenant's mTLS endpoint-alias host routes to the tenant (RFC 8705 §5).** The alias is now the tenant property
  `PersistedTenant.MtlsEndpointAliasAuthority` (an `https` origin), exposed as `OpenIdTenant.MtlsEndpointAliasAuthority`,
  and discovery builds `mtls_endpoint_aliases` from it. The host strategy routes a request to a tenant by its issuer host
  or its alias host, through `ITenantStore.GetOrDefaultByHostAsync` (renamed from `GetOrDefaultByIssuerHostAsync`), which
  resolves no tenant when more than one matches; validation lists every holder through the new
  `ITenantStore.ListTenantIdsByHostAsync`. Validation judges both hosts: the alias must be an `https` origin, the
  host pattern must map its host back to the tenant, it must differ from the issuer host, and no other tenant may serve
  it. `ITenantResolver.ValidateTenantAsync` and `ITenantStrategy.ValidateTenantAsync` take the alias beside the issuer.
  The management API carries `mtlsEndpointAliasAuthority` on tenant create, update, and read. **Breaking:** the
  `mtls_endpoint_alias_authority` setting (and `OpenIdSettingNames`/`OpenIdSettingKeys.MtlsEndpointAliasAuthority`) is
  removed; the `PromoteMtlsEndpointAliasAuthority` migration moves each stored value into the new
  `MtlsEndpointAliasAuthority` and derived, indexed `NormalizedMtlsEndpointAliasHost` columns. Design and rationale:
  [ADR-0195](docs/decisions/0195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md).
- **A tenant brands its login, consent, and account pages.** The interactive surface registers the tenant settings
  `branding_display_name`, `branding_logo_uri` (absolute `https`), `branding_primary_color` and
  `branding_secondary_color` (hexadecimal), and `branding_color_mode` (`light` or `dark`). It serves them at
  `GET {tenant}/account/branding`, in the bootstrap document's `branding` shape. The `apps/auth` bundle loads them
  before it renders: it themes the surface with the colors and mode, draws the logo, and titles the login and account
  pages with the display name. A failed load keeps the default theme. `@ncode/config` exports `parseBrandingConfig`,
  which the bootstrap parser now uses too, so a malformed branding token is dropped instead of trusted. New public API:
  `InteractiveSettingNames`, `InteractiveSettingKeys`, and `InteractiveAuthenticationDefaults.BrandingPath`. See
  [ADR-0194](docs/decisions/0194-a-tenant-brands-its-interactive-surface-through-settings.md).
- **The interactive surface is served under the tenant route.** The login, consent, logout, and account endpoints
  share the protocol endpoints' tenant route, so interactive flows work under `DynamicByPath` at
  `/{tenantId}/account/*`. Every redirect the surface issues, the consent challenge, and the cookie scheme's login
  challenge are tenant-relative. The server-rendered forms post to page-relative `login` and `consent`. The session
  cookie is scoped to the tenant's path, so tenants that share an origin keep separate sessions. Path scoping is not an
  isolation boundary within an origin; select tenants by host when they must be isolated. The surface's endpoints also
  serve the `apps/auth` bundle's files beneath the tenant route, with `ETag` and `Last-Modified` validators, and every
  page they serve sets `<base href="{tenant path}/account/">`. The bundle builds with a relative base and resolves its
  API and page paths against `document.baseURI`, so one build works beneath any tenant. **Breaking:**
  `UseInteractiveAuthentication()` is removed (the host adds no middleware for the bundle), and
  `defineBackendServedSurfaceConfig` in `@ncode/vite-config` no longer takes a `base`. See
  [ADR-0193](docs/decisions/0193-the-interactive-surface-is-served-under-the-tenant-route.md).
- **The Management console scopes an initial access token to a resource server.** The Grants page's mint dialog
  offers an optional picker of the tenant's non-system resource servers, loaded while the dialog is open. It sends the
  choice as `resourceServerId`, and the one-time reveal names the resource server the registered client will be bound
  to. See [ADR-0192](docs/decisions/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md).
- **Dynamic client registration creates a client that introspects for a resource server.**
  `POST api/tenants/{tenantId}/grants/initial-access-tokens` accepts a `resourceServerId` that scopes the token to a
  resource server. The same checks apply as when the management API binds a client: the resource server exists in the
  tenant, is not a system resource server, and the caller may update it. A client registered with a scoped token is
  bound to that resource server, must be confidential, and cannot be made public by an RFC 7592 update. A bound client
  registers `introspection_signed_response_alg` and `introspection_encrypted_response_{alg,enc}` (RFC 9701 §6), which an
  ordinary client is refused. The client information response reports the binding as `resource_server_id`.
  **Breaking:**
  `IInitialAccessTokenValidator.ValidateAsync` returns the token's `InitialAccessTokenGrant` (or `null`) instead of a
  `bool`, and the registration endpoint validates a presented initial access token even when anonymous registration is
  allowed. See
  [ADR-0192](docs/decisions/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md).
- **Every key the management API reports carries its `kid`.** `SecretResource` and `CreatedSecretResource` have a
  required `KeyId`: the `kid` the key was imported with, or its secret identifier when it has none. The `@ncode/api`
  contract mirrors it, and the admin console's secret tables show it in a **Key ID** column. See
  [ADR-0186](docs/decisions/0186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md).
- **The Definition of Done fails a migration that would break the previous release.** `ExpandOnlyRuleTests`, in the new
  `NCode.Identity.OpenId.Host.Database.Tests` project, reads the `Up` operations of every host migration and fails one
  that drops or renames a schema, table, column, or sequence; adds a required column without a default; makes a column
  required without a default; changes a column's type; or shortens it. A deliberate contract step carries
  `[ContractMigration("why")]`, and the test also fails a marker on a migration that only expands. The existing
  `ReplaceTenantDomainNameWithIssuerHost` migration, which shipped before any release, carries the marker. See
  [ADR-0184](docs/decisions/0184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md).
- **The Management console rotates a client secret.** A client's shared secrets — the secret types the capabilities
  document reports as not asymmetric — carry a rotate action. After confirmation it replaces the secret with one that
  expires a year out and reveals the replacement once, as create does (`rotateClientSecret` in `@ncode/store`,
  `RotateSecretRequest` in `@ncode/api`). Issue [#155](https://github.com/NCodeGroup/Identity/issues/155) is complete
  ([ADR-0183](docs/decisions/0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md)).
- **A client secret is rotated in one call.** `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate`
  generates a replacement shared secret with the rotated secret's type, size, `use`, and `alg` and the expiry the
  request names (`RotateSecretRequest`), adds it and removes the rotated secret in one change, and returns the
  replacement's `CreatedSecretResource` — the only place its material appears. The caller must be allowed to create a
  secret for the client and to delete the rotated one; `If-Match` is honored, and a key answers `400`. The rotated
  secret stops being accepted at once; a client that needs an overlap still creates and deletes in two calls. Issue
  [#159](https://github.com/NCodeGroup/Identity/issues/159) is complete ([ADR-0183](docs/decisions/0183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md)).
- **The end-session endpoint accepts an `id_token_hint` the client re-encrypted to the server.** OpenID Connect
  RP-Initiated Logout 1.0 §2 lets a relying party that received an encrypted ID token re-encrypt the signed token to
  the OP. The hint is decrypted with the tenant's keys under the tenant's advertised `id_token_encryption_*` lists,
  and the enclosed ID token is checked as before. A tenant without an encryption key still accepts only a signed hint,
  and an ID token presented anywhere else is never decrypted. Issue [#115](https://github.com/NCodeGroup/Identity/issues/115) is complete
  ([ADR-0167](docs/decisions/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).
- **Dynamic client registration accepts the token endpoint and CIBA metadata.** A client registers
  `token_endpoint_auth_signing_alg` (OpenID Connect Dynamic Client Registration 1.0 §2) and
  `backchannel_token_delivery_mode`, `backchannel_client_notification_endpoint`,
  `backchannel_authentication_request_signing_alg`, and `backchannel_user_code_parameter` (CIBA Core 1.0 §4), and the
  client information response reports them. Each algorithm narrows the client's own list to it, under the tenant's
  ceiling. A `ping` or `push` client must register an https notification endpoint, and a client may claim the
  `user_code` parameter only where the tenant supports it; otherwise registration answers `invalid_client_metadata`.
  `backchannel_user_code_parameter_supported` now also applies to clients, where the registered flag is stored. Issue
  [#105](https://github.com/NCodeGroup/Identity/issues/105) is complete
  ([ADR-0155](docs/decisions/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).
- **A replacement token service can protect ID tokens as the client registered.** `IClientJwtCredentialSelector`,
  `ClientJwtCredentials`, and `ClientJwtProtectionSettings` are public in
  `NCode.Identity.OpenId.Authentication.Abstractions` (namespace unchanged). Every issuer of a JWT addressed to a client
  resolves the selector, and a deployment can register its own before `AddIdentityServer()`. Issue [#108](https://github.com/NCodeGroup/Identity/issues/108) is complete
  ([ADR-0181](docs/decisions/0181-every-issuer-of-a-jwt-addressed-to-a-client-resolves-one-public-credential-selector.md)).
- **The host's chart migrates the database before each rollout.** The host image now also carries the migration worker
  in `/app/migrations`, and the chart runs it as a Helm `pre-install` and `pre-upgrade` Job. A failed migration fails
  the release before any pod is replaced. The Job runs restricted like the server, and
  `migrations.database.existingSecret` gives it a connection with schema rights that the server does not need. Set
  `migrations.enabled=false` to apply migrations another way. Every migration must keep the previous release working:
  add first, drop or rename in a later release. The chart's pods now carry `app.kubernetes.io/component: server`, so
  the Service and PodDisruptionBudget never select the Job. Issue [#85](https://github.com/NCodeGroup/Identity/issues/85) is complete
  ([ADR-0180](docs/decisions/0180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md)).
- **The host has a Helm chart.** `charts/ncode-identity-host` deploys `NCode.Identity.OpenId.Host` under the
  restricted Pod Security Standard: non-root, read-only root filesystem, no capabilities, and no service-account token.
  The startup and liveness probes call `/health/live` and the readiness probe calls `/health/ready`, each sending an
  allowed `Host` header. A `preStop` sleep drains each pod before shutdown. The chart also ships a PodDisruptionBudget
  and an optional `Ingress`. The database, Valkey, and key-protection certificates are referenced as existing Secrets,
  never passed as values. Rendering fails for a release the host would refuse to start, for more than one replica
  without Valkey, and for unknown values. Without Valkey, updates never run two replicas at once. The new `Host chart`
  CI workflow lints the chart, renders each example release, and checks that each unsafe release is refused.
  ([ADR-0179](docs/decisions/0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md))

- **The host ships a production container image.** `src/NCode.Identity.OpenId.Host/Dockerfile` builds the host on
  the chiseled `aspnet:10.0-noble-chiseled-extra` runtime. The image has no shell, runs as the non-root `app` user
  (UID 1654), and listens on port 8080. It builds the `apps/auth` interactive bundle and serves it from `/app/auth`, so
  the container renders login and consent. The SDK and Node stages cross-build on the build platform, so the image
  builds for `linux/amd64` and `linux/arm64` without emulation, and the new `Host image` CI workflow builds both. The
  build context is an allow-list, and the Node major is a required `NODE_VERSION` build argument read from
  `src/frontend/.node-version`. **Breaking:** the image no longer exposes ports 80 and 443.
  ([ADR-0178](docs/decisions/0178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md))

- **The JOSE library creates and validates DPoP proofs.** `IDPoPProofService`, registered by `AddJoseLibrary()`,
  signs a `dpop+jwt` proof (RFC 9449) with any asymmetric `JoseSigningCredentials`. The proof embeds the public `jwk`
  and carries the `htm`, `htu`, `jti`, `iat`, and optional `ath` and `nonce` claims. `TryValidateProof` checks the
  structure, the `typ`, the absence of a `crit` header, an allow-listed asymmetric `alg` that matches the `jwk` key
  type, the signature, the HTTP binding, `ath`, and `iat` freshness. It returns the `jkt` thumbprint, `jti`, and
  `nonce`. The `jwk` is imported through the registered key types and verified by the registered signature algorithms,
  so `EdDSA` proofs (Ed25519 and Ed448) work once the opt-in EdDSA package is registered. The OpenId DPoP validator
  consumes the service and keeps the server-nonce challenge and the replay cache. **Breaking:**
  `OpenIdConstants.DPoP.ProofJwtType`, `.HttpMethodClaim`, `.HttpUriClaim`, and `.AccessTokenHashClaim` are removed in
  favor of `JoseTokenTypes.DPoPJwt` and the new `JoseClaimNames.Payload.Htm`, `.Htu`, and `.Ath`.
  ([ADR-0176](docs/decisions/0176-dpop-proofs-are-a-jose-building-block.md))

- **The management console's address bar names its scope and surface.** The console routes by scope and surface:
  `/tenants` and `/servers` in the global scope, and `/tenants/{tenantId}/clients`, `…/resource-servers`, `…/grants`,
  and `…/settings` within a tenant. A tenant view can be bookmarked, shared, and reloaded, back and forward move
  between surfaces and scopes, and a deep link that lands on the sign-in screen returns to the same page once signed
  in. The management scope is derived from the route. **Breaking:** the tenant's secrets and settings tab is now
  "Settings" (`tenant-tab-settings`), and a host that serves the console must answer every console path with
  `index.html`. ([ADR-0174](docs/decisions/0174-the-management-consoles-url-is-its-navigation-state.md))

- **`self_signed_tls_client_auth` matches the certificates in a client's JSON Web Key Set.** Importing a JSON Web Key
  keeps the first certificate of its `x5c` (RFC 7517 §4.7) after checking that it carries the key's public key, so a
  client that registers its keys as `jwks` or publishes them at a `jwks_uri` authenticates with a certificate listed
  there (RFC 8705 §2.2). A key whose `x5c` is empty, malformed, or carries another key is refused.
  ([ADR-0161](docs/decisions/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md))

- **Local and Copilot workflow formats are intentionally supported together ([#179](https://github.com/NCodeGroup/Identity/issues/179) closed).** Keep the Local prompts
  and Copilot skills; no bulk prompt migration is needed. A new first Definition-of-Done stage checks every pair's
  name, description, and ordered workflow body, with fixture tests proving that missing adapters, drift, and unsupported
  skill settings fail. This covers the generated workflows and the hand-written audit pair. Skills use the session's
  selected model and remain available for automatic selection; Local prompts retain their model-selection metadata.
  [AGENTS.md](AGENTS.md#local-and-copilot-customizations) explains model selection and the migration banner, and
  [ADR-0170](docs/decisions/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md#dual-runtime-customization-contract)
  records the completed dual-runtime decision without an upstream-fix dependency.

- **A resource server can introspect through a client that acts for it, and receive the answer as a signed JWT.** A
  client created with `CreateClientRequest.resourceServerId` acts for that resource server: it authenticates like any
  confidential client but is accepted only at the introspection endpoint, and the binding never changes. The resource
  server must be in the client's tenant and must not be a system resource server, and the caller must be allowed to
  update it. `ClientResource.resourceServerId` reports the binding, `GET …/clients?resourceServerId=` lists a resource
  server's clients, and a resource server with bound clients cannot be deleted. Such a client that sends
  `Accept: application/token-introspection+jwt` receives the response as a JWT typed `token-introspection+jwt` (RFC
  9701). The JWT is signed with its `introspection_signed_response_alg` (default `RS256`) and encrypted when it registers
  `introspection_encrypted_response_alg` / `_enc`. Discovery advertises the
  `introspection_{signing_alg,encryption_alg,encryption_enc}_values_supported` lists, and the client-authentication
  methods and algorithms of the introspection and revocation endpoints. The management console's resource-server page
  gains a **Clients** tab that lists and creates them. `JwtEncodingParameters.EnsureTokenLifetime` lets a JWT that is
  not a credential omit `exp`. See
  [ADR-0168](docs/decisions/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md).

- **Every tenant is provisioned with persisted keys, so a production deployment can sign from its first request.** On a
  fresh production database no tenant had a key, so the JSON Web Key Set was empty and nothing could be signed. That
  included the bootstrap administrator's first management token, which is the only credential that could import a key.
  Now every tenant is given a persisted key, when it is seeded, for each configured algorithm it holds no usable key
  for:
  - **When:** the root tenant on its first request, a tenant created through the management API before the create
    returns, and an existing tenant at its next seed.
  - **Defaults:** `RS256` and `PS256` (3072-bit RSA) and `ES256` (P-256).
  - **Configuration:** `OpenId:Tenant:KeyProvisioning:Keys` names any registered asymmetric signature or key-management
    algorithm (with an optional `KeySizeBits`). Configuring it replaces the defaults, and it is validated when the host
    starts. `OpenId:Tenant:KeyProvisioning:Enabled=false` turns provisioning off for a deployment that brings its own
    keys.
  - **Existing keys are never replaced.** A key the tenant already holds — imported or generated — suppresses generation
    and is never replaced, rotated, or removed. A key the Data Protection key ring can no longer unprotect fails the
    tenant with a message naming the tenant and the key ring, rather than being replaced.

  See [ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md).

- **A tenant can be created without an issuer, and is given the one its tenant strategy derives.** The host and path
  strategies take an issuer template — `OpenId:TenantResolution:DynamicByHost:IssuerTemplate =
  https://{tenantId}.auth.example`, `DynamicByPath:IssuerTemplate = https://op.example/{tenantId}` — and a tenant created
  through the management API without an issuer is given its expansion, stored with it and judged by the same rules as an
  explicit issuer. Changing the template never moves an existing tenant, and clearing a tenant's issuer restores the
  derived one. The root tenant takes `OpenId:TenantResolution:Root:Issuer` when it is set (the hosted platform's
  `https://auth.<apex>` beside `https://{tenantId}.auth.<apex>`), and its address is reserved for it. A template is
  validated when the host starts: it carries `{tenantId}` once, varies the host under the host strategy, and ends with
  the tenant route under the path strategy. See
  [ADR-0165](docs/decisions/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md).

- **The management API previews a tenant's address before it is written.** `GET /api/tenants/{tenantId}/address`
  (optionally `?issuer=`) returns the issuer the tenant has or would be given, whether it is the derived one, whether
  the tenant already exists, and every reason the tenant could not be served there — the same rules the create and update
  apply. The management console's tenant form previews the address as you type: it shows the issuer an empty field
  resolves to, flags a taken tenant ID, and lists why a proposed issuer cannot be used.

- **The root tenant is provisioned under every tenant strategy, and every tenant is seeded.** Under the host and path
  strategies the root (control-plane) tenant is created when the first request reaches its address, so a multi-tenant
  deployment boots from configuration alone: the bootstrap administrator, named into the root tenant, obtains the first
  management token there. A tenant created through the management API is seeded before the response — its OpenID and
  management resource servers exist before its first request — and every tenant is re-seeded when it is next
  materialized, so a seed contributor added by a deploy reaches existing tenants under every strategy. See
  [ADR-0166](docs/decisions/0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md).

- **Several replicas can now share DPoP nonces and single-use records through Valkey.** The new
  `NCode.Identity.SharedState.Abstractions` package defines `IExpiringKeySet`, an atomic add-if-absent set of
  expiring keys; `NCode.Identity.SharedState` provides the in-process adapter `AddIdentityServer()` registers by default
  (correct for one replica), and the opt-in `NCode.Identity.SharedState.Valkey` package adds
  `SharedStateLibraryBuilder.AddValkey(configuration)`, which uses the `ConnectionStrings:valkey` connection string
  and binds the `Valkey` section (`KeyPrefix`) strictly. Without the connection string it registers nothing. It adds a
  `valkey` health check tagged `ready`. The reference host calls it, and the Aspire AppHost runs a Valkey container and
  injects the connection string, so local development uses the same adapter as a multi-replica deployment. See
  [ADR-0164](docs/decisions/0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md).

- **CI runs now show their results on GitHub instead of only in zip artifacts.** The run summary page opens with the
  Definition of Done verdict and a per-stage duration table, then the test results (one table across every test
  assembly or frontend package, plus each failed test's message and stack) and the merged coverage summary. Failed
  tests are annotated at their source line — on the run page and inline in pull-request diffs — and a failed gate
  names the stage that broke. Coverage history, per-line diff coverage on pull requests, and test analytics are on
  [Codecov](https://codecov.io/gh/NCodeGroup/Identity), uploaded over OIDC from a separate job so the gate jobs stay
  read-only and no upload secret is stored. Codecov's statuses are informational; "Definition of Done" remains the
  only required check. The frontend tier gains the same reports and coverage (`pnpm run test:report`), and the
  `coverage` artifact now holds a browsable HTML report.

  Locally, `./build/dod.ps1` writes each run's results to `artifacts/test-results/` (cleared first) and the coverage
  report to `artifacts/coverage/index.html`. It previously merged coverage from every run ever left in the test
  projects' `TestResults/` folders, so coverage figures from before this change overstated or understated the real
  number. See [ADR-0143](docs/decisions/0143-ci-presents-test-results-and-coverage-in-github.md).

- **Every development-only convenience is now behind one self-gating call.** `AddDevelopmentEnvironment(configuration,
  hostEnvironment)` registers the developer CORS policy, the developer and conformance seeders, and the developer
  signing keys, and does nothing at all in Production.

  These were four separate calls, each gated only by its own configuration section, which spread the decision "is this
  a development host?" across four places in the composition root — and meant a fifth convenience had to remember to
  gate itself the same way. Each piece remains individually inert when its own section is absent, so this narrows
  behaviour without removing any opt-in.

  This is a second line of defence, not the primary one: `AddProductionSafety` already refuses to start such a
  deployment and reports the problem, rather than silently changing what gets registered. The environment gate
  matters for a host that has not called it.

- **The server refuses to start in Production with development-only settings still enabled.**
  `AddProductionSafety(configuration, hostEnvironment)` validates configuration during composition and throws before
  the port is bound. It checks the signing-key source, `AllowedHosts`, developer CORS configuration, and the two seeders
  (`DeveloperInteractiveSeed`, `ConformanceSeed`), and reports **every** finding in one message so a misconfigured
  deployment is fixed in a single pass rather than one restart per problem. It is inert in every non-Production
  environment, so local, test, and conformance hosts keep their conveniences.

  Each setting checked is a convenience that is correct in Development and dangerous on a public origin, and each is
  **silent** — a deployment that forgets to override it starts, serves traffic, and looks healthy. That is why these
  are startup failures rather than log warnings.

  `DeveloperKeyMode` gains `None`, which is how a host states that it supplies its own stable, operator-managed
  signing key; `AddDeveloperKeys(None)` registers nothing. `None` is deliberately **not** the zero value: the default
  must stay `Ephemeral` so a developer who has configured nothing still gets a working, hermetic, zero-setup host.
  A Production deployment therefore has to state its intent explicitly rather than inherit it, and an unset
  `DeveloperKeys:Mode` is reported as a violation precisely because it silently means `Ephemeral`.

- **UserInfo responses can be signed and encrypted, and a client registers how its ID tokens and UserInfo responses are
  protected.** The OpenID Connect registration metadata `id_token_signed_response_alg` (default `RS256`),
  `id_token_encrypted_response_alg` / `_enc`, `userinfo_signed_response_alg`, and `userinfo_encrypted_response_alg` /
  `_enc` (`enc` defaults to `A128CBC-HS256`) are client settings — a server or tenant may set the default for its
  clients — and dynamic client registration accepts them, together with `request_object_signing_alg` and
  `request_object_encryption_alg` / `_enc`. A client that registers a UserInfo algorithm receives an `application/jwt`
  response carrying `iss` and `aud`; one that registers encryption receives an ID token, logout token, or UserInfo
  response encrypted to a public key from its `jwks` or `jwks_uri` (signed and encrypted is a Nested JWT with `cty`
  `JWT`). Each registered value must be in the matching `*_values_supported` list: registration refuses one that is not
  with `invalid_client_metadata`, and issuance fails with `server_error` rather than substitute another algorithm.
  Registering an encrypted response requires `jwks` or `jwks_uri`, and the two may no longer both be registered. See
  [ADR-0155](docs/decisions/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md).

### Changed

- **The OpenID protocol client needs no ASP.NET Core runtime.** `NCode.Identity.OpenId.Client` no longer references
  the `Microsoft.AspNetCore.App` shared framework and references
  `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Http`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` and `Microsoft.Extensions.Primitives`
  directly, so a console app, worker or function runs it on the .NET runtime alone; an authorization URI
  percent-encodes every parameter character outside the RFC 3986 unreserved set (`! * ( ) @` included)
  ([ADR-0285](docs/decisions/0285-the-openid-protocol-client-needs-no-aspnet-core-runtime.md),
  issue [#321](https://github.com/NCodeGroup/Identity/issues/321)).

- **Audit events are CloudTrail-shaped JSON records.** `AuditRecordWriter` writes each audit event as one compact,
  snake_case record whose field names are the constants of `AuditRecordFields`: `schema_version` (`"1.0"`), `event_id`
  (a UUIDv7), `event_time` (UTC), `event_name`, `event_category`, `event_source`, `outcome`, `severity`, `tenant_id`,
  `subject_id`, `client_id`, the `actor`, `request` and `error` objects, a `resources` array of `{type, id}` and an
  event-specific `details` object that the event writes through `ISupportAuditDetails`. Null members and an empty
  `resources` are omitted. `AuditActor`, `AuditRequest`, `AuditResource` and `AuditError` model the objects, and
  `AuditActorTypes`, `AuditSeverities`, `AuditEventCategories`, `AuditEventSources`, `AuditResourceTypes`,
  `OpenIdAuditEventNames` and `ManagementAuditEventNames` hold the open vocabularies. `AuditRequestFactory` builds
  `request` from the HTTP context: the request, trace and span ids, the source IP, the User-Agent, the method, the path
  (never the query string) and the endpoint. `DefaultAuditLoggingHandler` logs the record under the category
  `NCode.Identity.Audit` with EventId 14005 and a state of `audit.*` key/value pairs, the audit span event carries the
  same `audit.*` tags, and the audit metrics gain the `ncode.identity.audit.event_category` attribute. A refresh-token
  replay logs at Critical. Breaking: `AuditEvent.Action` is `EventName`, `Timestamp` is `EventTime` and
  `OpenIdAuditActions` is `OpenIdAuditEventNames`; `AuditEvent.EventName` and `EventCategory` are abstract and
  `EventSource` is required; `SchemaVersion`, `CorrelationId`, `ActorId`, `Source` and `Reason` are removed, and
  `OpenIdErrorEvent.ErrorCode`, `ErrorDescription` and `StatusCode` are replaced by `Error`;
  `IAuditEventRecorder.RecordAuthorizationDeniedAsync` and `RecordClientAuthenticationFailedAsync` take `errorCode` in
  place of `reason`, and `RecordSubjectAuthenticationFailedAsync` takes `(errorCode, errorMessage, attemptedSubjectId)`;
  the metric attribute `ncode.identity.audit.action` is `ncode.identity.audit.event_name`; EventIds 14003 and 14004 are
  retired
  ([ADR-0283](docs/decisions/0283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md),
  [audit records](docs/audit-records.md)).

- **The JWK package names every RSA private member.** `JsonWebKeyParameterNames` adds `FirstPrimeFactor` (`p`),
  `SecondPrimeFactor` (`q`), `FirstFactorCrtExponent` (`dp`), `SecondFactorCrtExponent` (`dq`),
  `FirstCrtCoefficient` (`qi`) and `OtherPrimesInfo` (`oth`), and the RSA key type descriptor uses them. Breaking:
  `JsonWebKeyParameterNames.EccPrivateKey` is renamed `D`, because the `d` member is also the RSA private exponent
  and the OKP private key (issue [#364](https://github.com/NCodeGroup/Identity/issues/364)).

- **Key wrapping is a shared secrets abstraction.** Every key wrapper implements `IKeyWrapper` (`Name`, `WrapAsync`,
  and `UnwrapAsync`, which writes the key into an `IBufferWriter<byte>`) and returns `WrappedKey` (`WrapperName`,
  `KeyId`, `Ciphertext`); both, with `KeyWrapperNames`, live in `NCode.Identity.Secrets.Abstractions` under
  `NCode.Identity.Secrets.KeyWrapping`, and the offline recovery wrapper implements the same contract. Breaking: `IRootKeyProvider`, `RootWrappedKey` and `RootKeyProviderNames` are removed;
  `LocalRootKeyOptions` is `LocalKeyWrapperOptions`; the packages `NCode.Identity.Secrets.Persistence.AwsKms`,
  `.AzureKeyVault` and `.GoogleCloudKms` are `NCode.Identity.Secrets.KeyWrapping.AwsKms`, `.AzureKeyVault` and
  `.GoogleCloudKms`, with `Add<Vendor>KeyWrapper` and `<Vendor>KeyWrapperOptions` in place of
  `Add<Vendor>RootKeyProvider` and `<Vendor>RootKeyOptions`. Stored master keys, configuration keys and wrapper names
  are unchanged
  ([ADR-0279](docs/decisions/0279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md)).

- **The OAuth and OpenID Connect wire vocabulary lives in a dependency-free package.** The new
  `NCode.Identity.OpenId.Protocol.Abstractions` package holds `OpenIdProtocol`, shared by the OP and the protocol
  client. The groups `Parameters`, `ErrorCodes`, `GrantTypes`, `ResponseTypes`, `ResponseModes`, `CodeChallengeMethods`,
  `TokenTypes`, `TokenTypeIdentifiers`, `ClientAuthenticationMethods`, `ClientAssertionTypes`, `PromptTypes`,
  `DisplayTypes`, `ScopeTypes`, `SubjectTypes`, `BackchannelTokenDeliveryModes`, `DPoP`, `ConfirmationMethods`,
  `MediaTypes`, `ClaimTypes`, `AuthenticationMethods` and `AuthenticationContextClassReferences` moved from
  `OpenIdConstants` to `OpenIdProtocol`, and `TokenTypeHints`, `MetadataNames`, `WellKnownPaths` and `BackChannelLogout`
  are new. Breaking: `OpenIdConstants.ContentType` is now `OpenIdProtocol.MediaTypes.FormUrlEncoded`,
  `OpenIdConstants.BackChannelLogout.EventsClaimName` is now `JoseClaimNames.Payload.Events`, the `$` parameters moved
  to `OpenIdConstants.InternalParameters`, and the client's `ClientAuthenticationMethods`, `TokenTypeHints` and
  `TokenTypeIdentifiers` are removed in favour of `OpenIdProtocol`'s
  ([ADR-0276](docs/decisions/0276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md),
  [#353](https://github.com/NCodeGroup/Identity/issues/353)).

- **A key type registers its JSON Web Key rules.** A `JsonWebKeyTypeDescriptor` carries a key type's thumbprint members
  and private members; `JsonWebKeyTypeDescriptors` holds the built-in IANA types, and `JsonWebKey.KeyTypeDescriptor`
  exposes the one a key uses, set through the new `JsonWebKey(JsonElement, JsonWebKeyTypeDescriptor)` and
  `JsonWebKey(JsonObject, JsonWebKeyTypeDescriptor)` constructors. Breaking: `ISecretKeyTypeDescriptor.JwkKeyType` is now
  a `JsonWebKeyTypeDescriptor?` instead of a `string?`. DPoP proof validation, client key import and provider key import
  build typed keys, so a contributed key type's thumbprint and private members work
  ([ADR-0271](docs/decisions/0271-a-key-type-registers-its-json-web-key-rules.md)).

- **A generated private key stores its public key.** `PersistedSecret.PublicKey` holds the base64url SPKI of every
  generated asymmetric key, set from the new required `GeneratedSecretKeyMaterial.PublicKey`, and
  `PersistedSecret.HasPublicKey()` tells whether a secret has one. `ISecretSerializer.DeserializePublicSecret` reads it,
  so the management public-key endpoints and the registration response no longer use a protector and
  `ISecretPublicKeyConverter.Convert(PersistedSecret)` replaces `Convert(ISecretProtector, PersistedSecret)`. The
  `AddSecretPublicKey` migration adds the nullable column; a key generated earlier must be rotated to expose its public
  key ([ADR-0268](docs/decisions/0268-a-generated-private-key-stores-its-public-key-beside-it.md), issue
  [#176](https://github.com/NCodeGroup/Identity/issues/176)).
- **The conformance seed keys its clients by client id.** `ConformanceSeedOptions.Clients` is now an object keyed by
  client id (`ConformanceSeed:Clients:<client_id>:…`), so an environment override names its client rather than an array
  position. `ConformanceClientSeed.ClientId` is removed, and an entry that still sets it fails strict binding at
  startup. `run-conformance.ps1 -Fapi1Advanced` supplies each key set through `ConformanceSeed__Clients__<client_id>__Jwks`,
  renders both FAPI configurations from one `fapi1-advanced.template.json` and takes the OP's URLs from `-IssuerUrl`
  ([#235](https://github.com/NCodeGroup/Identity/issues/235)).
- **The plan-and-execute workflow comes from the project template, executes on Claude Sonnet 5.5, and runs in Claude
  Code too.** The repository adopted [project-template](https://github.com/polewskm/project-template) `v0.3.0` and
  updated to `v0.35.1`: its agents, prompt files, skills, Claude Code commands, `CLAUDE.md`, `commit-msg` hook and
  `.template/` scripts are template-owned, `.copier-answers.yml` and `.template-version` record the version, and
  `./.template/Update-Project.ps1` takes a newer one. `v0.4.0` keeps the update from failing on Windows with
  `WinError 206` (the filename or extension is too long) when the repository's `.gitignore` ignores a folder the
  template's does not, here `src/frontend/node_modules`. From `v0.6.0`, `PLAN.md` is gitignored and never committed:
  a plan's first commit is an empty `Plan: <title>`, each step's checkpoint stages only the files the step lists, and a
  re-plan is an empty commit. From `v0.9.0`, an executor that cannot run a step's Done-when command says so and leaves
  the check to the coordinator, and `v0.10.0` adds `/finish-plan`, which retires a merged plan, fast-forwards `main` and
  deletes the plan's branch and worktree. From `v0.11.0`, `/finish-plan` asks `gh` for the branch's merged pull
  request, so a merge-queued plan no longer stops to ask, and the Claude Code executor runs commands with the
  PowerShell tool, so a plan made of commands runs in its worktree; `v0.12.0` lets a plan name the issue it
  implements on an `Issue:` line, posts the plan to that issue and ends the pull request description with
  `Closes #<number>`. `v0.13.0` adds `/plan-issue`, which plans one issue from its text and comments, and
  `/triage-issues`, which lists the open issues ready to plan, and every planning command treats an issue's text as
  data, not instructions; `v0.14.0` adds `/plan-template-update`, which plans a template update from a trial run of
  it. From `v0.15.0`, `.gitignore` ignores and Claude Code denies reading only the root `secrets/` folder, so a new
  file in a `Secrets/` source folder, such as `Contracts/Secrets/`, is no longer left out of commits on Windows.
  `v0.16.0` keeps the job's token out of `.git/config` with `persist-credentials: false`, which every checkout here
  except `copilot-setup-steps.yml`'s now sets, and `v0.17.0` starts the `commit-msg` hook with `#!/usr/bin/env sh`,
  so pre-commit on Windows can run it. From `v0.19.0`, `/ship-plan` lands a reviewed plan as the `## Landing` section
  of `AGENTS.md` says, here as a draft pull request (`/ship-plan draft`) with no release, and `/continue-plan` shows
  where each open plan stands; `v0.23.0` adds `/run-issues`, which takes issues from plan to a draft pull request one at
  a time. From `v0.25.0`, `/ship-plan` files a plan's follow-ups as issues labelled `follow-up`; `v0.27.0` lets one plan
  cover up to three related issues; and `/run-issues` now also answers top-level pull request comments and review
  summaries and plans the next issue while a pull request waits for review. The Landing section sets the keys
  `v0.28.0`–`v0.32.0` add: `draft: yes`, so `/ship-plan` always opens a draft pull request, and `gate: ./build/dod.ps1
  -Fast -Affected`, which `/ship-plan` and `/run-issues` run before each push. A plan's `Plan step` checkpoints now
  reach its pull request's branch, and its title is the conventional commit subject the squash merge takes. Plan steps
  execute on Claude Sonnet 5.5 instead of Auto in VS Code, Copilot CLI and Claude Code. The repository-wide agent rules
  moved from `.github/copilot-instructions.md` to `.github/instructions/repository.instructions.md`. The `Commit
  attribution` check now runs the template's `./.template/Test-CommitAttribution.ps1`, which replaces
  `./build/commit-attribution.ps1` and its Definition-of-Done stage, and the template's hook strips a "Generated with
  [Claude Code" line only when the phrase starts the line
  ([ADR-0170](docs/decisions/0170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md)).

- **A required check rejects commits attributed to an AI agent.** `./build/commit-attribution.ps1 -Base <rev>` fails
  when the author or committer of a commit in `Base..Head` is an AI agent account, such as the Copilot cloud agent's
  `copilot-swe-agent[bot]`, or when its message carries agent attribution: a `Co-authored-by:` line naming an agent, a
  `Claude-Session:` line, or a "Generated with [Claude Code" line. The `Commit attribution` workflow runs it on pull
  requests, in the merge queue (the commit that lands, including the trailers GitHub writes when it squashes) and on
  pushes to `main` and `release/**`. The `commit-msg` hook now also strips Claude attribution, and Claude Code's and
  VS Code's settings no longer add it. A pull request from the cloud agent fails the check until its commits are
  re-authored with `git rebase origin/main --exec "git commit --amend --no-edit --reset-author"` and
  `git push --force-with-lease`
  ([ADR-0128](docs/decisions/0128-commits-are-attributed-to-people-not-agents.md)).

- **Minimal-API request delegates are generated at compile time, so a host starts faster.** Every package builds with
  the Request Delegate Generator, so a host no longer compiles each endpoint's expression trees before it answers its
  first request, and the integration tests run in 41 s instead of 5m58s.
  `NCode.Identity.OpenId.Authentication.Interactive` still binds its endpoints at runtime, because the generated form
  binding answers a form whose antiforgery validation failed with 500 instead of 400
  ([ADR-0262](docs/decisions/0262-minimal-api-request-delegates-are-generated-at-compile-time.md), issue [#186](https://github.com/NCodeGroup/Identity/issues/186)).

- **Feature branches run a fast gate, and the merge queue runs the full gate.** `./build/dod.ps1` gains `-Affected`
  (with `-Base`), which runs only the test projects a change reaches through the project graph, and `-Filter`, which
  passes a `dotnet test --filter` expression through; test assemblies run in parallel in every mode. A pull request
  into `main` runs the affected tests, and the merge queue, `main` and the `release/**` lines run the full gate, so a
  branch no longer has to be up to date with `main` before it merges
  ([ADR-0263](docs/decisions/0263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md)).

- **Breaking (frontend): the management console reaches every owner's secrets and settings through one endpoint per
  operation.** `@ncode/store` exposes `useListSecretsQuery`, `useCreateSecretMutation`, `useDeleteSecretMutation`,
  `useRotateSecretMutation`, `useLazyGetSecretPublicKeyQuery`, `useGetSettingsQuery`, `useGetEffectiveSettingsQuery` and
  `useUpdateSettingsMutation`, each taking a `ManagementOwner` from `@ncode/api`, in place of a hook per owner type, and
  `OwnerSecretsTab` and `OwnerSettingsTab` serve every detail page
  ([ADR-0257](docs/decisions/0257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md),
  issue [#236](https://github.com/NCodeGroup/Identity/issues/236)).

- **The `apps/auth` bundle's files are cached for a year and served without resolving the tenant.** The host serves
  only the bundle's content-hashed `assets` directory, at `GET|HEAD {tenant}/account/assets/{**asset}`, with
  `Cache-Control: public, max-age=31536000, immutable` and its `ETag` and `Last-Modified` validators, from a route
  group that resolves no tenant, so a returning browser loads the bundle without a request. Other files of the
  bundle's directory, such as `index.html`, are not served, and `@ncode/vite-config` pins `build.assetsDir` to
  `assets`
  ([ADR-0254](docs/decisions/0254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md),
  issue [#221](https://github.com/NCodeGroup/Identity/issues/221)).

- **The host embeds a tenant's branding in the page, and `GET {tenant}/account/branding` is removed.** Every page
  served with the `apps/auth` bundle carries the tenant's branding in a `<script type="application/json"
  id="branding">` block after its `<base>`, and the bundle themes itself from it without a request.
  `InteractiveAuthenticationDefaults.BrandingPath` is removed
  ([ADR-0253](docs/decisions/0253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md), issue
  [#211](https://github.com/NCodeGroup/Identity/issues/211)).

- **Each of a tenant's own surfaces in the management console has its own address.** The tenant's secrets,
  settings, and usage are tenant surfaces at `/tenants/{tenantId}/secrets`, `/settings`, and `/usage`, each with its
  own tab, so they can be bookmarked, shared, and restored with back and forward
  ([ADR-0255](docs/decisions/0255-each-tenant-surface-of-the-management-console-has-its-own-address.md), issue [#236](https://github.com/NCodeGroup/Identity/issues/236)).

- **A migration classifies its data change, and a constraint added to an existing table is a contract step.**
  `ExpandOnlyRule.Check(Migration)` judges each host migration: one that runs raw SQL or changes seed data carries
  `[ExpandMigration("why")]` (added) when the release still serving reads every row it writes, or
  `[ContractMigration("why")]` when it does not, and every existing migration is classified. The rule also reports a
  column made required even with a default, a removed default, a narrowed precision or scale, a unique index, unique
  constraint, primary key, check constraint or foreign key added to an existing table, and a foreign key that restricts
  deleting rows of an existing table
  ([ADR-0250](docs/decisions/0250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md),
  issue [#205](https://github.com/NCodeGroup/Identity/issues/205)).

- **Breaking: a retired setting stays registered until a later release removes it.** `SettingDescriptor.RetiredReason`
  and `IsRetired` retire a setting: reads ignore it, `ValidateSettings` and the configuration reader refuse it, and
  `ISettingSerializer.ValidateSettingsUpdate`, a member that every implementation adds, accepts it only with its stored
  value or removed; the management API's settings updates validate through it. `mtls_endpoint_alias_authority` is
  retired: `PromoteMtlsEndpointAliasAuthority` copies each tenant's value and keeps the setting for the previous
  release, and its `Down` writes the alias back
  ([ADR-0251](docs/decisions/0251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md), issue [#206](https://github.com/NCodeGroup/Identity/issues/206)).

- **Breaking: rate limits and quotas are registered descriptors judged in one round trip per stage.** Each limit is a
  `RateLimitDescriptor` (name, stage, order, window, sliding or fixed, limit and overage settings, metered only, usage
  meters, partition selector) contributed through `ICollectionDataSource<RateLimitDescriptor>`; the five built-ins
  (`RateLimitPartitions`) keep their settings and behavior.
  `IRequestRateLimiter.AcquireAsync(stage, RateLimitContext, CancellationToken)` and `RateLimitDecision` are public in
  `NCode.Identity.OpenId.Authentication.Abstractions`, so a package judges a stage of its own (`RateLimitStages`); the
  limiter marks a refused request, and `RateLimitEndpointFilter` records the usage meters once the endpoint has run.
  `IExpiringCounterSet.IncrementUntilExceededAsync` counts a stage's limits in one call (one Valkey script, measured as
  `counter.increment_until_exceeded`), and every implementation of the port implements it. Shared-state keys carry the
  tenant in braces as a hash tag, so counters and replay records in flight across the upgrade start over. The
  `QuotaExceeded` log event (2044) is retired, and `RateLimitExceeded` names the window
  ([ADR-0246](docs/decisions/0246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md), issue
  [#232](https://github.com/NCodeGroup/Identity/issues/232)).

- **The host image writes one-line JSON logs.** The image sets `Logging__Console__FormatterName=json`,
  `Logging__Console__FormatterOptions__UseUtcTimestamp=true` and
  `Logging__Console__FormatterOptions__TimestampFormat=O`, so every entry, the migration worker's included, is one JSON
  object on one line with a UTC timestamp; `Logging__Console__FormatterName=simple` restores text
  ([ADR-0248](docs/decisions/0248-the-host-image-writes-one-line-json-logs.md), issue [#265](https://github.com/NCodeGroup/Identity/issues/265)).

- **Breaking: a client value that a ceiling lowers is incoherent.** `RegisteredValueCoherenceRule` checks every
  setting a client registers whose merge is a meet (`Min`, `And` or `Intersect`, which the added abstract
  `SettingDescriptor.HasMeetMerge` states), so a lifetime, timeout, size or rate above what the tenant, the server or a
  selected security profile permits is refused at registration and at a management write, a tenant or server write
  that would lower it below a client's value is refused, and the reconciliation and the incoherent-client report list
  it: `The 'access_token_lifetime' value '01:00:00' exceeds the effective 'access_token_lifetime' value '00:05:00'.` A
  value that a ceiling already lowers blocks no write that moves that ceiling, because a write matches a violation
  that predates it without its description. A floor (`Max`, `Or`, `Union`) is never reported
  ([ADR-0240](docs/decisions/0240-a-client-value-that-a-ceiling-lowers-is-incoherent.md), issue [#262](https://github.com/NCodeGroup/Identity/issues/262)).

- **The in-process dispatcher runs jobs concurrently.** Up to `InProcessJobDispatcherOptions.MaxConcurrentJobs` jobs
  (16 by default) run at once, so a delivery that waits on an unreachable endpoint holds one worker while the jobs
  behind it run on the others. `MaxConcurrentJobs` and `QueueCapacity` must be at least 1, which the host checks when it
  starts ([ADR-0232](docs/decisions/0232-the-in-process-dispatcher-runs-jobs-concurrently-up-to-a-limit.md),
  issue [#276](https://github.com/NCodeGroup/Identity/issues/276)).

- **Breaking: a reconciliation of the server's settings reaches only the tenants its change affects.**
  `PersistedServer.ReconciledSettings` records the effective settings every tenant and client was reconciled against
  (the `AddServerReconciledSettings` migration adds the `Servers.ReconciledSettingsJson` column), and
  `ReconcileServerSettingsCommand.PreviousSettings` carries them to the handlers: `TenantKeyReconciliationHandler` reads
  the keys only of a tenant that uses an algorithm it did not use before, and `ClientCoherenceReportHandler` checks the
  clients only of a tenant whose effective settings changed in a setting a coherence rule reads.
  `IServerStore.UpdateReconciledSettingsAsync` records the fingerprint and the settings and takes the place of
  `UpdateReconciledSettingsFingerprintAsync`, and `IServerStore.UpdateSettingsAsync` keeps the recorded settings unless
  the settings of an earlier change were never reconciled
  ([ADR-0234](docs/decisions/0234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md),
  issue [#279](https://github.com/NCodeGroup/Identity/issues/279)).

- **Breaking:** `ISecretSerializer` and `ISecretGenerator` take an `ISecretProtector`; `ISecretSerializer` gains
  `DeserializePublicSecret`. Imported public keys (`spki`, `x509`) are stored as plain base64url.

- **Breaking:** the Data Protection key ring is encrypted under the server master key; configure the `SecretProtection`
  section instead of `DataProtection:KeyProtection`, and the chart's `secretProtection` values instead of
  `keyProtection` (the chart's schema, render-time refusals and CI releases change with it).

- **Breaking (data):** values protected by the old scheme are not readable. In Development, unreadable tenant keys are
  pruned and re-provisioned automatically; reissue client secrets or reset the local database.

- **Local development:** the LocalDev AppHost generates one development root key per machine (in local application
  data, never in the repository) and gives it to both OP instances that share the development database; a Development
  host started any other way uses the ephemeral root key.

- Deleting a tenant deletes its master keys.

- **Breaking: a session's lifetimes are required, and a listed session always reports when it ends.**
  `CreateSessionRequest.AbsoluteLifetime` and `CreateSessionRequest.IdleTimeout` are required positive `TimeSpan`s and
  `CreateSessionRequest.Lifetime` is removed; `SessionGrant.AbsoluteExpiresWhen` records the absolute end;
  `IPersistedGrantService.UpdatePayloadAndExpirationAsync` is a member a custom implementation implements; and
  `SessionInfo.ExpiresWhen`, `SessionResource.ExpiresWhen` and `expiresWhen` of `/account/sessions/list` are the time
  the session ends unless it is used again, never `null`
  ([ADR-0220](docs/decisions/0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md), issue [#271](https://github.com/NCodeGroup/Identity/issues/271)).

- **Breaking: a cached tenant expires 15 minutes after it is materialized, however often it is used.**
  `OpenIdTenantOptions.TenantCacheLifetime` (`OpenId:Tenant:TenantCacheLifetime`) is an absolute lifetime that
  replaces the 4-hour sliding `TenantCacheExpiration`, so a replica that missed a tenant's change announcement serves
  the tenant at most that long
  ([ADR-0219](docs/decisions/0219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issue [#249](https://github.com/NCodeGroup/Identity/issues/249)).

- **Breaking: `OpenIdServer.RefreshSettingsAsync`, `IOpenIdTenantCache.ClearAsync`,
  `IReplicaBroadcastHandler.HandleMissedMessagesAsync`, and `IServerStore.UpdateReconciledSettingsFingerprintAsync`
  are members a custom implementation implements**, and `IServerStore.UpdateSettingsAsync` clears the server's
  `ReconciledSettingsFingerprint` in the same unit of work
  ([ADR-0219](docs/decisions/0219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issues [#248](https://github.com/NCodeGroup/Identity/issues/248),
  [#249](https://github.com/NCodeGroup/Identity/issues/249) and [#252](https://github.com/NCodeGroup/Identity/issues/252)).

- **The tenant re-seed after a server settings change is part of the reconciliation, not a job on `IJobDispatcher`.**
  It also runs after a configuration reload and a deployment, it holds no dispatcher worker, and it evicts only the
  tenants that hold keys but lack one their settings use. `NCode.Identity.OpenId.Core` does not reference
  `NCode.Identity.Dispatch.Abstractions`
  ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-0219](docs/decisions/0219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issue [#252](https://github.com/NCodeGroup/Identity/issues/252)).

- **Breaking: refresh-token rotation goes through `ITokenService.RotateRefreshTokenAsync`, which stores the successor
  with `IPersistedGrantService.ReplaceOnceOrDefault`; `IPersistedGrantService.TryRevokeOnceAsync` spends a token that
  has no successor.** A custom implementation implements the three members
  ([ADR-0211](docs/decisions/0211-a-rotated-refresh-token-is-spent-with-its-successor.md), issue [#260](https://github.com/NCodeGroup/Identity/issues/260)).

- **Breaking: deleting a client removes everything that exists for it, and `IClientStore.HasDependentsAsync` is
  removed.** `IClientStore.RemoveAsync` removes its secrets, grants to resource servers, consents, and the grants issued
  to it in the same unit of work; the management API and RFC 7592 deregistration answer `204` whatever the client holds
  ([ADR-0212](docs/decisions/0212-deleting-an-owner-removes-what-it-owns.md), issue [#250](https://github.com/NCodeGroup/Identity/issues/250)).

- **Breaking: `ITenantUsageStore.RemoveBeforeAsync(day, limit, cancellationToken)` removes a bounded batch when the unit
  of work is saved,** and the flush service prunes in batches of 256 entries
  ([ADR-0213](docs/decisions/0213-the-usage-ledger-is-pruned-in-bounded-batches.md), issue [#264](https://github.com/NCodeGroup/Identity/issues/264)).

- **Breaking: `ITenantUsageStore.AddAsync` adds a numbered batch of one replica's usage, and the ledger adds each batch
  once.** `AddAsync(replicaId, batchNumber, usage, cancellationToken)` takes the identifier the replica drew when it
  started and the number of the batch, from one, in the order the replica adds its batches. It adds the counts and
  records the number as the replica's last added batch in the same unit of work, and a batch numbered at or below the
  last added one changes nothing, so a batch that a replica retries is added once. An empty `usage` and a `batchNumber`
  below one throw `ArgumentOutOfRangeException`. A store implementer skips such a batch, records the number with the
  counts, and removes the checkpoints of the replicas whose latest usage day is before the day in `RemoveBeforeAsync`,
  which still returns the count of usage entries removed. The Entity Framework store keeps the checkpoint in
  `TenantUsageCheckpointEntity`, and a deployment applies the migration `AddTenantUsageCheckpoints`, which creates the
  `TenantUsageCheckpoints` table
  ([ADR-0209](docs/decisions/0209-a-replica-adds-each-usage-batch-to-the-ledger-once.md), issue [#216](https://github.com/NCodeGroup/Identity/issues/216)).

- **The reference hosts write logs to standard output only.** `NCode.Identity.OpenId.Host` and
  `NCode.Identity.OpenId.Host.MigrationService` register the console and EventSource logging providers on every
  platform, and not the debug provider or, on Windows, the Event Log provider that the default builders add. The
  console writes to standard output in the format that `Logging:Console:FormatterName` selects, and EventSource writes
  nothing until `dotnet-monitor` or `dotnet-trace` attaches. A reference host writes nothing to the Windows Event Log,
  and a deployment that needs another sink, such as the Event Log of a Windows service, adds it in its own composition
  root ([ADR-0210](docs/decisions/0210-the-reference-hosts-log-to-standard-output.md), issue [#245](https://github.com/NCodeGroup/Identity/issues/245)).

- **Breaking: a coherence rule reads a `ClientCoherenceContext`, and the validator resolves a client's settings
  itself.** `IClientCoherenceRule.FindViolations(ClientCoherenceContext)` takes the client's registered and effective
  settings, and `IClientCoherenceValidator.Validate(tenantSettings, clientSettings)` is the one way to check a client:
  it reads the registered settings from the client's settings document and resolves the effective settings onto its
  tenant's, so every caller gives every rule the same two views. `ClientCoherenceViolation` names the setting that
  governs a value as `GoverningSettingName`, because a setting that requires a companion is not a ceiling, and carries
  a `Description` that every caller reports. A custom rule implements the new `FindViolations`, and a caller passes the
  tenant's settings and the client's settings document to `Validate`
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#253](https://github.com/NCodeGroup/Identity/issues/253)).

- **Dynamic registration keeps no relation check of its own, and its error descriptions name client settings.** The
  checks of the supported metadata, the algorithms, and the backchannel metadata are coherence rules, so
  `ValidateCoherence` is its only relation check. A description names `grant_types_supported`, not `grant_types`, and
  the error codes are unchanged: `invalid_redirect_uri` for `redirect_uris`, else `invalid_client_metadata`
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#253](https://github.com/NCodeGroup/Identity/issues/253)).

- **Every configuration seeder checks the client it creates, and a validation problem lists at most ten errors per
  setting.** The bootstrap administrator, developer, and conformance seeders run the coherence validator on a client
  they create and throw an `InvalidOperationException` that names the client and lists its violations; the bootstrap
  seeder checks before it imports the secret, and a client that exists is left as it is. A management validation
  problem lists the first ten errors of each setting, in order, and adds `Errors not listed for '<name>': <n>.` for the
  rest, so a change that makes many clients incoherent answers a bounded response
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#254](https://github.com/NCodeGroup/Identity/issues/254)).

- **Breaking: every `ISecurityProfileCatalog.Expand` overload names its scope, and a profile's default applies only
  within it.** `Expand(scopeSettings, scopeName)` and `Expand(scopeSettings, scopeName, baselineSettings)` take the
  name of the scope whose settings they expand, `OpenIdSettingScopes.Server` at the server's root, as the overload that
  takes a settings document does, and an unregistered scope name is refused with an `ArgumentException`. A profile's
  default applies only to a setting that applies within that scope, so a server or a tenant that selects
  `fapi1-advanced` supplies `tls_client_certificate_bound_access_tokens` and a client that alone selects it does not.
  Ceilings merge at every scope, because a bound only narrows. A caller passes its scope name
  ([ADR-0148](docs/decisions/0148-security-profiles-accumulate-down-the-settings-cascade.md),
  [ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md), issue [#259](https://github.com/NCodeGroup/Identity/issues/259)).

- **Breaking: `require_dpop_bound_access_tokens` is the client's requirement for DPoP-bound access tokens.** The
  setting is named `require_` plus the RFC 9449 §5.2 client metadata member `dpop_bound_access_tokens`, as the
  certificate-bound requirement is named for its RFC 8705 §3.4 member, and `OpenIdSettingNames` and `OpenIdSettingKeys`
  carry it as `RequireDPoPBoundAccessTokens`. It applies to clients, merges with `Or`, defaults to `false`, and is not
  advertised. The migration `RenameDPoPRequirement` moves a stored `require_dpop` in a server, tenant, or client
  settings document to the new key. A server configuration value is not translated, because configuration is not
  stored data: a deployment that sets `OpenId:Server:Settings:require_dpop` renames the key to
  `OpenId:Server:Settings:require_dpop_bound_access_tokens`, or the configuration cannot be honored, because the old
  name is not a registered setting
  ([ADR-0097](docs/decisions/0097-sender-constrained-access-tokens-dpop-and-mtls.md), issue [#256](https://github.com/NCodeGroup/Identity/issues/256)).

- **Breaking: an Entity Framework store reports a stale token as `StoreConcurrencyException`.**
  `IStoreManager.SaveChangesAsync` and the concurrency-token check of every store throw the store-neutral exception,
  not `DbUpdateConcurrencyException`. A caller that catches the Entity Framework type around a store method or around
  `SaveChangesAsync` catches `StoreConcurrencyException` instead, and a caller that saves through a `DbContext`
  directly still receives Entity Framework's type
  ([ADR-0208](docs/decisions/0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md), issue [#257](https://github.com/NCodeGroup/Identity/issues/257)).

- **Breaking: `tls_client_certificate_bound_access_tokens` is the server's and a tenant's advertised support only.** The
  setting keeps its RFC 8705 §3.3 discovery name, applies to tenants, merges with `Replace`, and does not force a client
  to bind its tokens. A client's `require_tls_client_certificate_bound_access_tokens` does. The `fapi1-advanced`
  profile sets that requirement as a ceiling and the support as a default (FAPI 1.0 Advanced §5.2.2-6 requires mutual
  TLS support). The migration `SeparateCertificateBindingRequirement` translates stored settings documents: a client's
  value moves to the requirement, and a server or tenant document whose support is `true` also gets the requirement,
  because that value had required every client below it to bind. A server configuration value is not translated,
  because configuration is not stored data: a deployment that sets `tls_client_certificate_bound_access_tokens` to
  `true` under `OpenId:Server:Settings` to make every client bind its tokens also sets
  `require_tls_client_certificate_bound_access_tokens` to `true` there, or those clients receive bearer tokens
  ([ADR-0097](docs/decisions/0097-sender-constrained-access-tokens-dpop-and-mtls.md),
  [ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md), issue [#233](https://github.com/NCodeGroup/Identity/issues/233)).

- **Breaking: the `offline` revocation mode is retired.** `tls_client_auth_revocation_mode` is `online` (the default) or
  `none`, because the server retrieves revocation data itself and the platform chain engine retrieves nothing. The same
  migration translates a stored `offline` to `online`. A PKI that publishes OCSP responses and no CRL sets `none` or
  replaces `IClientCertificateRevocationChecker`
  ([ADR-0173](docs/decisions/0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md), issue [#207](https://github.com/NCodeGroup/Identity/issues/207)).

- **Breaking: every initial access token has a lifetime of at most 30 days.** `lifetimeSeconds` is optional, defaults
  to one day, and a value outside 1 to 2 592 000 (30 days) is refused with `400`. `CreatedInitialAccessTokenResource`
  carries `ExpiresWhen` as a `DateTimeOffset`, not a `DateTimeOffset?`, for a scoped and an unscoped token alike. A
  token that an earlier release minted without a lifetime authorizes no registration; an administrator mints a new one
  ([ADR-0192](docs/decisions/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md), issue
  [#208](https://github.com/NCodeGroup/Identity/issues/208)).

- **Every entry point checks a client through the coherence validator.** Dynamic registration, the management API, and
  the conformance seed apply the same rules. A tenant or server settings change that narrows
  `redirect_uri_kinds_supported`, selects `fapi2` or `fapi1-advanced`, or narrows
  `token_endpoint_auth_methods_supported` is refused while a client registers a value it would exclude. Registration
  error codes are unchanged, and their descriptions are uniform
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issues [#231](https://github.com/NCodeGroup/Identity/issues/231), [#246](https://github.com/NCodeGroup/Identity/issues/246)).

- **Breaking: a client-authentication handler needs a descriptor.** The host does not compose when the handlers and the
  descriptors differ, and options validation constructs no handler
  ([ADR-0207](docs/decisions/0207-client-authentication-methods-are-named-by-registered-descriptors.md), issue [#251](https://github.com/NCodeGroup/Identity/issues/251)).

- **Breaking:** every copy of the effective-settings merge routes through `IEffectiveSettingsResolver`. The
  `DefaultOpenIdTenantFactory` and `EphemeralOpenIdTenantFactory` constructors take it instead of
  `ISecurityProfileCatalog`. Seeds and management previews read the server's committed settings rather than this
  replica's 5-minute poll of them
  ([ADR-0204](docs/decisions/0204-a-scopes-effective-settings-are-resolved-by-one-service.md), issue [#230](https://github.com/NCodeGroup/Identity/issues/230)).
  A custom `ITenantStore` or `IClientStore` must implement the new `GetSettingsPageAsync`.

- **Deleting a tenant removes its keys, its system resource servers, and its role assignments.** They are deleted with
  the tenant in one unit of work, so only a client or a resource server the tenant did not seed blocks the delete with
  `409` ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md), issue [#242](https://github.com/NCodeGroup/Identity/issues/242)).

- **Breaking:** `redirect_uri` is carried as the string the client wrote, never as a `Uri`.
  `OpenIdCommonParameters.RedirectUri` is a `KnownParameter<string>`, and `IBaseAuthorizationRequestValues`,
  `IAuthorizationRequest`, `ITokenRequest`, and `AuthorizationResponseDelivery` expose `RedirectUri` as a string.
  `UriParser` writes a parsed `Uri` parameter as its original string rather than `AbsoluteUri`
  ([ADR-0203](docs/decisions/0203-redirect-uris-are-matched-exactly.md), issue [#243](https://github.com/NCodeGroup/Identity/issues/243)).

- **Breaking:** response modes resolve through one provider, and a handler declares its traits.
  `IAuthorizationResponseModeHandler` gains `SecuredResponseMode`, the plain mode whose JWT-secured form the handler
  delivers, and `DeliversInQuery`, whether it returns the response in the query; neither has a default, and nothing is
  inferred from a handler's name, so a package that registers its own handler declares both. The internal
  `IAuthorizationResponseModeProvider` is composed once and refuses to start with a duplicate `ResponseMode`, two
  handlers that secure one mode, a `SecuredResponseMode` that names no registered handler, or a handler named `jwt`.
  `jwt`, the one shorthand JARM defines, resolves to the handler that secures the response type's default mode, and a
  handler that `DeliversInQuery` is refused for a response type that returns a token
  ([ADR-0201](docs/decisions/0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issue
  [#228](https://github.com/NCodeGroup/Identity/issues/228)).

- **Breaking:** the authorization response is addressed through one public `AuthorizationResponseDelivery`, which
  replaces `ClientRedirectContext`. The new internal `IAuthorizationResponseDeliveryResolver` derives it once per
  request, from the redirect URI, the response mode handler, the `state`, and the credentials that secure a JWT
  response, and the authorization endpoint, the PAR endpoint, the continuation, validation, the security profiles, and
  the result share it rather than derive it again. `IAuthorizationRequestProcessor.ProcessRequestAsync` takes the
  delivery, `ValidateAuthorizationRequestCommand` and `AuthorizationRequestProfileContext` carry it as `Delivery`, and
  `AuthorizationResult` requires its `OpenIdContext`, `OpenIdClient`, and delivery, drops `RedirectUri` and
  `ResponseMode`, and is delivered by `result.Delivery.ResponseModeHandler`. `IAuthorizationRequest.ResponseMode` is
  now the value the request asked for, possibly `null`, and `ResponseTypeValues.ResolveResponseMode` is removed. The
  outer parameters of a request that carries a request object follow one table, `RequestObjectParameterPolicy.Rules`:
  the object must carry `response_type`, `scope`, `redirect_uri`, and `client_id`, an outer `response_type`,
  `redirect_uri`, or `client_id` must equal the object's, with `redirect_uri` compared as written, and an outer `scope`
  is ignored. The PAR endpoint now derives the delivery when a request is pushed (RFC 9126 §2.1), so it refuses a
  `redirect_uri` the client has not registered at push time rather than at the authorization endpoint
  ([ADR-0201](docs/decisions/0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md),
  [ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md), issues [#229](https://github.com/NCodeGroup/Identity/issues/229), [#238](https://github.com/NCodeGroup/Identity/issues/238)).

- **A JWT-secured authorization response lives for the client's `authorization_response_lifetime`.** The new
  client-scoped setting is a `TimeSpan` that defaults to 5 minutes, accepts 1 second to 10 minutes (JARM §2.1), merges
  with `Min` so a tenant can only lower it. It replaces the fixed 5 minutes the response JWT's `exp` used
  ([ADR-0201](docs/decisions/0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issue
  [#238](https://github.com/NCodeGroup/Identity/issues/238)).

- **CI runs once per commit of a pull request.** The workflows ran for both the push to a branch and its pull request,
  so each check appeared twice. A push now runs CI only on `main`; any other branch is checked through its pull request
  or by running the workflow by hand.

- **Breaking:** a request object's parameters are the only parameters of an authorization request (RFC 9101 §6.3). A
  parameter sent only outside the request object, such as `response_mode` or `nonce`, is now ignored instead of being
  merged in, so an unsigned parameter can no longer change a signed request. The outer `response_type` and `scope`
  are still used when the object omits them. A request object must now carry `redirect_uri` and a `client_id` that
  names the requesting client. An outer `client_id`, `response_type`, or `redirect_uri` is optional, but one that is
  sent must equal the object's value, or the request is refused as `invalid_request_object`. A pushed authorization
  request may therefore carry only `request` and client authentication (RFC 9126 §3): the authorization endpoint
  verifies the object before it redirects, takes `redirect_uri`, `response_mode`, and `state` from it, and returns an
  error raised before then without redirecting.
  See [ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md).

- **Breaking:** the event metric `ncode.identity.events.published` is recorded through a meter created from
  `IMeterFactory`, whose name moves from `DefaultEventMetricsHandler.MeterName` to `EventMetrics.MeterName`
  (`NCode.Identity.Events`, unchanged), and its attributes are renamed from `event.type`, `audit.action`, and
  `audit.outcome` to `ncode.identity.event.type`, `ncode.identity.audit.action`, and `ncode.identity.audit.outcome`.
  `DefaultEventMetricsHandler` is no longer `IDisposable`.
- **Breaking:** `IAuditEventRecorder.RecordClientAuthenticationFailedAsync` takes the attempted `authenticationMethod`,
  and `ClientAuthenticationFailedAuditEvent` requires `AuthenticationMethod`.
- **A tenant is provisioned only with the keys its settings use.** The tenant key seed merges the tenant's persisted
  settings onto the server's and provisions a configured algorithm only when one of the tenant's effective algorithm
  lists names it — by default the ID token, UserInfo, introspection, and access-token signing lists and the
  request-object and access-token encryption lists. `OpenIdTenantKeyProvisioningOptions` gains `AlgorithmSettings`
  (`OpenId:Tenant:KeyProvisioning:AlgorithmSettings`), which names those lists; a package that signs or decrypts with
  the tenant's keys under another setting adds its name. A `fapi2` tenant no longer holds and publishes an RS256 key it
  never signs with. A key the tenant already holds is kept
  ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md)).
- **The server-rendered pages stream into the response without building a document string.** The login, consent, and
  error pages and the `form_post` authorization response render into a pooled `Sequence<char>` that is transcoded
  straight into `HttpResponse.BodyWriter`. The bundle's `index.html` is split once per version at its
  `<div id="root"></div>` mount point, so each page writes the cached head, its form, and the cached tail rather than
  replacing the mount point in a combined document. The renderer's page methods take an `IBufferWriter<char>` and the
  split `InteractiveBundleShell` instead of returning a string.
- **The interactive pages read the bundle's `index.html` once.** `InteractiveBundleReader` caches the bundle index the
  login, consent, and error pages inject their forms into, rather than reading it from disk on every request. Each
  request compares the file's last-write time and length with the cached copy's, so a rebuilt bundle is still picked up
  without a restart, including on bind-mounted and network file systems where change notifications are unreliable.
- **A security profile supplies compliant defaults.** `ISecurityProfile` gains `ApplyDefaultSettings`, through which a
  profile contributes overridable defaults with the new `ISecurityProfileDefaultsBuilder`; the builder accepts only a
  setting that does not merge with a lattice operation. The catalog places a profile's defaults beneath the settings of
  the scope that selects it, so that scope and every scope below it may still set another value. When several selected
  profiles default one setting, the first in ordinal order supplies it. FAPI 2.0 now defaults
  `id_token_signed_response_alg` and `introspection_signed_response_alg` to `PS256`. A client under the profile that
  registers no algorithm is therefore signed with an algorithm the profile permits, rather than with `RS256`, which the
  profile excludes, and no longer fails at issuance
  ([ADR-0148](docs/decisions/0148-security-profiles-accumulate-down-the-settings-cascade.md)).
- **The management API rejects a client algorithm its effective settings do not permit.** Client create and settings
  update merge the client's document onto its tenant's effective settings, as the runtime does. They then answer `400`
  with a validation problem naming the setting when a registered ID token, UserInfo, or introspection signing or
  encryption algorithm is outside its effective `*_values_supported` list. The default `A128CBC-HS256` of a registered
  encryption without an `enc` is checked the same way. A tenant settings update checks every client of the tenant, and
  a server settings update every client of every tenant; each answers `400`, naming the list and the client, when the
  change excludes an algorithm a client registers, and a mismatch the change does not introduce is not reported. A
  mismatch is reported when the settings are written instead of failing every issuance with `server_error`
  ([ADR-0155](docs/decisions/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)). **Breaking:**
  `OpenIdServer` gains the abstract `ConfiguredSettingsProvider` — the settings the server derives from its defaults
  and the host's configuration, onto which its persisted settings document is merged.
- **Hash algorithms and binary encodings are open vocabularies.** The `HashAlgorithmType` and `BinaryEncodingType`
  enums are removed. `ICryptoService.HashValue` takes the framework's `HashAlgorithmName` and computes the digest with
  `CryptographicOperations`, so SHA-384 and SHA-3 are available. Encodings are named: `BinaryEncodings` holds
  `base64url`, `base64`, and `hex`, each the name of a registered `IBinaryEncoder`, and `EncodeBinary`, `GenerateKey`,
  and `HashValue` take the name as a `string`. `AddIdentityLibrary` registers the three built-in encoders; a package adds
  one, such as Base32, by registering another. An unregistered or duplicate encoding name is rejected. Hashed and
  encoded output is unchanged
  ([ADR-0190](docs/decisions/0190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md),
  [ADR-0150](docs/decisions/0150-vocabularies-are-open-unless-provably-closed.md)).

- **A secret import format is an open name that each key type declares.** The `SecretKeyImportFormat` enum is replaced
  by `SecretKeyImportFormats`, a static class of `const string` names: `jwk`, `pem`, and `certificate`.
  `SecretKeyImportParameters.Format` and `SecretKeyImportRequest.Format` are a `string`, and
  `ISecretKeyTypeDescriptor.SupportsPublicKeyImport` is replaced by `PublicKeyImportFormats`, the names the type
  imports, so a registered key type can bring a format such as PKCS #12. The management `POST …/secrets/import`
  endpoints, the secret importer, and client-key import reject a format the secret type does not list, and the `400`
  message lists the type's formats. The `format` member now compares ordinally, so `JWK` is rejected; OKP and AKP keys
  accept `jwk` and `pem`. The capabilities `supportsPublicKeyImport` field is unchanged
  ([ADR-0104](docs/decisions/0104-import-public-asymmetric-secret-material.md),
  [ADR-0150](docs/decisions/0150-vocabularies-are-open-unless-provably-closed.md)).

- **A session's termination reason is an open vocabulary.** The `SessionTerminationReason` enum is replaced by
  `SessionTerminationReasons`, a static class of `const string` names: `logout`, `idle_timeout`, `absolute_expiry`,
  `concurrent_session_limit`, `administrative_revocation`, `global_sign_out`, and `user_initiated`. `ISessionService`,
  `IAuditEventRecorder.RecordSessionTerminatedAsync`, `SessionTermination.Reason`, and
  `SessionTerminatedAuditEvent.TerminationReason` take and carry a `string`, so an extension can end a session for its
  own reason, such as a password change. The `session.terminated` audit event's `reason` is now the name, for example
  `global_sign_out` instead of `GlobalSignOut`
  ([ADR-0098](docs/decisions/0098-session-lifecycle-termination-seam.md),
  [ADR-0150](docs/decisions/0150-vocabularies-are-open-unless-provably-closed.md)).

- **The whole-second time helpers move to the new dependency-free `NCode.Time` package.** `DateTimeExtensions`,
  `DateTimeOffsetExtensions`, and `TimeProviderExtensions` (`WithPrecisionInSeconds`, `GetUtcNowWithPrecisionInSeconds`,
  `GetTimestampWithPrecisionInSeconds`) leave `NCode.Identity.Jose.Abstractions` and the `NCode.Identity.Jose.Extensions`
  namespace for namespace `NCode.Time`, so the abstractions package holds no logic. Replace
  `using NCode.Identity.Jose.Extensions;` with `using NCode.Time;` and reference `NCode.Time`. Issue [#31](https://github.com/NCodeGroup/Identity/issues/31) is
  complete ([ADR-0188](docs/decisions/0188-general-time-helpers-live-in-a-dependency-free-package.md)).

- **Tenant, server, and resource-server keys are imported and updated under the client-key policy.** Their import
  endpoints use `IClientKeyImporter.TryImportKey`, so a JSON Web Key keeps its `kid`, a key whose `use`, `key_ops`, or
  `alg` contradicts it is refused with a `400` that says why, and a `kid` already used by another of the owner's keys is
  a `409 Conflict`. Their update endpoints check `use` and `alg` against the key through the new
  `ISecretValidator.ValidateUpdate`, as the client endpoint does, and keep the key's `kid`. `IClientKeyImporter` and its
  error messages no longer describe the policy as client-only. See
  [ADR-0186](docs/decisions/0186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md).
- **Coverage measures hand-written code, and the job summary and Codecov agree.** The full Definition of Done collects
  .NET coverage with `build/coverage.runsettings`, which leaves out source-generator output under `obj/`, the
  scaffolded EF Core migrations, and `[GeneratedCode]` members. The reference host stays in scope because it ships.
  `codecov.yml` counts a partially covered line as a hit, as ReportGenerator does, so the job summary and Codecov report
  the same figure from the same lines. See
  [ADR-0185](docs/decisions/0185-coverage-measures-hand-written-code-and-counts-a-line-that-runs-as-covered.md).
- **The reference host separates liveness from readiness.** `/health` becomes two endpoints. `/health/live` runs no
  checks and reports only that the process answers, so a dependency outage never makes Kubernetes restart a replica.
  `/health/ready` runs every check tagged `ready` and returns `503` while any of them fails, which takes the replica out
  of rotation until its dependencies return. Those checks are the database (`AddDbContextCheck<OpenIdDbContext>`,
  registered with the `openiddb` connection string) and Valkey when configured. The Aspire orchestrator gates
  dependents on `/health/ready`. **Breaking:** `/health` is no longer mapped.

- **Every frontend bundle splits into vendor and surface chunks, and an oversized chunk fails the build.** The shared
  Vite preset splits React, MUI with emotion, Redux, and every other package into their own vendor chunks, and each
  app lazy-loads its route-level containers, so no chunk exceeds 500 kB after minification. The preset's
  `failOnOversizedChunks` plugin fails `pnpm build` when a chunk exceeds the build's `chunkSizeWarningLimit` instead of
  only warning. ([ADR-0175](docs/decisions/0175-bundles-split-into-vendor-and-surface-chunks-and-an-oversized-chunk-fails-the-build.md))

- **A forwarded client certificate is decoded by a registered format, and every format reads the whole chain the
  client presented.** Traefik's `passTLSClientCert` (`pem: true`) forwards the chain as comma-separated base64 DER in
  `X-Forwarded-Tls-Client-Cert`, which failed to parse. `Base64Der` now reads a comma-separated list, the client's own
  certificate first; `Pem` and `UrlEncodedPem` read several PEM blocks; and `EnvoyXfcc` reads `Chain` as well as
  `Cert`, honoring quoted values. The resolver returns a `PresentedClientCertificate`: the client's certificate and the
  other certificates it sent, as untrusted `Intermediates` for chain building. Formats are an open vocabulary: each is
  an `IClientCertificateHeaderDecoder` registered under a `Format` name (the known names are
  `ClientCertificateHeaderFormats`), so a package supports another proxy by registering a decoder, and the host refuses
  to start when `OpenId:MutualTls:Format` names no registered decoder. The certificate is resolved once per request.
  **Breaking:** `MutualTlsClientCertificateFormat` is removed and `MutualTlsForwardingOptions.Format` is a string with
  the same names; `IClientCertificateResolver` and `MutualTlsForwardingOptions` move to
  `NCode.Identity.OpenId.Authentication.ClientCertificates`, registered by `AddClientCertificateServices()`; and
  `ResolveCertificateAsync` takes an `OpenIdContext` and returns a `PresentedClientCertificate`. See
  [ADR-0172](docs/decisions/0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md).

- **Introspection reports a token only to the party that consumes it.** An access token is reported active only to a
  client acting for a resource server in the token's audience, with `aud` reduced to that resource server and `scope`
  to its scopes. A refresh token is reported active only to the client it was issued to. Everything else is
  `{"active": false}`. **Breaking:** an integration that introspected access tokens as an ordinary client now receives
  `active: false` and must create a client bound to its resource server. `IntrospectTokenCommand` gains the caller's
  `ResolvedResourceServer`. `OpenIdClient` gains `ResourceServerId`, and `IOpenIdClientFactory.CreatePublicClientAsync`
  takes an `OpenIdClientDefinition`. `PersistedClient` gains `ResourceServerId`, `IClientStore` gains
  `GetPageByResourceServerAsync`, and the client table gains a `ResourceServerId` foreign key (migration
  `AddClientResourceServer`). See
  [ADR-0168](docs/decisions/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md).

- **The server validates every token it issued through one seam, and a system resource server holds no keys.** UserInfo,
  the management API, introspection, token exchange, and the end-session `id_token_hint` validate through
  `IIssuedTokenValidator`. Access tokens are checked against the tenant's access-token algorithm lists and keys, with
  `typ` `at+jwt`, the issuer, the lifetime, and session revocation. ID tokens are checked against
  `id_token_signing_alg_values_supported` with `typ` `JWT`. Token exchange picks the policy by `subject_token_type`, and
  accepts a `jwt` subject token only when it is an access token or an ID token. The management API refuses to create or
  import a secret for a system resource server (`409 Conflict`), and issuance ignores a key already stored on one; the
  console no longer offers their Secrets tab. **Breaking:** `ResolvedResourceServer` requires `IsSystem` and `Scopes`,
  `IResourceServerCredentialResolver` gains `ResolveByIdAsync`, and the `AccessTokenValidationError` log event is
  retired. See [ADR-0167](docs/decisions/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md).

- **The development key modes build on the keys every tenant is provisioned with.**
  - **`PersistentSigningKey`** keeps the provisioned keys and only removes a key the development key ring can no
    longer unprotect, so provisioning replaces it.
  - **`Ephemeral`** turns provisioning off, because its in-memory keys replace the persisted ones.
  - **`None`** behaves exactly as production.

  **Breaking:** `AddDeveloperSigningKey()` is renamed `AddPersistentDeveloperKeys()`, and `OpenIdTenantOptions` gains
  `KeyProvisioning`. A development database keeps its RS256 key and gains `PS256` and `ES256` keys. See
  [ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md).

- **Every tenant a strategy serves has an issuer, and nothing derives one from the request.** A tenant without an issuer
  used to publish URLs built from the incoming request — a development convenience that let a caller's `Host` header
  choose where clients send their credentials. The tenant factory now takes a tenant's issuer and base address from its
  stored issuer alone and fails closed for a served tenant without one. **Breaking:**
  `OpenId:TenantResolution:StaticSingle:Issuer` is required, so a host without it does not start (the reference host's
  launch profiles pin `https://localhost:5001`, `https://localhost:44369`, `https://host.docker.internal:5001`,
  `https://localhost:5003`, and the Docker profile's now-fixed `https://localhost:5011`), and the host and path strategies
  require an address for the root tenant — `Root:Issuer` or their issuer template. A host is reached at the origin its
  issuer names. A development database that holds a tenant stored without an issuer must be reset
  (`build/dev-db-reset.ps1`) or the tenant given an issuer. An issuer's host labels may be at most 63 characters. A
  disabled static tenant is no longer served, like any other disabled tenant. `DefaultOpenIdTenantFactory` no longer
  takes a `TemplateBinderFactory`, and resolves `GetTenantIssuerAsync` before `GetTenantBaseAddressAsync`, which now
  receives the issuer. See
  [ADR-0165](docs/decisions/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md).

- **Tenant provisioning is split into creating and seeding, and the root tenant's options are grouped.**
  **Breaking:** `ITenantProvisioner.ProvisionAsync(tenantId, displayName, issuer)` is replaced by
  `EnsureCreatedAsync(tenantId, displayName)` — which creates a configuration-defined tenant at its strategy's default
  issuer, judged like any other write — and `SeedAsync(tenantId)`. `ITenantStrategy` and `ITenantResolver` gain
  `GetDefaultIssuerAsync`, which a custom strategy implements. `TenantResolution:RootTenantId` moves to
  `TenantResolution:Root:TenantId`, beside the new `Root:DisplayName` and `Root:Issuer`, and
  `TenantSeedContext.IsNewlyProvisioned` is removed. The Aspire conformance instance serves its own `conformance` tenant
  rather than sharing the development host's. See
  [ADR-0166](docs/decisions/0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md).

- **DPoP nonces and the DPoP-proof and client-assertion replay records are stored in the shared-state port, scoped by
  tenant.** They are recorded in `IExpiringKeySet` under keys that name the tenant and digest the request-supplied
  values, so tenants sharing a store never collide and a crafted `jti` cannot make a key ambiguous or unbounded. If
  the store is unreachable, these checks fail closed. **Breaking:** `IDPoPProofReplayCache.TryRecordAsync` and
  `IClientAssertionReplayCache.TryRecordAsync` now take the request's `OpenIdContext` as their first parameter. The
  server no longer registers an in-memory `IDistributedCache`, so a host that resolved one must register its own.
  Records in flight across the upgrade are lost; they expire within minutes. See
  [ADR-0164](docs/decisions/0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md).

- **A tenant is named by whoever creates it.** `POST /api/tenants` takes the tenant's `tenantId` — 1 to 63 lowercase
  letters, digits, or hyphens that starts and ends with a letter or digit — instead of minting an opaque one, and the
  identifier never changes. It reads the same in the management API (`/api/tenants/acme`), in a path tenant's issuer
  (`https://op.example/acme`), and in a host tenant's hostname (`acme.auth.example`), so a path tenant can be created
  with its issuer in one request; `displayName` remains the editable label. The configured
  `OpenId:TenantResolution:RootTenantId` and `StaticSingle:TenantId` must have the same form or the host does not
  start. The management console asks for the tenant ID when it creates a tenant and locks it when it edits one.
  **Breaking:** `CreateTenantRequest.TenantId` is required. A tenant created earlier with an opaque identifier remains
  addressable, but changing its issuer fails validation, so recreate it.
  See [ADR-0159](docs/decisions/0159-a-tenant-is-named-by-its-creator.md).

- **Each tenant strategy validates its own options, by the same rules that judge the tenants it serves.** The shared
  tenant-resolution validator no longer branches on the built-in strategy codes: each built-in strategy registers its
  own options validator beside it, and a custom strategy does the same for its own section. The rules are tighter: a
  configured `StaticSingle:Issuer` must use `https` (`http` only for a loopback host) and carry no user information,
  and a `DynamicByPath:TenantPath` route may carry only the tenant parameter, with no constraint, default value,
  optional marker, or catch-all, so a tenant's route follows from its identifier alone. **Breaking:** `ITenantResolver`
  and `ITenantStrategy` gain `ValidateTenantAsync`, which returns `TenantValidationFailure`s; a custom strategy
  implements it. See [ADR-0160](docs/decisions/0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md).

- **A client's public keys have one source and are imported under one policy (breaking).** A client's keys are the
  keys stored for it — registered by value at dynamic client registration or imported through the management API — or,
  when it has a `jwks_uri`, the keys it publishes there; never both. The `jwks_uri` keys replace any stored public key at
  runtime, every consumer (client assertions, request objects, CIBA requests, encryption to the client, and
  `self_signed_tls_client_auth`) resolves the same set, and the management API refuses with `409` a settings write that
  adds a `jwks_uri` while public keys are stored, or a public key added while a `jwks_uri` is set. A registration update
  (RFC 7592) replaces the client's public keys with its `jwks`, or removes them when it switches to a `jwks_uri`.

  The `jwks` setting is retired: registration imports the keys of a `jwks` as the client's secrets and every
  registration response projects them back from the stored keys. `OpenIdSettingNames.Jwks`, `OpenIdSettingKeys.Jwks`,
  and the setting's descriptor and rule are removed, and the `RetireClientJwksSetting` migration strips the setting from
  stored documents.

  One public seam, `IClientKeyImporter`, imports a client key at every entry point: dynamic registration (all or
  nothing), the `jwks_uri` fetch (ignoring keys it cannot use, per RFC 7517 §5), and the management import. Every key of
  a JSON Web Key Set needs a `kid` unique within the set; a key keeps the `kid` it was registered with, stored in the new
  `KeyId` of `PersistedSecret`, `SecretKeyImportRequest`, and `SecretEntity` (the `AddSecretKeyId` migration adds the
  nullable column; a key without one is selected by its secret identifier, as before). Key types are matched by their
  JSON Web Key `kty` through the new `ISecretKeyTypeDescriptor.JwkKeyType` and
  `ISecretKeyTypeCatalog.TryGetByJwkKeyType` (which replaces the protected
  `AsymmetricSecretKeyTypeDescriptor.ExpectedJwkKeyType`), so a client may register `OKP` (EdDSA) and `AKP` (ML-DSA)
  keys, not only RSA and EC. The key-to-algorithm rule is the new `IAlgorithmCollection.ValidateKeyAlgorithm`
  (`KeyAlgorithmDeclaration`). The resolver's per-key `ClientJwksKeyImportFailed` log (event 2034) is replaced by
  `ClientJwksKeyIgnored` (event 2038).
  ([ADR-0161](docs/decisions/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md))

- **The frontend runs on React 19 and Material UI 9.** This covers every surface, including the login and consent
  pages the OpenID Provider host serves. Material UI 9 removed the system-prop shorthands (`<Box display="flex">`)
  and the deprecated `inputProps`-style props, so components style through `sx` and pass input attributes through
  `slotProps`. It also gives `Switch` the ARIA `switch` role, so assistive technology announces the "Disabled"
  toggles in the management forms as on/off switches rather than checkboxes.

- **The frontend toolchain is current, and its dev-only security advisories are gone.** The grouped Dependabot update
  moves Vite to 8, Vitest to 5, Storybook to 10, ESLint to 10, jsdom to 30, and their companions, and `pnpm audit`
  is now clean. TypeScript moves to 6.0, the newest version typescript-eslint supports; TypeScript 7 waits for it
  (issue [#147](https://github.com/NCodeGroup/Identity/issues/147)).

  Lint now uses a flat `src/frontend/eslint.config.js`, because ESLint 9 and later no longer read `.eslintrc`.
  `@eslint-react/eslint-plugin` replaces `eslint-plugin-react`, which does not support ESLint 10, and
  `eslint-plugin-react-hooks` 7 adds the React Compiler checks. The stricter rules found two presentational tables
  that read the clock during render: `GrantTable` and `SecretTable` now take a `now` prop from their container.
  `@types/node` stays on the Node 22 runtime major, pinned workspace-wide by a pnpm override, and `engines.node` now
  states the toolchain's real floor (`>=22.13.0`). Dependabot ignores TypeScript and `@types/node` majors, so the
  weekly grouped update no longer brings back the two upgrades that cannot land yet. See
  [ADR-0147](docs/decisions/0147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md).

- **The frontend runs on Node 26, and its Node and pnpm versions are each set in one place.** Node 22 is in
  Maintenance LTS; Node 26 enters Active LTS on 2026-10-28 and is supported until 2029-04-30.
  `src/frontend/.node-version` is now the only place the Node major is set. CI reads it (`node-version-file`), and so
  do the Aspire AppHost and `build/frontend-install.ps1` when they pick the NVS install. Previously both could pick an
  older install: the AppHost compared versions as text (`22.9.0` sorted above `22.22.2`), and the script took the first
  directory. `engines.node` is now `>=26.0.0`, and `@types/node` moves to 26.

  Node no longer ships Corepack, which used to provide pnpm. `build/frontend-install.ps1` now installs the exact pnpm
  the `packageManager` field pins, beside the pinned Node, after adding that Node through NVS if it is missing. CI uses
  `pnpm/action-setup`. pnpm moves from 9.12 to 10.34, the newest major Dependabot supports: Dependabot cannot read the
  lockfile pnpm 12 writes, so moving further would silently stop security updates (issue [#153](https://github.com/NCodeGroup/Identity/issues/153)). Every pnpm setting
  now lives in `pnpm-workspace.yaml`. A dependency's install script fails `pnpm install` until it is allowed or denied
  there; esbuild's is denied because esbuild does not need it.

  **Action:** run `./build/frontend-install.ps1` once. It adds Node 26 through NVS, installs pnpm 10, and lets pnpm
  replace the `node_modules` that pnpm 9 built. See
  [ADR-0152](docs/decisions/0152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md).

- **Frontend dependencies must be three days old before anyone installs them.** pnpm now refuses a version published
  less than three days ago (`minimumReleaseAge`), so a compromised release is usually caught and pulled before it
  reaches the lockfile; `pnpm add` picks the newest version old enough. The window equals Dependabot's npm cooldown,
  now stated in `dependabot.yml` rather than left to its default. That equality matters: on pnpm 10, Dependabot
  enforces its cooldown on every version already in the lockfile, so a version a person locked inside the window made
  its npm updates fail until the version aged. Dependabot security updates bypass both gates; any other urgent version
  goes in `minimumReleaseAgeExclude`. See
  [ADR-0153](docs/decisions/0153-one-release-age-window-gates-frontend-dependencies.md).

- **Dependabot version updates are paused; security updates continue.** While the family is refactored heavily ahead of
  its first release, weekly version bumps are mostly churn against code that is about to move. Every ecosystem in
  `.github/dependabot.yml` therefore sets `open-pull-requests-limit: 0`, GitHub's documented way to pause version
  updates while keeping their configuration. Issue [#36](https://github.com/NCodeGroup/Identity/issues/36) tracks resuming them, no later than the first published
  release. Dependabot security updates are now on for the repository, with one group per ecosystem, so an advisory that
  affects a dependency still produces a fix pull request automatically.

- **Frontend tests fail on warnings, and frontend coverage can no longer fall unnoticed.** A test that logs
  `console.error` or `console.warn` now fails. The usual culprit is a React "not wrapped in act(...)" warning, which
  means the test asserted before an update settled. The check comes from a console guard in the new test-only
  `@ncode/test-support` workspace. A test that makes no assertion also fails, lint fails on any warning, and tests,
  stories, and setup files are now type-checked (seven workspaces had excluded their tests). Each workspace's
  coverage now measures every source file and holds a floor at its current level, enforced by
  `pnpm run test:report` in CI. New and extended tests raise frontend line coverage from 81% to 99% and branch
  coverage from 74% to 98%; eight of the twelve workspaces are fully covered. The conformance runner's CLI logic moved
  from its entry point into a tested `cli.ts`, and its `--help` now documents exit code `2`. See
  [ADR-0151](docs/decisions/0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md).

- **Every standalone frontend surface boots through one shared gate.** The Management console, the Playground, and the
  conformance dashboard each carried their own copy of the gate that loads the runtime configuration before the app
  renders. They now share `BootstrapGate` from the new `@ncode/shell` workspace, the one package allowed to depend on
  both the store and the design system. Its loading and error views are a stateless `BootstrapScreen` in `@ncode/ui`.
  Each surface names itself once, in `main.tsx`, through `createRuntimeConfigLoader({ surface })`, and
  `loadRuntimeConfig` now defaults the serving origin to the page's own. One test suite replaces three, and it covers
  the cases they missed:
  - A loader that ignores its abort signal and settles late can no longer overwrite newer configuration.
  - Configuration that reaches the store another way replaces an earlier error.
  - An error without a message shows the generic one instead of spinning forever.

  `SurfaceKind` is now an open vocabulary, so a new surface needs only its host override, not a change to
  `@ncode/config`. See [ADR-0156](docs/decisions/0156-shared-app-shell-containers-live-in-ncode-shell.md).

- **Every frontend workspace builds and tests from one Vite preset.** The new `@ncode/vite-config` workspace owns the
  test gates and the two serving models, and each workspace's Vite or Vitest config is now one call that states its
  coverage floors. Two things were drifting:
  - Workspaces on the `node` test environment never collected `.test.tsx` files.
  - The Management console's dev server did not pin port 5173, which its redirect URI and CORS origin assume. Outside
    Aspire, Vite would move it to the next free port, the Playground's.

  Both are fixed for every workspace. Locally, Turborepo now re-runs a workspace's tests and type-check when an internal
  package it depends on, `tsconfig.base.json`, or `eslint.config.js` changes. Before, it replayed their cached results.
  See [ADR-0157](docs/decisions/0157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md).

- **The reference host is no longer called "Playground".** `NCode.Identity.OpenId.Playground` is now
  `NCode.Identity.OpenId.Host`, and its two companions are `NCode.Identity.OpenId.Host.Database` and
  `NCode.Identity.OpenId.Host.MigrationService`. The Aspire orchestrator, previously
  `NCode.Identity.OpenId.Playground.AppHost`, is now `NCode.Identity.OpenId.LocalDev`.

  "Playground" said "toy, for experimenting" about the process that is going to be deployed and face the internet —
  a name that invites the "it's only the playground" reasoning that leaves ephemeral signing keys, wildcard
  `AllowedHosts`, and dev seeding switched on in something publicly reachable. Separately, `Server` / `Host` /
  `AppHost` read as synonyms while being a shipped package, a deployable process, and a developer-only tool; Aspire
  is local-development only here, so the dev-only project should never have carried the most authoritative-sounding
  name. `LocalDev` names the purpose rather than the technology, and is safe because Aspire discovers the
  orchestrator through `<IsAspireHost>` and its SDK, not the project name.

  `NCode.Identity.Server` is unchanged — it is the one that ships. The frontend `apps/playground` flow explorer and
  its `playground-spa` client also keep their names: that one genuinely is a playground.
  ([ADR-0130](docs/decisions/0130-distinct-names-for-host-local-dev-orchestrator-and-library.md))

  **Breaking (source/tooling, not packages — every renamed project is `IsPackable=false`):** project paths,
  namespaces and Aspire `Projects.*` references moved; `PlaygroundApiMarker`, `PlaygroundDatabase` and
  `PlaygroundApplicationFactory` became `HostApiMarker`, `HostDatabase` and `HostApplicationFactory`.

- **Configuration is bound strictly and checked before the host accepts traffic.** A key that maps to no option now
  stops the host with an error naming the section, the options type, and the key, instead of being silently ignored.
  This covers the whole OpenID section and the `BootstrapAdmin`, `Frontend`, `ReverseProxy`, `ConformanceSeed`, and
  `DeveloperInteractiveSeed` sections. Every bound options type is validated on start, and a tenant-resolution
  configuration that cannot work — an unknown strategy, a path route without its tenant parameter, an invalid host
  pattern, a static issuer that does not end with the tenant path — also stops the host instead of failing on the first
  request after the instance reported ready.

  `AddConfiguration` owns the OpenID section's shape: `Server` (including `Settings`), `Tenant` (tenant
  materialization and caching), `TenantResolution` (tenant selection — the canonical location), and `MutualTls`. Any
  other subsection is rejected. The mutual-TLS forwarding options are bound there too, so a host's chosen section name
  applies to them. ([ADR-0137](docs/decisions/0137-configuration-binds-strictly-and-is-validated-at-startup.md))

  **Breaking:** configuration that used to be ignored now fails startup. `OpenIdOptions.SectionName`,
  `OpenIdServerOptions.SettingsSubsection`, and `MutualTlsForwardingOptions.ConfigurationSectionKey` are removed;
  `OpenIdServerOptions.Settings` carries the server's settings section. The authentication library no longer binds
  `OpenId:MutualTls` itself — `AddConfiguration` does.

- **Setting names are a closed vocabulary.** Every setting a server, tenant, client, or resource server carries must
  have a registered descriptor, and its value must convert to the descriptor's type; nothing invents a descriptor for an
  unknown name any more. The rule is enforced wherever a setting enters:
  - `OpenId:Server:Settings` is validated when the host starts. An unregistered name, a value that does not convert, a
    list setting written as a single value, or a single-value setting written as a list stops the host with an error
    naming the configuration path; an empty array is an empty list. A configuration reload that cannot be honored is
    rejected with an error log and the previous settings stay in force.
  - The management API validates the settings of a created server, tenant, client, or resource server and every
    settings `PATCH`, returning `400` with a validation problem keyed by setting name and storing nothing. A non-object
    `settings` payload is rejected rather than stored as `{}`.
  - A stored settings document that names an unregistered setting fails when it is loaded instead of being loaded
    without it, so a control an operator configured is never silently dropped.

  Dynamic client registration stores the RFC 7591 metadata (`client_name`, `client_uri`, `logo_uri`, `tos_uri`,
  `policy_uri`, `contacts`, `jwks`, `software_id`, `software_version`) as registered client settings, and no longer
  stores the requested `scope`. ([ADR-0139](docs/decisions/0139-setting-names-are-a-closed-vocabulary.md))

  **Breaking:** `ISettingDescriptorJsonProvider` is removed; `ISettingSerializer` gains `ValidateSettings`, which returns
  `SettingError` values; `ISettingCollection.Set(key, value)` throws for a key with no descriptor of that value type.
  `OpenIdSettingNames.ScopesSupported` is removed (discovery still publishes `scopes_supported`), and
  `ConformanceClientSeed.Scopes` is removed — the OpenID Connect scopes are available to every client. An extension must
  register a descriptor for every setting it reads or writes, and retiring a setting needs a migration of the stored
  settings documents.

- **The remaining lenient configuration inputs are bound and validated.** `ReverseProxy` rejects a trusted proxy that is
  not an IP address (IPv4 written as four decimal octets — the parser otherwise reads `10.0.0` as another address), a
  trusted network that is not CIDR notation or has bits set past its prefix (the parser otherwise masks
  `10.0.0.4/8` to `10.0.0.0/8`), and a forward limit below one. `InteractiveAuthentication`, `DeveloperKeys`, and `Cors`
  are bound strictly into `InteractiveAuthenticationOptions`, `DeveloperKeysOptions`, and `DeveloperCorsOptions`; an
  unknown `DeveloperKeys:Mode` throws instead of falling back to `Ephemeral`, and an allowed origin that can never match
  a browser's `Origin` header (a trailing slash, a path, a wildcard) stops the host. In Production, any `Cors` section is
  a production-safety violation, because the developer CORS policy is never registered there.
  ([ADR-0139](docs/decisions/0139-setting-names-are-a-closed-vocabulary.md))

  **Breaking:** `DeveloperKeyModeConfiguration.GetDeveloperKeyMode()` is replaced by
  `DeveloperKeysConfiguration.GetDeveloperKeysOptions()`. `ReverseProxyOptions` and `InteractiveAuthenticationOptions`
  are resolved through `IOptions<T>` rather than registered as singleton instances. Strict binding is public as
  `NCode.Registration.StrictConfigurationBindingExtensions.BindStrictly`, so an extension can bind its own section the
  same way.

- **`IEndpointGroup.Prefix` is replaced by `GetPrefix(IServiceProvider)`**, resolved when routes are mapped, so a
  group's prefix can come from configuration. **Breaking** for custom endpoint groups, which return a `RoutePattern`.
  ([ADR-0138](docs/decisions/0138-protocol-endpoints-are-served-under-the-tenant-route.md))

- **Setting values are validated against their domain, not only their type.** A value of the right type that the
  server would not honor is now rejected wherever a setting enters, exactly as an unregistered name is: configuration
  stops the host (and a reload is rejected), a management write returns `400` keyed by setting name, a stored document
  fails the request that loads it, dynamic client registration returns `invalid_client_metadata` (or
  `invalid_redirect_uri`), and the conformance and developer seeders stop with an error naming the client. Every
  library setting declares its domain:
  - `security_profiles` may list only registered security profiles — a misspelled `fapi2` no longer leaves a client
    silently without the FAPI 2.0 baseline. The security-profile catalog also throws, rather than applying no
    profile, for a profile name that is not registered.
  - `token_endpoint_auth_method` and `token_endpoint_auth_methods_supported` accept only the methods of the registered
    client-authentication handlers; the enumerated settings (`subject_type`, `backchannel_token_delivery_mode`,
    `refresh_token_reuse_policy`, `access_token_type`, the response-type, response-mode, prompt, display, PKCE,
    subject-type, delivery-mode, and claim-type lists) accept only their protocol vocabularies; a grant type is a named
    grant type or an absolute URI (an extension grant).
  - Algorithm lists accept only registered algorithms of their kind, never `none` or `dir`; the signing lists for ID
    tokens, UserInfo responses, access tokens, and DPoP proofs accept only asymmetric algorithms.
  - Lifetimes and intervals are at least one second, timeouts are positive, sizes are at least one, durations that zero
    disables are not negative, and `user_code_length` is between 8 and 32.
  - URIs, host allow-lists, the mutual-TLS alias authority, the expected `request_uri` media type, the `tls_client_auth`
    identities, and `jwks` are checked the way the runtime uses them (`jwks_uri`, `sector_identifier_uri`, and
    `client_notification_endpoint` are https; redirect URIs are absolute without a fragment).
  ([ADR-0140](docs/decisions/0140-setting-values-are-validated-against-their-domain.md))

  **Breaking:** `SettingDescriptor` gains the abstract `TryValidate`, and `SettingDescriptor<TValue>` gains `Rule`
  (`SettingRule<TValue>`, built with the new `SettingRules`). Symmetric (`HS*`) signing can no longer be configured for
  ID tokens, UserInfo responses, or access tokens. `OpenIdConstants.RefreshTokenExpirationPolicy.None` is removed:
  a refresh token always expires (RFC 9700 §4.14.2). The `token_endpoint_auth_method` descriptor is contributed by the
  authentication library. `client_notification_endpoint` defaults to empty. A list setting that contains `null` is a
  type error. Stored documents that hold an out-of-domain value now fail when they are loaded and must be corrected.

- **Response types are whole values, enforced exactly and kept consistent with grant types.**
  `response_types_supported` lists whole `response_type` values — `code`, `id_token`, `token`, `code id_token`,
  `code token`, `id_token token`, `code id_token token` — in canonical form (names in ordinal order), the shape OpenID
  Connect Discovery, RFC 8414, and RFC 7591 define. Discovery advertises the list as configured instead of expanding its
  names into every combination, and the authorization endpoint accepts exactly a listed value (its names in any order),
  so an operator enables one implicit or hybrid flow by listing it — `code id_token` no longer drags in `id_token`,
  `token`, or `code token`. A response type also needs the grant types it uses (OpenID Connect Registration §2:
  `authorization_code` for `code`, `implicit` for `id_token` or `token`); a response type the tenant does not support is
  rejected with `unsupported_response_type`, and one the client may not use with `unauthorized_client`.

  Dynamic client registration registers combined values such as `code id_token`, stores and reports them in canonical
  form, defaults omitted `grant_types` to the grant types the registered response types use (and omitted
  `response_types` to `code` only for a client that uses the authorization endpoint), and refuses with
  `invalid_client_metadata` a response type whose grant type is not registered or a grant type or response type the
  tenant does not support. The registration response reports an empty list (`"response_types": []`) instead of omitting
  it. ([ADR-0141](docs/decisions/0141-response-types-are-whole-values.md))

  **Breaking:** a `response_types_supported` value lists a whole response type, so a configuration that listed names to
  enable their combinations (`[code, id_token]`) now enables only those names and must list the combination
  (`code id_token`); a value in non-canonical order (`id_token code`) is outside the domain. The `hybrid` grant type is
  removed (`OpenIdConstants.GrantTypes.Hybrid`, and as a `grant_types_supported` value): a hybrid flow uses
  `authorization_code` and `implicit`, and the token endpoint answers only `grant_type=authorization_code` for a code.
  `IAuthorizationRequest.GrantType` is removed. `OpenIdConstants.ResponseTypes` gains the four combined values, and the
  error factory gains `UnsupportedResponseType`.

- **`response_types_supported` is the only control over the tokens the authorization endpoint returns.** The
  `allow_unsafe_token_response` setting is removed. It was a second switch for the same capability: a deployment that
  listed `code token` but turned the switch off advertised and registered a flow it then refused at every request, and
  the refusal was always `unauthorized_client`, even when the tenant imposed it. To forbid an access token from the
  authorization endpoint, a tenant or client lists no response type that includes `token` (the default list is `code`
  alone). Discovery, dynamic client registration, and the authorization endpoint then agree, and a refusal is
  `unsupported_response_type` when the tenant imposes it and `unauthorized_client` when only the client does.
  ([ADR-0142](docs/decisions/0142-listed-response-types-are-the-only-front-channel-token-control.md))

  The `RetireAllowUnsafeTokenResponse` migration rewrites every stored server, tenant, client, and resource-server
  settings document that sets it, keeping what it restricted: `true` or `null` is removed; `false` becomes a
  `response_types_supported` in the same document that lists the document's values that do not include `token` — or,
  when the document lists none, `["code", "id_token", "code id_token"]`. A rewritten document gets a new settings
  concurrency token. A value that is not a boolean or `null` is left as is, so its document keeps failing when it is
  read.

  **Breaking:** `OpenIdSettingNames.AllowUnsafeTokenResponse` and `OpenIdSettingKeys.AllowUnsafeTokenResponse` are
  removed. Configuration that names `allow_unsafe_token_response` stops the host when it starts; remove it, and narrow
  `response_types_supported` instead if it was `false`. A deployment that stores settings documents in its own database
  must apply the same rewrite there, or a document that names the setting fails when it is read.

- **Every authorization code is bound to PKCE by default.** `require_pkce` now defaults to `true`, so an authorization
  request whose response type issues a code must carry an `S256` `code_challenge`, for every client; a missing one is
  refused with `invalid_request` (RFC 7636 §4.4.1), not `unauthorized_client`, at both the authorization and the pushed
  authorization request endpoints. A response type that issues no code (an implicit flow an operator enabled) is not
  affected. The only exception is the one OAuth 2.1 §7.5.1.1 allows, and every condition must hold: the server's
  configuration relaxes `require_pkce`; an administrator attests the client with the new `pkce_nonce_exemption`
  setting (dynamic client registration never sets it); the client is registered as confidential; and the request
  requests `openid` and carries a `nonce`. A public client is always bound to PKCE, and at the token endpoint a code
  that is not bound can be redeemed only by a client that authenticates and keeps `openid` in scope, so the ID Token
  returns the nonce. ([ADR-0144](docs/decisions/0144-authorization-codes-are-bound-to-pkce-by-default.md))

  PKCE is `S256`: the `plain` method, which sends the verifier in the authorization request, is removed (OAuth 2.1
  §7.5.2). `code_challenge_methods_supported` accepts the registered transformations and defaults to `S256` alone, so a
  deployment that registers another transformation offers it by listing it. A `code_challenge` must name its
  `code_challenge_method` — RFC 7636's implicit `plain` default no longer applies — and both the challenge and the
  verifier must have the RFC 7636 syntax (43 to 128 unreserved characters). The `RetirePlainCodeChallengeMethod`
  migration removes `plain` from every stored server, tenant, client, and resource-server
  `code_challenge_methods_supported` list, giving each rewritten document a new settings concurrency token; a list that
  named only `plain` becomes empty.

  The conformance host relaxes `require_pkce` and attests its clients, because the OpenID Foundation suite is a
  confidential relying party that validates the nonce but sends PKCE only in one module. The Basic OP module that sends
  neither PKCE nor a nonce (`oidcc-ensure-request-without-nonce-succeeds-for-code-flow`) is an expected failure. A
  seeded conformance client registers one `TokenEndpointAuthMethod` — its `token_endpoint_auth_method`, the only method
  it may use — so it is a confidential client the way a dynamically registered one is.

  **Breaking:** a client that sends no `code_challenge` is refused until it uses PKCE, or until the deployment relaxes
  `require_pkce` and attests it with `pkce_nonce_exemption`, if it is a confidential OpenID Connect client that sends a
  nonce. `OpenIdConstants.CodeChallengeMethods.Plain` is removed, `AuthorizationRequest.CodeChallengeMethod` is `null`
  when the request names no method, and configuration that lists `plain` stops the host when it starts. A deployment
  that stores settings documents in its own database must remove `plain` from them, or a document that lists it fails
  when it is read. `ConformanceClientSeed.TokenEndpointAuthMethods` is replaced by `TokenEndpointAuthMethod`.

- **A client authenticates only with the method it is registered for.** A client's `token_endpoint_auth_method` (RFC
  7591 §2) is now the one method that authenticates it, at every endpoint that authenticates clients — token, pushed
  authorization request, introspection, revocation, device authorization, and back-channel authentication — and it
  decides the client's type: `none` registers a public client, any other method a confidential one. Previously a client
  could use any method its `token_endpoint_auth_methods_supported` permitted, and that list includes `none` by default,
  so a client registered as confidential — every dynamically registered `client_secret_*` client — could present only
  its `client_id` and be served as public, for example to redeem a refresh token or push an authorization request. Such
  a request, a credential of another method, and a secret presented by a public client are now `invalid_client`.
  ([ADR-0145](docs/decisions/0145-a-client-authenticates-only-with-its-registered-method.md))

  A client that registers no method uses the RFC 7591 default, `client_secret_basic` — the default dynamic client
  registration already applied — unless its server or tenant configures another; so a client created through the
  management API without one is a confidential client that authenticates with HTTP Basic once it holds a secret.
  `token_endpoint_auth_methods_supported` is the ceiling the server, tenant, and security profile impose over the
  registered method: a client registered outside it cannot authenticate, and dynamic client registration refuses such a
  method with `invalid_client_metadata`. The authorization endpoint identifies the client by its `client_id` and never
  authenticates it, and it refuses a request that carries a `client_secret` with `invalid_request`.

  The developer single-page-application clients register `none`, a conformance client registers only its configured
  method, and the bootstrap administrator registers `client_secret_basic`, so the API reference's client-credentials
  login now sends its credentials with HTTP Basic. The `RegisterClientAuthenticationMethod` migration rewrites every
  stored server, tenant, client, and resource-server settings document that registers no method: one whose
  `token_endpoint_auth_methods_supported` lists exactly one method registers that method (such as a developer client
  pinned to `["none"]`), and an empty `token_endpoint_auth_method` is otherwise removed; each rewritten document gets a
  new settings concurrency token.

  **Breaking:** a client that authenticates with a method other than the one it registers — including a client that
  registers none and authenticates with anything but HTTP Basic — is refused until it registers its method. An empty
  `token_endpoint_auth_method` is no longer a valid value, and configuration that sets it stops the host when it
  starts. A request without a secret in HTTP Basic credentials (no colon) is `invalid_client`. The bootstrap
  administrator presents its secret with HTTP Basic, not in the request body. A deployment that stores settings
  documents in its own database must apply the same rewrite there.

- **The developer seed registers only the SPA clients its configuration declares.**
  `DeveloperInteractiveSeedOptions.SpaClients` used to start with `mgmt-spa` and `playground-spa`, and the
  configuration binder appends to a list that already holds entries, so `DeveloperInteractiveSeed:SpaClients` could
  add clients but never replace or remove those two. The list now starts empty and the seeder registers exactly the
  configured clients, or none when nothing is configured. That makes the seed inert in a host that does not ask for
  it, as `AddDevelopmentEnvironment` documents; previously every non-Production host, the Conformance environment
  included, persisted the two loopback clients.

  The reference host declares both clients in `appsettings.Development.json`, beside the `Frontend` section that
  publishes the same client ids, so local development, the Aspire stack, and the integration tests are unchanged. An
  integration test fails if the bootstrap document publishes a client id the seed does not register.
  ([ADR-0146](docs/decisions/0146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md))

  **Breaking:** a host that relied on the built-in clients declares them under `DeveloperInteractiveSeed:SpaClients`.

- **A security profile that a server or tenant selects binds every scope below it, whichever profile it is.** The
  `security_profile` setting (one profile name) is replaced by `security_profiles`, a list that accumulates down the
  server → tenant → client cascade: a scope can add a profile but can no longer clear or replace one a scope above it
  selects, and every selected profile applies — its ceilings and its request-time requirements. Previously only
  `fapi2` was kept when a child set another value, so a client could clear or replace any other mandated profile; the
  profile's ceilings stayed in force but its requirements silently stopped running. A layered profile selects the
  profile it builds on, and selecting it applies both. Profiles whose ceilings contradict leave nothing permitted on
  that dimension, which fails closed. ([ADR-0148](docs/decisions/0148-security-profiles-accumulate-down-the-settings-cascade.md))

  A profile selected at the server scope — in `OpenId:Server:Settings` or the server's stored settings — now injects its
  ceilings, so discovery advertises them and every tenant and client enforces them; previously it switched on only the
  requirements. The management API's tenant, client, and resource-server effective-settings previews apply the selected
  profiles' ceilings, as the runtime does.

  A profile's ceiling binds by construction: a profile contributes it through `ISecurityProfileCeilingBuilder`, which
  accepts only a registered setting that merges with a lattice operation (`Min`, `Max`, `And`, `Or`, `Intersect`, or
  `Union`), so no child scope can override it and several profiles combine in any order. A ceiling on a setting that a
  child could override, or whose merge would ignore it, fails when a scope selects the profile. A requirement's error
  description now reads "a selected security profile".

  The `TranslateSecurityProfileToSecurityProfiles` migration rewrites every stored server, tenant, client, and
  resource-server settings document that sets `security_profile`: a non-empty name becomes a one-element
  `security_profiles`, and an empty or `null` one, which selected no profile, is removed. Each rewritten document gets a
  new settings concurrency token; a value of another type is left as is, so its document keeps failing when it is read.

  **Breaking:** `OpenIdSettingNames.SecurityProfile` and `OpenIdSettingKeys.SecurityProfile` are replaced by
  `SecurityProfiles` (a `SettingKey<IReadOnlyCollection<string>>`), and configuration that sets `security_profile` stops
  the host when it starts — list the profile under `security_profiles` instead. `ISecurityProfile.ApplyCeilingSettings`
  takes an `ISecurityProfileCeilingBuilder` instead of an `ISettingCollection`, and `SettingDescriptor` gains the abstract
  `HasLatticeMerge`. A deployment that stores settings documents in its own database must apply the same rewrite there,
  or a document that names `security_profile` fails when it is read.

- **A setting is accepted only at a scope that reads it.** Every setting now declares the scopes it applies to — the
  server, tenant, client, or resource-server scopes whose effective settings the runtime reads it from — and a settings
  document may carry it only at one of those scopes or a scope above it in the cascade. A setting anywhere else had no
  effect and was stored silently: `security_profiles` or `require_pkce` on a resource server, or a tenant-wide setting
  such as `session_idle_timeout`, `subject_claim_types`, or discovery metadata on a client. It is now rejected wherever
  it enters, like an unregistered name: configuration stops the host, a management create or settings patch returns
  `400`, dynamic client registration refuses it, a seeder stops naming the client, and a stored document fails the
  request that loads it. The error names the scopes the setting applies to. A resource server selects no security
  profile; a constraint it needs on its access tokens is one of its own settings (issue [#101](https://github.com/NCodeGroup/Identity/issues/101)).
  ([ADR-0149](docs/decisions/0149-settings-declare-the-scopes-they-apply-to.md))

  Scopes are an open vocabulary, not a fixed set: each is a registered `SettingScopeDescriptor` (a name and the scope it
  inherits from), and the cascade is derived from them. The server registers `server`, `tenant`, `client`, and
  `resource_server`; a package that stores settings for an entity of its own — an organization, a connection —
  registers that entity's scope beneath the one it inherits from and declares its own settings for it, with no change to
  the library. ([ADR-0150](docs/decisions/0150-vocabularies-are-open-unless-provably-closed.md))

  The management API's effective-settings previews list only the settings that apply within the previewed scope: a
  resource server's preview no longer shows the client settings it inherits from its tenant, and a client's no longer
  shows the tenant's session and discovery settings.

  The `RemoveSettingsOutsideTheirScopes` migration removes those settings from every stored client and resource-server
  settings document. They never had an effect where they were stored, so nothing they configured is lost; each
  rewritten document gets a new settings concurrency token, and any other setting in it that cannot be honored still
  fails closed.

  **Breaking:** `SettingDescriptor` gains the required `AppliesTo` (scope names); `SettingScopeDescriptor`,
  `ISettingScopeCollection`, `ISettingScopeCollectionProvider`, and `OpenIdSettingScopes` are new; and
  `ISettingSerializer.DeserializeSettings`, `ISettingSerializer.ValidateSettings`, and the JSON overload of
  `ISecurityProfileCatalog.Expand` take the name of the scope whose document they read. A deployment that stores
  settings documents in its own database must apply the same removal there.

- **The JWT validator takes an explicit algorithm and key policy, and the server never compresses a JWT.**
  `JwtValidationParameters` gains `DecryptionKeys` (`UseDecryptionKeys`) and the allow-lists
  `ValidSignatureAlgorithms`, `ValidKeyManagementAlgorithms`, `ValidContentEncryptionAlgorithms`, and
  `ValidCompressionAlgorithms` (`null` is unrestricted, empty permits none), checked against every header before any
  cryptographic operation. An encrypted token is decrypted only with the keys the caller names — never with the
  verification keys — and must enclose a signed JWT (`cty` `JWT`); a key is used only for the `use` and `alg` its
  metadata declares; the JSON serializations are rejected. `DecodedJwt.SecretKey` is now `DecodedJwt.SigningKey`, and
  a Nested JWT also reports its `EncryptionHeader` and `DecryptionKey`. The encoder marks a Nested JWT with `cty`
  `JWT`. See [ADR-0154](docs/decisions/0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md).

- **Algorithm settings name only what the server can safely honor, and the unsafe and non-standard ones are retired.**
  The four `*_encryption_zip_values_supported` settings and
  `backchannel_authentication_request_encryption_{alg,enc,zip}_values_supported` are removed: the server neither
  compresses what it encrypts nor accepts a compressed JWT (RFC 8725 §3.6), and CIBA defines no encrypted
  authentication request. The ID token, UserInfo, and request-object encryption lists admit only asymmetric key
  management (`RSA-OAEP*`, `ECDH-ES*`), because those tokens are encrypted to a party's published key; the CIBA signing
  list admits only asymmetric signatures; and no list offers `RSA1_5`. The request-object, UserInfo, and ID token lists
  now apply to clients as well as tenants. The `RetireUnsafeJoseSettingValues` migration removes the retired settings
  from every stored settings document and strips the values the narrowed lists no longer admit (an emptied list stays
  empty); each rewritten document gets a new settings concurrency token.

  **Breaking:** the seven retired `OpenIdSettingNames` / `OpenIdSettingKeys` members are removed and six are added;
  `AuthenticateSubjectCommand` now authenticates only the end-user session, and the new
  `AuthenticateResourceRequestCommand` authenticates a bearer access token for a set of required audiences;
  `RegisterClientRequest` and `RegisterClientResponse` gain the registration metadata above. A deployment that stores
  settings documents in its own database must apply the same migration there.
  ([ADR-0155](docs/decisions/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md))

- **An access token is bound by its client and by every resource server it targets, and a resource server can constrain
  the tokens issued for it.** The client's effective settings and those of every targeted resource server combine by
  their lattice merges: a requirement any of them states holds, a lifetime is the shortest, and an algorithm list
  permits only what all of them permit, in the server's order. A resource server can now set `access_token_lifetime`,
  `access_token_signing_alg_values_supported`, the new `access_token_sender_constraint_required` (the token must be
  DPoP- or mutual-TLS-bound), and the new `access_token_confirmation_methods_supported` (the `cnf` methods it verifies,
  `jkt` and `x5t#S256`); every audience of a multi-audience token binds it. A client's sender-constraint requirement is
  refused at the token endpoint before its grant is spent, and the FAPI 2.0 profile states its sender-constrained-token
  rule as the ceiling `access_token_sender_constraint_required = true`.

  An encrypted access token is encrypted to the party that reads it. A resource server decrypts its own tokens when it
  holds a key it can decrypt with under its own settings: a token for it alone is encrypted to that key within the
  combined settings, and issuance fails with `server_error` rather than encrypt to the tenant's key, which it cannot
  read; a token for it together with another resource server is refused with `invalid_request`. Any other encrypted
  token goes to the tenant's key. A missing access-token signing or encryption key is now `server_error`.

  **Breaking:** a multi-audience encrypted token whose resource servers hold no keys is encrypted to the tenant's key
  instead of refused, and a client-required encrypted token targeting a resource server that decrypts its own tokens
  together with another is refused instead of encrypted to the tenant's key. `IReadOnlySettingCollection` gains
  `Combine` (the lattice-only combination of sibling scopes' settings), `ResolvedResourceServer` gains
  `SecretsProvider`, `ResourceServerCredentialResolution.SingleSecretsProvider` is removed,
  `IAccessTokenPolicyService` and `AccessTokenPolicy` are new — a replacement `ITokenService` issues access tokens under
  the policy the service evaluates, and an extension can replace the service to add a bound — and
  `OpenIdSettingNames` / `OpenIdSettingKeys` gain `AccessTokenSenderConstraintRequired` and
  `AccessTokenConfirmationMethodsSupported`. No stored document needs migrating.
  ([ADR-0158](docs/decisions/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md))

### Removed

- `OpenIdConstants.SecurityTokenTypes.LogoutToken` is removed. Breaking: use `JoseTokenTypes.LogoutJwt`
  (`logout+jwt`), the single definition of the back-channel logout token's `typ`, which the OP now stamps on its
  `logout_token` ([#355](https://github.com/NCodeGroup/Identity/issues/355)).

- `IsActiveResult`, `PositiveIsActiveResult` and `NegativeIsActiveResult` (`NCode.Identity.Models`) are removed from
  `NCode.Identity.Abstractions`; nothing used them.

- `KeyRingProtectionOptions`, `KeyRingCertificateOptions` and certificate key-ring protection
  (supersedes ADR-0177).

### Security

- **`RSA1_5` is not a registered JOSE algorithm.** RSAES-PKCS1-v1_5 key encryption is open to padding-oracle attacks
  (RFC 8725 §3.2), and a JWE that named it decrypted through `IJoseSerializer` and through an `IJwtValidator` call
  that sets no `ValidKeyManagementAlgorithms`. `AddJoseLibrary()` registers RSA key management only as `RSA-OAEP`,
  `RSA-OAEP-256`, `RSA-OAEP-384` and `RSA-OAEP-512`, so such a JWE fails with `JoseInvalidAlgorithmException`.
  `AlgorithmCodes.KeyManagement.RsaPkcs1` remains as the name a policy refuses, and the algorithm settings and the
  protocol client refuse `RSA1_5` even when a host registers an implementation of its own. **Breaking:** a consumer
  that exchanges `RSA1_5` JWEs moves to RSA-OAEP or ECDH-ES
  ([ADR-0282](docs/decisions/0282-the-jose-library-does-not-implement-rsa1-5.md), issue
  [#339](https://github.com/NCodeGroup/Identity/issues/339)).

- **ES256, ES384 and ES512 accept only their own curve.** RFC 7518 §3.4 binds them to P-256, P-384 and P-521, so a
  256-bit key on another curve no longer signs as ES256, and `EccSecretKey.GetECCurve()` reads the actual curve from
  the key material instead of inferring it from the key size
  ([ADR-0272](docs/decisions/0272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md)).

- **Grant payloads are encrypted at rest.** A persisted grant's payload (authorization codes, refresh tokens,
  sessions, device, CIBA and pushed authorization requests, DCR tokens and continue envelopes) is stored only as an
  `nce1.` envelope under the master key of the grant's tenant, with purpose `grant-payload` and the grant's hashed key
  as the item id, so a payload copied to another grant row does not decrypt. A payload that cannot be unprotected makes
  the grant absent and logs a warning rather than failing the request. **Breaking:**
  `PersistedGrant.PayloadJson`/`GrantEntity.PayloadJson` become `ProtectedPayload` (`string`), and the
  `ProtectGrantPayloads` migration deletes every stored grant, so users sign in again
  ([ADR-0265](docs/decisions/0265-grant-payloads-are-protected-under-the-tenant-master-key.md), issue
  [#175](https://github.com/NCodeGroup/Identity/issues/175)).
- **Client-certificate revocation is checked through OCSP and every CRL form.** The default
  `IClientCertificateRevocationChecker` asks each registered `IClientCertificateRevocationSource` in registration
  order until one answers `Good` or `Revoked`. The `ocsp` source queries the responders that the certificate's
  authority information access names (RFC 6960 with the RFC 5019 profile: a SHA-1 `CertID`, no nonce, GET up to 255
  characters and POST beyond), accepts a response signed by the issuer or by a responder it issued with
  `id-kp-OCSPSigning`, checks a responder without `id-pkix-ocsp-nocheck` through its CRLs, and caches a response until
  its `nextUpdate`. The `crl` source follows RFC 5280 §6.3.3: partitioned lists (`reasons`, `onlySomeReasons`),
  indirect lists (`cRLIssuer`, `indirectCRL`, `certificateIssuer`), delta lists (`freshestCRL`, `deltaCRLIndicator`),
  `removeFromCRL`, and a list signed by another key whose certificate chains to the same trust anchor and is not
  revoked. A parsed list is cached per location and verified by each check, and a responder location that fails is
  remembered for `FailureCacheDuration`. **Breaking:** `IClientCertificateRevocationChecker.CheckAsync` takes a
  `ClientCertificateRevocationContext` (`Certificate`, `Issuer`, `TrustAnchor`, `Intermediates`, `Depth`), and
  `IClientCertificateRevocationSource` and `ClientCertificateRevocationSourceNames` are added. Log events 2050 and 2061
  keep their IDs, but their messages name any revocation source and record the location as `Location` instead of
  `CrlUri`
  ([ADR-0259](docs/decisions/0259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md), issue [#255](https://github.com/NCodeGroup/Identity/issues/255)).

- **The server sends `Strict-Transport-Security` on every HTTPS response outside Development.**
  `AddStrictTransportSecurity` binds `StrictTransportSecurityOptions` strictly from the `StrictTransportSecurity`
  section (`Enabled`, `MaxAge`, `IncludeSubDomains`, `Preload`, `ExcludedHosts`) and validates it when the host starts,
  and `UseStrictTransportSecurity`, which the reference host calls right after `UseReverseProxy`, adds the header to
  every response to a request that arrived over HTTPS, directly or through a trusted proxy. The default is
  `max-age=31536000; includeSubDomains`; `Preload` requires `IncludeSubDomains` and a `MaxAge` of at least 365 days;
  `localhost`, `127.0.0.1` and `[::1]` never receive it. A deployment whose TLS-terminating proxy sends the header sets
  `StrictTransportSecurity:Enabled` to `false`
  ([ADR-0258](docs/decisions/0258-the-server-sends-strict-transport-security-outside-development.md), issue [#304](https://github.com/NCodeGroup/Identity/issues/304)).

- **Every page the server writes has a strict Content-Security-Policy and cannot be framed, except the
  `check_session_iframe` and, by its client's origin, the `form_post` response.**
  `HttpResponse.SetHtmlPageSecurity(HtmlPageSecurityPolicy)` writes each page's policy (`default-src 'none'`,
  `base-uri` and `form-action` `'none'` unless the page lists them, `frame-ancestors 'none'`) with
  `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` and `no-store`;
  `ContentSecurityPolicyDirective`, `ContentSecurityPolicyDirectives` and `ContentSecurityPolicySources` build it. The
  interactive pages run only the `apps/auth` scripts and styles that carry the response's nonce, including a page
  served to a browser that already holds the antiforgery cookie; the `form_post` response runs only its own script and
  may be framed only by its redirect URI's origin; the front-channel logout page frames only the clients' logout
  origins; the `check_session_iframe` runs only its script and stays embeddable. **Breaking:** a bundle served from
  `InteractiveAuthentication:BundleRootPath` runs its scripts only when its build writes the nonce placeholder
  (`@ncode/vite-config`'s `CSP_NONCE_PLACEHOLDER`)
  ([ADR-0256](docs/decisions/0256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  issue [#303](https://github.com/NCodeGroup/Identity/issues/303)).

- **Client-certificate revocation retrieval is shared and remembers failures; RSA-PSS lists verify; soft-fail is
  opt-in.** `ClientCertificateRevocationOptions` from `OpenId:ClientCertificateRevocation` (`FetchTimeout`,
  `MaxResponseSizeBytes`, `MaxCacheDuration`, `FailureCacheDuration`) is validated when the host starts; concurrent
  checks of a list share one retrieval; a retrieval that yields no usable list is not repeated for
  `FailureCacheDuration`; a list signed with RSASSA-PSS verifies; `tls_client_auth_revocation_mode` accepts
  `online_soft_fail`, which refuses a revoked certificate and accepts an unknown status with a warning; `online` is the
  default
  ([ADR-0252](docs/decisions/0252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md),
  issue [#255](https://github.com/NCodeGroup/Identity/issues/255)).

- **A Data Protection key the server's master key cannot decrypt stops the host.** `DataProtectionKeyRingGate` checks
  the live keys after the server's protector loads and before Data Protection loads its key ring, and the host names
  each undecryptable key instead of letting Data Protection write a default key over them; a Development host revokes
  the key and logs a warning. `DataProtectionKeyRing:RevokedKeyIds` (`DataProtectionKeyRingOptions`) revokes keys by ID,
  and `AddConfiguration` registers Data Protection
  ([ADR-0247](docs/decisions/0247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md), issue [#239](https://github.com/NCodeGroup/Identity/issues/239)).

- **The host image's base images are pinned by digest and tracked by Dependabot.** Every `FROM` of the Dockerfile names
  its tag and the digest of its multi-architecture index, a weekly `docker` Dependabot stream moves the digests, and the
  `Host image` workflow checks the Node major against `.node-version`. **Breaking:** the Dockerfile takes no
  `NODE_VERSION` or `DOTNET_VERSION` build argument
  ([ADR-0249](docs/decisions/0249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md), issue
  [#226](https://github.com/NCodeGroup/Identity/issues/226)).

- **Every password a local account is given meets the password policy.** The management API's create and reset, the
  self-service change and the developer and conformance seeders judge the password with `IPasswordPolicy` against the
  tenant's settings: at least `password_min_length` characters (15 by default, never below 8, counted as Unicode code
  points), at most `password_max_length` (128 by default, never below 64), no composition rules, and not on a
  blocklist — the bundled list of 46,483 common passwords and the account's own user name or email; a deployment adds a
  breached-password checker by registering an `IPasswordBlocklist`. The management API answers a refused password
  `400`; the change page shows `password_too_short`, `password_too_long` or `password_blocklisted` with guidance. The
  developer seed's administrator password is `dev-admin-password`. **Breaking:** `InteractivePasswordChangeResult` is a
  class carrying the page's error code (`InteractivePasswordChangeErrors`), and `IPasswordPolicy`, `IPasswordBlocklist`,
  `PasswordPolicyContext`, `PasswordPolicyResult` and `PasswordPolicyErrors` are added
  ([ADR-0236](docs/decisions/0236-a-password-is-judged-by-one-policy-wherever-it-is-set.md), issue [#292](https://github.com/NCodeGroup/Identity/issues/292)).

- **Failed password attempts are limited per account.** The interactive sign-in, the password grant and the
  self-service change count failed passwords per account in `IExpiringCounterSet`; once
  `password_failed_attempts_limit` failures (10 by default, at most 100) fall within `password_failed_attempts_window`
  of the first (15 minutes by default), every password for the account is refused with the answer a wrong password
  gets until the window ends. A verified password, and a password an administrator sets, clear the count.
  **Breaking:** `IExpiringCounterSet` gains `RemoveAsync`, and `IPasswordAttemptLimiter` is added
  ([ADR-0237](docs/decisions/0237-failed-password-attempts-are-limited-per-account.md), issue [#293](https://github.com/NCodeGroup/Identity/issues/293)).

- **A password is judged, hashed and verified in its NFC form.** The policy counts the code points of a password's
  Unicode Normalization Form C (NFC) and its blocklists see that form, and the stores hash and verify it, so a password
  typed with another composition of the same characters, such as `e` and a combining accent for `é`, verifies; a stored
  hash of the password as submitted still verifies and is replaced by a hash of the NFC form. **Breaking:** an
  `IPasswordHasher` hashes and verifies the NFC form of the bytes it is given
  ([ADR-0238](docs/decisions/0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md), issue [#297](https://github.com/NCodeGroup/Identity/issues/297)).

- **A wrong password takes as long whether or not an account has the user name.** When no account has the user name,
  the account has no password, the ASP.NET Core Identity user is locked out, or the default hasher cannot read the
  stored hash, the store verifies the password against a dummy hash of the current parameters and refuses it, so a
  wrong password costs the same hash verifications whether or not the account exists. **Breaking:** `IPasswordHasher`
  gains `DummyPasswordHash`
  ([ADR-0239](docs/decisions/0239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md), issue
  [#298](https://github.com/NCodeGroup/Identity/issues/298)).

- **A password refused at the attempt limit costs what a wrong one costs.** `ILocalAccountStore.VerifyCredentialAsync`
  looks the account up once and asks its caller's `isRefusedAsync` about the user name the account holds; the sign-in,
  the password grant and the self-service change ask `IPasswordAttemptLimiter.IsLimitReachedAsync` through a gate that
  enforces its answer whatever the store returns, so a password refused at the limit is verified against the dummy hash
  and counted as a failed attempt, and neither the answer nor the work tells whether an account has the user name under
  any spelling a database collation matches. **Breaking:** `VerifyCredentialAsync` takes `isRefusedAsync` and returns
  `LocalAccountVerificationResult` (`UserName`, `Account`, `IsRefused`, `IsVerified`), and a store that never calls
  `isRefusedAsync` fails every verification
  ([ADR-0244](docs/decisions/0244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md), issue [#300](https://github.com/NCodeGroup/Identity/issues/300)).

- **A blocked or disabled end user cannot sign in, and the client is answered `access_denied`.** The interactive
  sign-in validates the end user's standing before it creates a session, so a blocked principal or a disabled local
  account gets neither a session nor a cookie: a sign-in that resumes an authorization request answers it with
  `access_denied` at the client's redirect URI, and any other sign-in shows the refusal on the login page. The
  authorization request answers a signed-in end user whose standing is refused with `access_denied` for every
  `prompt`, a consent denial answers the request with `access_denied`, and the self-service pages serve a cookie
  only while the session it names is active. **Breaking:** `ValidateSubjectStandingCommand` is added and the
  blocked-principal and local-account checks handle it (every subject validation still applies them),
  `IContinueProvider.RefuseAsync` and `IContinueRefusalService` are added, and
  `ILocalAccountStore.VerifyCredentialAsync` returns the disabled account of a valid credential
  ([ADR-0229](docs/decisions/0229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md), issue
  [#289](https://github.com/NCodeGroup/Identity/issues/289)).

- A database dump or backup no longer reveals secret material without the root key. Production refuses the ephemeral
  root key provider, an inline local root key (whatever provider is selected), and running without a recovery-key
  decision, reporting them with every other production-safety finding (the failure now reads "setting(s) are unsafe
  for a production deployment").

- **Every grant that issues tokens for a subject validates it.** The device-code grant, the CIBA grant (at the token
  endpoint, and at the approval for every delivery mode) and the token-exchange grant send
  `ValidateSubjectAuthenticationCommand`, the device-code and CIBA grants before they spend their credential, so a
  disabled, deleted or blocked end user is refused with `access_denied`, and a refused CIBA approval denies the request.
  A device code and an `auth_req_id` are redeemed only by the client they were issued to (`invalid_grant`). The device
  verification endpoint checks the server-side session of its cookie and asks for a new login when it has ended.
  **Breaking:** `ValidateSubjectAuthenticationCommand.OpenIdRequest` is nullable (`null` outside a protocol request
  skips the request's `max_age`, `acr_values` and `claims` checks), and `ICibaRequestService.ApproveAsync` returns
  `false` for a refused approving subject
  ([ADR-0225](docs/decisions/0225-every-grant-that-issues-tokens-for-a-subject-validates-it.md), issue [#283](https://github.com/NCodeGroup/Identity/issues/283)).

- **An administrator's password reset ends the account's sessions.** After the reset commits, every session of the
  account's principal is terminated with `password_reset`: its grants and refresh tokens are revoked, its relying
  parties receive back-channel logout, and its consents are kept. **Breaking:**
  `SessionTerminationReasons.PasswordReset` is added
  ([ADR-0226](docs/decisions/0226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md),
  issue [#285](https://github.com/NCodeGroup/Identity/issues/285)).

- **Deleting or disabling a local account ends its sessions, and every token request checks the account.** Disabling an
  account terminates its principal's sessions with `account_disabled`, and deleting it terminates them with
  `account_deleted` before it removes anything: every grant of the principal is revoked, its relying parties receive
  back-channel logout, and each termination is audited. A deleted principal is erased with its consents and grants, and
  a disabled account keeps its consents. The local-account capability refuses a subject whose account is disabled or
  gone with `access_denied` at the authorization request and at the authorization-code, refresh-token and password
  grants, whichever connection the subject signed in through. **Breaking:**
  `SessionTerminationReasons.AccountDeleted` and `AccountDisabled` are added, `IFederatedPrincipalStore.RemoveAsync`
  erases the principal's consents and grants, and `NCode.Identity.OpenId.Accounts.Management` references
  `NCode.Identity.OpenId.Authentication.Abstractions`
  ([ADR-0224](docs/decisions/0224-deleting-or-disabling-an-account-ends-its-sessions.md), issue [#280](https://github.com/NCodeGroup/Identity/issues/280)).

- **A session that ends while no request uses it is terminated within a minute.** One replica in each
  `OpenId:EndedSessionTermination:Interval` (one minute by default) revokes the grants issued under each session that
  ended unused, sends back-channel logout to its relying parties (`idle_timeout` or `absolute_expiry`), and audits the
  termination, once: a request and the pass that find the same ended session terminate it once. The claim revokes the
  grants issued under the session in one unit of work, so a failure after it leaves no token of the session active. A
  session that the release before the session bounds stored without an expiry is terminated too. The grant prune keeps
  a session until it is terminated and removes it one retention after. **Breaking:**
  `IPersistedGrantService.SetRevokedOnceAsync` returns `bool`, `IGrantStore.RemoveEndedBeforeAsync` takes the grant
  types that end by revocation, `IGrantStore.GetUnrevokedExpiredPageAsync`,
  `IPersistedGrantService.RevokeSessionOnceAsync`, `ISessionService.TerminateIfEndedAsync`,
  `IOpenIdContextFactory.CreateForBackgroundAsync` and `IMaterializedServerAccessor` are added, and the
  `AddGrantTypeRevokedWhenExpiresWhenIndex` migration adds an index to the grants table
  ([ADR-0223](docs/decisions/0223-a-session-that-ends-unused-is-terminated-once-in-the-background.md), issue [#281](https://github.com/NCodeGroup/Identity/issues/281)).

- **Every end-user session ends.** `session_absolute_lifetime` defaults to 30 days and accepts one second to 365 days,
  and `session_idle_timeout` defaults to 3 days and accepts one second to 100 days; a zero value is refused. A session's
  grant expires at the end of its idle window or, if earlier, at its absolute end, and each renewal of the window moves
  the expiry, so the grant prune removes an abandoned session after the retention. An authorize request that finds an
  ended session terminates it with `absolute_expiry` or `idle_timeout`, which revokes the grants issued under it and
  notifies its relying parties. The `BoundEndUserSessions` migration removes the stored values of both settings that the
  domains refuse and gives every unrevoked session grant without an expiry one 30 days after it was created
  ([ADR-0220](docs/decisions/0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md), issue [#271](https://github.com/NCodeGroup/Identity/issues/271)).

- **A public client's refresh token is rotated unless it is sender-constrained.** `refresh_token_rotation_policy`
  (`unbound`, the default, or `always`) replaces `refresh_token_rotation_enabled`. `unbound` rotates a refresh token
  that is bound neither to a proof-of-possession key nor to client authentication, and `always` rotates every refresh
  token. No setting turns off the rotation of a public client's unbound refresh token (RFC 9700 §4.14.2), and the FAPI
  2.0 security profile defaults a client to `unbound`. **Breaking:** the `refresh_token_rotation_enabled` setting and
  `OpenIdSettingNames`/`OpenIdSettingKeys.RefreshTokenRotationEnabled` are removed, and the
  `RenameRefreshTokenRotationSetting` migration rewrites stored settings documents (`true` becomes `always`, `false` is
  removed). The policy merges by replacement, so a tenant's `always` is a default that a client's own `unbound`
  replaces; a public client's unbound token rotates whatever the policy
  ([ADR-0216](docs/decisions/0216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md), issue [#268](https://github.com/NCodeGroup/Identity/issues/268)).

- **A refresh token that two requests present at once is spent once, and the other request is answered as a reuse.**
  With rotation on, the presented token is revoked and its successor stored in one unit of work before any token is
  issued. The losing request receives `invalid_grant` and the `token.refresh.replay` audit event, and under
  `refresh_token_reuse_policy` `revoke_all` (the default) the whole rotation family is revoked, the winner's new token
  included (RFC 9700 §4.14.2)
  ([ADR-0211](docs/decisions/0211-a-rotated-refresh-token-is-spent-with-its-successor.md), issue [#260](https://github.com/NCodeGroup/Identity/issues/260)).

- **Deleting a tenant erases the data of its end users.** Its local accounts and their claims, federated principals and
  identities, sessions and other grants are removed with it; the `409` for its clients and resource servers is
  unchanged and its usage is kept
  ([ADR-0212](docs/decisions/0212-deleting-an-owner-removes-what-it-owns.md), issue [#250](https://github.com/NCodeGroup/Identity/issues/250)).

- **Revocation data is fetched only through the outbound-request guard.** The platform chain engine retrieves nothing:
  certificate downloads are disabled and its own revocation check is off (`X509RevocationMode.NoCheck`), so no AIA, CRL,
  or OCSP URL in a presented certificate is fetched by the platform. The server fetches a CRL itself through the named
  client `NCode.Identity.OpenId.ClientCertificateRevocation`, whose connections go through `IOutboundHttpForgeryGuard`,
  which follows no redirect, and which times out after 5 seconds and reads at most 10 MiB, so a certificate cannot
  direct the server to an internal address. A CRL that is not DER, not current, not issued and signed by the issuing
  certificate, or not understood is `Unknown`, which the chain validator refuses
  ([ADR-0173](docs/decisions/0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md), issue [#207](https://github.com/NCodeGroup/Identity/issues/207)).

- **An initial access token authorizes one registration.** Every token, scoped or not, is spent by the registration it
  authorizes and expires within 30 days, so a leaked token registers at most one client, and only until it expires. A
  token that is spent, expired, or revoked is refused with `401` `invalid_token`. A registration that an earlier check
  refuses leaves the token unspent, and a failure storing the client after the spend leaves it spent
  ([ADR-0192](docs/decisions/0192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md),
  [ADR-0063](docs/decisions/0063-dynamic-client-registration.md), issue [#208](https://github.com/NCodeGroup/Identity/issues/208)).

- **A private-use redirect URI must be a reverse domain name, and a URI of no kind is never permitted.** A
  `private_use` URI has a scheme of at least two non-empty labels, such as `com.example.app`, so `javascript:`, `data:`,
  `ws:`, `file:`, and `myapp:` are no redirect target, even with `redirect_uri_kinds_supported` unset
  ([ADR-0203](docs/decisions/0203-redirect-uris-are-matched-exactly.md), issue [#247](https://github.com/NCodeGroup/Identity/issues/247)).

- **The end-session redirect checks the post-logout URI's kind and escapes its `Location`.** A
  `post_logout_redirect_uri` is refused like an unregistered one when `redirect_uri_kinds_supported` excludes its kind,
  and the `Location` header is escaped as an authorization response's is
  ([ADR-0203](docs/decisions/0203-redirect-uris-are-matched-exactly.md), issue [#247](https://github.com/NCodeGroup/Identity/issues/247)).

- **A redirect URI is delivered and compared exactly as written.** A parked continuation and a stored authorization
  code carried `myapp://callback` as `myapp://callback/` with a lower-cased host, the response went to the form a
  `UriBuilder` produced, and the token endpoint accepted a `redirect_uri` that differed in the case of its host or in
  its user information. The response now goes to the string the client sent, each registered query parameter reaches
  the client as registered, and only a character a header value cannot carry as written (a non-ASCII character, RFC
  3987 §3.1, or an ASCII control character or space) is percent-encoded in the `Location` header. The token endpoint
  compares `redirect_uri` ordinally with the authorization request's value (RFC 6749 §4.1.3) and refuses any other with
  `invalid_grant` ([ADR-0203](docs/decisions/0203-redirect-uris-are-matched-exactly.md), issue [#243](https://github.com/NCodeGroup/Identity/issues/243)).

- **The FAPI profiles refuse an `http` redirect URI.** `fapi1-advanced` caps `redirect_uri_kinds_supported` at
  `https` (FAPI 1.0 Part 1 §5.2.2-20 and §7.5), and `fapi2` at `https`, `loopback`, and `private_use` (FAPI 2.0
  §5.3.2.2-8), so `http` is left only for a native client's loopback redirect under `fapi2`. Dynamic client
  registration refuses a redirect URI the tenant's settings exclude with `invalid_redirect_uri`, the management API
  refuses one the client's effective settings exclude with a validation problem on `redirect_uris`, and the
  conformance seed stops the host. The authorization and PAR endpoints refuse such a redirect URI with
  `invalid_request` and a `400` that is not redirected
  ([ADR-0203](docs/decisions/0203-redirect-uris-are-matched-exactly.md),
  [ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md), issue [#244](https://github.com/NCodeGroup/Identity/issues/244)).

- **A redirect URI is matched exactly, and only a registered loopback URI may vary its port.** The requested
  `redirect_uri` is compared ordinally, as written, to the client's registered strings, and more than one value is
  refused (RFC 9700 §4.1.3, OAuth 2.1). It used to ignore the query and to accept any loopback URI the client had not
  registered, and the response writer passed on the query parameters the request supplied; the query that reaches the
  client is now the registered one (RFC 6749 §3.1.2). A client must register the exact URIs it sends.
  `allow_loopback_redirect` keeps its name and its default of `true`, but its meaning narrows to RFC 8252 §7.3: a
  registered `http://127.0.0.1` or `http://[::1]` URI may be requested with another port, and `localhost` gets no
  flexibility. The `fapi1-advanced` profile sets a ceiling of `false` for it (FAPI 1.0 Part 1 §5.2.2-20), so a client
  under it registers the exact port; `fapi2` sets none. An unregistered URI is still refused with
  `unauthorized_client` and no redirect
  ([ADR-0203](docs/decisions/0203-redirect-uris-are-matched-exactly.md),
  [ADR-0202](docs/decisions/0202-fapi-1-0-advanced-is-a-second-registered-security-profile.md), issue [#202](https://github.com/NCodeGroup/Identity/issues/202)).

- **A retired tenant identifier or issuer is never reissued.** Deleting a tenant retires its identifier and its issuer,
  and changing a tenant's issuer retires the previous one, so a new tenant can no longer take an identity that relying
  parties trust. The tenant store records them in the new `RetiredTenants` and `RetiredIssuers` tables, in the same
  unit of work as the delete or update. `ITenantResolver.ValidateTenantAsync` reports a retired identifier, or an issuer
  another tenant gave up, as a conflict: the management API answers `409 Conflict` on create and update, and the
  address preview lists it. Only the tenant that gave up an issuer can take it back. `ITenantStore` adds
  `IsTenantIdRetiredAsync` and `GetRetiredIssuerHolderOrDefaultAsync`. The host database's
  `RetireTenantIdentifiersAndIssuers` migration adds the two tables
  ([ADR-0189](docs/decisions/0189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md)).
- **A signed request object is short-lived and addressed to the issuer.** The authorization endpoint (`request` /
  `request_uri`) and the CIBA backchannel endpoint refuse a request object valid for longer than the new client-scoped
  `request_object_max_lifetime` setting, which defaults to 60 minutes as a `Min` ceiling. The authorization endpoint
  now always validates `aud`, against the tenant's issuer unless `request_object_expected_audience` is set, so a
  client that omitted `aud` must add it. The new `request_object_nbf_required` setting (`Or` merge, default `false`)
  makes `nbf` mandatory. The FAPI 2.0 profile pins both, at 60 minutes and `true`. A CIBA signed authentication request
  must carry `iat`, `nbf`, `exp`, and `jti` (CIBA Core §7.1.1), and the CIBA loader now uses the client's
  `clock_skew` instead of the JWT library's 5-minute default. `TokenLifetimeRequirement` and
  `ValidateTokenLifeTimeOptions` gain `RequireNotBefore` and `RequireIssuedAt`. See
  [ADR-0187](docs/decisions/0187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md).

- **The frontend bootstrap document's issuer is the serving tenant's, never the request's.** With `Frontend:Issuer`
  unset, `/config/bootstrap.json` published the request's `scheme://host` as the issuer, so a forged or proxied
  `Host` sent the bundle to an authority the operator never configured. It now publishes the stored issuer of the
  tenant that serves the surface: the `StaticSingle` tenant, or the root tenant under the dynamic strategies. Before
  that tenant is provisioned, it publishes its strategy's default issuer. When neither exists, the endpoint answers
  `500` rather than guess. The API base still defaults to the serving origin, which is where the management API is,
  and the response is `Cache-Control: no-store`. `Frontend:Issuer` and `Frontend:ApiBaseUrl` remain overrides.
  Issue [#113](https://github.com/NCodeGroup/Identity/issues/113) is complete
  ([ADR-0182](docs/decisions/0182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md)).

- **The Data Protection key ring is encrypted at rest, and a Production host refuses an unprotected one.** The host
  persists the key ring to the same database as the secrets it unprotects, so a database dump held both. In
  Production, `AddProductionSafety` now refuses to start, as the host starts, when the key ring has no repository or
  no `IXmlEncryptor`. Any encryptor satisfies it: a certificate, `ProtectKeysWithAzureKeyVault` or another key
  management service, or a custom one. `AddConfiguration` binds the new `DataProtection:KeyProtection` section
  (`KeyRingProtectionOptions`). A `Certificate` there, either PEM with a separate (optionally encrypted) private key or
  PKCS#12, with an RSA key, protects every new key. `PreviousCertificates` keep older keys readable through a rotation.
  **Breaking:** a Production deployment without key ring protection no longer starts.
  ([ADR-0177](docs/decisions/0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md))

- **`tls_client_auth` validates the presented certificate's chain in the server and fails closed.** The handler
  matched only the subject and trusted the TLS terminator to have validated the chain, so a proxy that only requested
  a certificate let a self-signed certificate carrying a client's registered subject authenticate as that client. The
  chain is now built against the tenant- or client-scoped `tls_client_auth_trust_anchors` (PEM CA certificates; a
  self-issued one is an anchor, any other an intermediate) with custom root trust, the forwarded intermediates as
  untrusted hints, the `clientAuth` extended key usage, validity, and `tls_client_auth_revocation_mode` (`online` by
  default, `offline`, or `none`). The replaceable `IClientCertificateChainValidator` performs the check, and a failure
  is audited as `client_certificate_untrusted`. **Breaking:** a `tls_client_auth` client with no trust anchors no
  longer authenticates, and a PKI that publishes no revocation data must set `tls_client_auth_revocation_mode` to
  `none`. ([ADR-0173](docs/decisions/0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md))

- **Behind a proxy, a forwarded certificate that cannot be decoded fails the request, and the connection's certificate
  is never taken for the client's.** An undecodable forwarded value was read as no certificate, after which the resolver
  fell back to the certificate on the connection — at most the proxy's own on a re-encrypting hop. When a forwarded
  header is configured it is now the only source: an absent header is no certificate, and a value the configured format
  cannot decode is `invalid_request` with a warning naming the header and the format.
  ([ADR-0172](docs/decisions/0172-forwarded-client-certificates-are-decoded-by-a-registered-format.md))

- **Any authenticated client could introspect any token.** A client could introspect another client's refresh token or
  any access token, including its own encrypted ones, and read what encryption hides from it. Introspection now
  reports an access token only to a client acting for a resource server in the token's audience, narrowed to that
  resource server, and a refresh token only to its own client (RFC 7662 §4, RFC 9701 §5). A client bound to a resource
  server is accepted at no other endpoint, and every other endpoint refuses it unless it opts in.
  ([ADR-0168](docs/decisions/0168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md))

- **The server's readers of its own tokens checked neither the token type nor an algorithm allow-list.** Introspection
  reported an ID token as an active access token and ignored a revoked session. Token exchange accepted a logout token
  or any other JWT the tenant signed as a `jwt` subject token. No reader limited the algorithms it accepted to those the
  tenant permits. Every reader now enforces the type, the tenant's allow-lists, and, for access tokens, session
  revocation.
  ([ADR-0167](docs/decisions/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md))

- **No published URL follows the request's `Host` header any more.** A tenant without an issuer published its
  discovery endpoints, `registration_client_uri`, and device `verification_uri` under the origin of whatever request
  materialized it, so a forged `Host` could point a client's credentials elsewhere. Every served tenant now has an
  issuer, and a tenant stored without one fails closed.
  ([ADR-0165](docs/decisions/0165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md))

- **The bootstrap administrator, the development administrator, and the conformance clients are seeded into one named
  tenant only.** With no tenant configured they were seeded into every workload tenant, which, now that tenants created
  at runtime are seeded, would have given each new tenant a client whose tokens carry `GlobalAdmin` or accounts with
  well-known passwords. **Breaking:** `BootstrapAdmin:TenantId`, `DeveloperInteractiveSeed:TenantId`, and
  `ConformanceSeed:TenantId` are required whenever their seeder has something to seed, and the host does not start
  without them; the named tenant may be the root tenant. `GlobalAdmin` is conferred only on the bootstrap client in its
  tenant, not on a client that shares its identifier elsewhere.
  ([ADR-0166](docs/decisions/0166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md))

- **A DPoP proof or client assertion could be accepted twice.** The replay stores checked for a record and then wrote
  one, so two concurrent presentations of the same proof or `private_key_jwt` / `client_secret_jwt` assertion could
  both pass. Each replica also kept its own records, so a replay sent to a different replica was accepted. Recording is
  now one atomic add-if-absent. With Valkey configured, it holds across every replica.
  ([ADR-0164](docs/decisions/0164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md))

- **A client's key set could carry private or symmetric key material, and an inconsistent key was accepted.**
  Registration stored the `jwks` document verbatim and echoed it back, so a key the import skipped — an `oct` key's `k`
  or an `OKP` key's `d` — was kept in plaintext settings, and the management API stored any `jwks` it was given. A key
  carrying any private or symmetric member (`d`, `p`, `q`, `dp`, `dq`, `qi`, `oth`, `k`, `priv`) is now refused at
  registration, at the management import, and by every key-type descriptor, and a `jwks_uri` set that publishes one is
  refused as a whole. A key's `use`, `key_ops`, and `alg` must now agree with each other and with the key at every
  entry point, including the management update of a client key's metadata.
  ([ADR-0161](docs/decisions/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md))

- **A client's stored public keys stayed in force beside the keys at its `jwks_uri`.** The resolver merged both, so a
  key the client had rotated out at its `jwks_uri` still verified its signatures. A `jwks_uri` now replaces the stored
  public keys, and a `jwks_uri` that cannot be fetched or whose set is refused leaves the client with no public keys.
  ([ADR-0161](docs/decisions/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md))

- **A client's access-token encryption bounds stopped applying once its token targeted a resource server.** A single
  targeted resource server's settings governed the token's encryption in place of the client's, so a client — or a
  security profile it selects — that required encryption or narrowed the encryption algorithms was ignored, and the
  token was issued unencrypted or outside the client's algorithms. The client's and every targeted resource server's
  settings now combine, so both bind.
  ([ADR-0158](docs/decisions/0158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md))

- **The JWT validator accepted an unsigned (`alg:none`) token as authentic.** The `none` signature algorithm verified
  with any key, so a token whose header named `none` and whose signature was empty validated against any key
  collection: a forged access token authenticated at UserInfo, introspection, token exchange, and the management API.
  The validator now rejects an unsigned token at every level, with no option to accept one, and the `none` algorithm
  verifies only with an empty key.
  ([ADR-0154](docs/decisions/0154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md))

- **Request objects, CIBA requests, and client assertions are accepted only with the algorithms the client's settings
  permit.** A request object's `alg` was checked only against `none`, so a narrower client list — or the FAPI 2.0
  profile's ceiling — did not bind. A request object is now verified under the client's effective
  `request_object_signing_alg_values_supported` and decrypted (with the tenant's keys) under its
  `request_object_encryption_{alg,enc}_values_supported`; a CIBA request under its asymmetric-only
  `backchannel_authentication_request_signing_alg_values_supported`, with an encrypted one rejected; and a client
  assertion under its `token_endpoint_auth_signing_alg_values_supported`. At these trust boundaries an unset list
  permits nothing instead of every registered algorithm.
  ([ADR-0155](docs/decisions/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md))

- **The UserInfo endpoint requires an access token.** It accepted the end-user's session cookie in place of an access
  token, which made a protected resource answer an ambient credential. It now authenticates only a bearer access token
  (in the `Authorization` header or the form body) whose audience includes the OpenID resource server, issued to an
  enabled client; anything else is `401` `invalid_token`.
  ([ADR-0155](docs/decisions/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md))
- **Text is encoded and decoded only through strict encodings.** The compact `b64=false` JWS signature now covers the
  payload's real UTF-8 bytes (previously non-ASCII characters were signed as `?`). DPoP proof validation rejects a
  non-ASCII access token instead of hashing it as `?`. `request_uri` request objects and other inbound text are decoded
  strictly. JWT UserInfo and introspection responses are written as strict UTF-8 bytes and keep
  `Content-Type: application/jwt`, and `text/plain` responses now declare `charset=utf-8`. The build bans lenient
  encodings with `RS0030` in every project.
  ([ADR-0266](docs/decisions/0266-text-is-encoded-and-decoded-only-through-strict-encodings.md))

### Fixed

- **A JSON Web Key Set skips a key with an empty `kty`.** `JsonWebKeySet(JsonElement)` and `JsonWebKeySet.Parse`
  failed the whole set with `ArgumentException` when one key's `kty` was the empty string; such a key is skipped like
  one with a missing or non-string `kty` (issue [#329](https://github.com/NCodeGroup/Identity/issues/329)).

- **The protocol client fails an ID token with an out-of-range `auth_time` instead of throwing.** With `max_age`, an
  `auth_time` such as `1e300` or one near the end of time threw `ArgumentOutOfRangeException`; `auth_time` and the
  logout token's `exp` are read as strict NumericDates, so the token fails with `invalid_claim`
  ([ADR-0123](docs/decisions/0123-numeric-date-is-read-strictly-and-range-guarded.md), issue [#367](https://github.com/NCodeGroup/Identity/issues/367)).

- **The protocol client accepts an `expires_in` written with a zero fraction.** A pushed authorization response or a
  token response whose `expires_in` was a whole number written as `60.0` or `6e1` failed with
  `OpenIdProviderException`; the client reads it as 60 seconds and still rejects a non-zero fraction such as `60.5`.
  `NCode.Json` adds `JsonElement.TryGetWholeInt32`, `TryGetPropertyWholeInt32` and `WholeInt32JsonConverter` for such
  values, and `NCode.Identity.OpenId.Client.Abstractions` references `NCode.Json`
  ([ADR-0277](docs/decisions/0277-a-whole-number-json-value-is-accepted-with-a-zero-fraction.md), issue [#352](https://github.com/NCodeGroup/Identity/issues/352)).

- **An `AKP` JWK thumbprint covers `alg`.** The thumbprint was computed over `kty` and `pub` only; RFC 9964 §6 also
  requires `alg`, so two `AKP` keys that differ only in algorithm no longer share a thumbprint
  ([ADR-0271](docs/decisions/0271-a-key-type-registers-its-json-web-key-rules.md)).

- **`client_secret_basic` accepts a secret that contains `%` or `+`.** The handler unescaped the form-urlencoded
  secret over a pooled buffer larger than the secret, so any secret with an escape or a `+` failed with `401`
  `invalid_client`; it now decodes the client id and secret to exactly what the client sent, as RFC 6749 §2.3.1
  form-urlencoding defines (issue [#319](https://github.com/NCodeGroup/Identity/issues/319)).

- **A client assertion whose header or payload is not a JSON object is answered with `400` `invalid_client`.** A
  `client_assertion` whose header or payload decoded to a JSON array, string, or number raised an unhandled exception
  and a `500`; the JWT-bearer handlers now treat it like any other malformed assertion.

- **The login page keeps what the end-user typed when the `apps/auth` bundle loads.** The bundle mounts as soon as
  its script runs and starts from the page the host rendered: the values typed or autofilled into its text and
  password fields, the field that had focus, and the consent scopes the end-user cleared carry into its forms, and a
  carried password is written to the input's `value` property, never to an attribute. The authorization error page
  served with the bundle shows its error instead of a login form
  ([ADR-0253](docs/decisions/0253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md), issue
  [#211](https://github.com/NCodeGroup/Identity/issues/211)).

- **A host's own ASP.NET Core Identity pages verify the password the adapter stores.**
  `AddAspNetIdentityLocalAccountStore<TUser>()` wraps the host's `IPasswordHasher<TUser>` registration, made before or
  after the call, in the public `NormalizingPasswordHasher<TUser>`, which hashes and verifies a password's NFC form and
  verifies the typed form as a fallback that asks for a rehash, so the adapter and the host's pages accept every
  composition of a password. **Breaking:** a host that registers its `IPasswordHasher<TUser>` after the call, or
  replaces `UserManager<TUser>.PasswordHasher`, wraps it in `NormalizingPasswordHasher<TUser>`, because the adapter
  throws an `InvalidOperationException` instead of hashing or verifying through a hasher that does not normalize
  ([ADR-0243](docs/decisions/0243-every-aspnet-core-identity-path-hashes-and-verifies-the-nfc-form-of-a-password.md), issue
  [#299](https://github.com/NCodeGroup/Identity/issues/299)).

- **A management request answers `409` for a stored settings document it cannot honor.** The effective-settings reads,
  a client's create and settings update, the incoherent-client report, a local account's create and password reset,
  and a tenant settings write whose server document cannot be honored answer `409 Conflict` with a problem whose
  `settingsScope`, `ownerId` and `documentError` name the document, only after authorization, and log
  `StoredSettingsUnreadable` (event ID 3009); a settings write that replaces the document repairs it. **Breaking:**
  `IEffectiveSettingsResolver` gains `ResolveStored`, and it and `ResolveServerAsync(CancellationToken)` throw
  `UnreadableSettingsException`, a `JsonException` that names the document
  ([ADR-0245](docs/decisions/0245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md),
  issue [#301](https://github.com/NCodeGroup/Identity/issues/301)).

- **A settings write passes a stored document that cannot be honored.** A server or tenant settings write does not
  fail with `500` when a stored client's or tenant's settings document names a setting, a value or a scope the settings
  vocabulary rejects: the write logs the document (`ClientSettingsUnreadable`, `TenantSettingsUnreadable`, event IDs
  3006 and 3007) and does not count it as a violation the change introduces, and a write that replaces such a document
  of its own checks no client and logs it (`ServerSettingsUnreadable`, 3008, or `TenantSettingsUnreadable`), so the
  management API can repair it; the incoherent-client report still lists the client
  ([ADR-0241](docs/decisions/0241-a-settings-write-is-not-refused-for-a-stored-document-it-cannot-read.md), issue [#295](https://github.com/NCodeGroup/Identity/issues/295)).

- **The ASP.NET Core Identity adapter stores a password the password policy accepts.** It hashes a password with the
  user manager's password hasher and stores the hash through `IUserPasswordStore<TUser>`, creating the user with
  `CreateAsync(user)` and rotating the security stamp on a reset, so ASP.NET Core Identity's password validators, whose
  default composition rules NIST SP 800-63B forbids, cannot refuse a password the policy accepted with `500`; a
  transparent rehash keeps the security stamp
  ([ADR-0238](docs/decisions/0238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md), issue [#296](https://github.com/NCodeGroup/Identity/issues/296)).

- **A dispatched job whose handler times out is retried, and the host keeps running.** A handler's
  `OperationCanceledException` that does not come from the host stopping, such as the `TaskCanceledException` of an
  `HttpClient` timeout during a back-channel logout or CIBA delivery, fails the attempt and retries the job with
  back-off; it does not end the dispatcher's worker or stop the host. A job whose handlers fail is retried once per
  attempt, however many of them failed, and a handler that cannot be resolved fails the attempt
  ([ADR-0232](docs/decisions/0232-the-in-process-dispatcher-runs-jobs-concurrently-up-to-a-limit.md), issue [#276](https://github.com/NCodeGroup/Identity/issues/276)).

- **A replayed authorization code terminates its session through the termination seam.** A second redemption of a
  redeemed code terminates the code's session with `authorization_code_replay`, so its grants are revoked, its relying
  parties receive back-channel logout and the termination is audited; a code that expired unused terminates nothing.
  **Breaking:** `SessionTerminationReasons.AuthorizationCodeReplay` is added
  ([ADR-0228](docs/decisions/0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md),
  issue [#287](https://github.com/NCodeGroup/Identity/issues/287)).

- **A terminated session notifies every client it lists when it is claimed.**
  `IPersistedGrantService.RevokeSessionOnceAsync` returns the session grant as the call revoked it, and every
  termination notifies the clients that grant lists, so a client that an ID token issued under the session added after
  an earlier read receives back-channel logout. A subject-wide termination also claims a session that ended and awaits
  the background termination. **Breaking:** `RevokeSessionOnceAsync<TPayload>` returns `PersistedGrant<TPayload>?`
  ([ADR-0228](docs/decisions/0228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md),
  issue [#288](https://github.com/NCodeGroup/Identity/issues/288)).

- **A ping or push client is told that its CIBA request was denied.** A denial, also of an approval whose subject is
  refused, sends the ping notification, after which the token endpoint answers `access_denied`, or the push error
  payload with `error=access_denied`; only the call that decided the request notifies the client
  ([ADR-0231](docs/decisions/0231-a-ping-or-push-client-is-told-when-its-ciba-request-is-denied.md), issue [#291](https://github.com/NCodeGroup/Identity/issues/291)).

- **A session is terminated at most once on every path.** `ISessionService.RevokeAsync`, `RevokeAllAsync` and
  `RevokeAllExceptAsync`, the eviction at the concurrent-session limit and RP-initiated logout claim each session with
  `IPersistedGrantService.RevokeSessionOnceAsync` before they dispatch its termination, so a session that two paths end
  at once notifies its relying parties and is audited once, and the claim revokes the grants issued under it in one unit
  of work. `RevokeAllAsync` revokes every grant of the subject before it dispatches, and the sessions management API
  reports the sessions the request terminated. **Breaking:** `ISessionService.RevokeAsync` returns `bool`, and
  `RevokeAllAsync` and `RevokeAllExceptAsync` return the number of sessions the call terminated
  ([ADR-0227](docs/decisions/0227-a-session-is-terminated-at-most-once-on-every-path.md), issue [#286](https://github.com/NCodeGroup/Identity/issues/286)).

- **A change to a grant's payload loses no concurrent write.** The touch of a session, the addition of a client to it,
  and the polls and decisions of the device-code and CIBA grants apply their change to the grant as stored and read
  again when another write lands first, so a client that a concurrent request added to a session stays, a poll leaves an
  approval that landed before its write, and only the CIBA approval that decided the request delivers. **Breaking:**
  `IPersistedGrantService.UpdatePayloadAndExpirationAsync` and the overload of `UpdatePayloadAsync` that takes a payload
  are removed, `UpdatePayloadAsync` takes the function that computes the change, `PersistedGrantUpdate<TPayload>` is
  added, and `ISessionService.AddClientAsync` reports whether this call added the client
  ([ADR-0222](docs/decisions/0222-a-grants-payload-is-changed-by-a-read-modify-write.md), issue [#282](https://github.com/NCodeGroup/Identity/issues/282)).

- **Listing a subject's sessions failed for every stored session.** The listing deserialized the versioned payload
  envelope of a stored session as the payload, so the active-sessions page, the management sessions API, sign-out
  everywhere and the concurrent-session limit failed for any subject with a session. The payload is read through
  `IPersistedPayloadSerializer`
  ([ADR-0220](docs/decisions/0220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md), issue [#271](https://github.com/NCodeGroup/Identity/issues/271)).

- **Deleting a local account left its principal's role assignments, ownership included.** Removing a federated
  principal removes every role assignment it holds, in every tenant, in the same unit of work, and deleting a tenant
  removes the assignments its principals hold in other tenants
  ([ADR-0212](docs/decisions/0212-deleting-an-owner-removes-what-it-owns.md), issue [#273](https://github.com/NCodeGroup/Identity/issues/273)).

- **A revocation wins a concurrency race.** The revocation endpoint, the device user code, logout and session eviction,
  the replay of an authorization code, and the management revocations read the grants again in a new unit of work and
  retry when another write changed a grant first, so a concurrent write such as a sliding-expiration update does not
  leave a grant active or reach the caller as `500` or `412`. **Breaking:** `IPersistedGrantService.RevokeWhereAsync`
  replaces `RevokeBySessionAsync`, `GrantFilter.GrantId` is added, and
  `DefaultRevokeGrantsOnSessionTerminatedHandler` revokes through `IPersistedGrantService`
  ([ADR-0215](docs/decisions/0215-a-revocation-wins-a-concurrency-race.md), issue [#269](https://github.com/NCodeGroup/Identity/issues/269)).

- **Deleting a client or a resource server removes the role assignments at its node.** The assignments of every role,
  ownership included, are removed in the same unit of work, also when a dynamically registered client is deregistered.
  The built-in `ResourceNodeTypes` values are the `OpenIdResourceTypes` values, so a store that removes a resource finds
  the assignments at its node
  ([ADR-0212](docs/decisions/0212-deleting-an-owner-removes-what-it-owns.md), issue [#266](https://github.com/NCodeGroup/Identity/issues/266)).

- **Deleting a client that held grants, consents or resource-server grants, deregistering a dynamically registered
  client, or deleting a tenant whose users had signed in failed with a foreign-key violation (`500` on PostgreSQL).**
  The removal takes what exists for the owner in the same unit of work
  ([ADR-0212](docs/decisions/0212-deleting-an-owner-removes-what-it-owns.md), issue [#250](https://github.com/NCodeGroup/Identity/issues/250)).

- **A management write that lost a concurrency race reached its client as `500`;** it is answered
  `412 Precondition Failed`
  ([ADR-0214](docs/decisions/0214-a-management-write-that-loses-a-race-is-answered-412.md), issue [#261](https://github.com/NCodeGroup/Identity/issues/261)).

- **The usage ledger was never pruned on EF InMemory, and the failure was logged at every flush.** The prune removes
  bounded batches through the change tracker, which every provider supports
  ([ADR-0213](docs/decisions/0213-the-usage-ledger-is-pruned-in-bounded-batches.md), issue [#264](https://github.com/NCodeGroup/Identity/issues/264)).

- **A refresh that extended a sliding expiration at the same moment as another could reach its client as `500`.** The
  update keeps the other request's change
  ([ADR-0211](docs/decisions/0211-a-rotated-refresh-token-is-spent-with-its-successor.md), issue [#260](https://github.com/NCodeGroup/Identity/issues/260)).

- **A usage batch whose save committed but reported a failure was added to the ledger twice.** A save can commit and
  still fail at its caller, for example when the connection drops after the commit, and the flush then added the same
  batch again, so a tenant's billed usage was overcounted. A replica numbers its batches, and the ledger skips a batch
  that its replica already added
  ([ADR-0209](docs/decisions/0209-a-replica-adds-each-usage-batch-to-the-ledger-once.md), issue [#216](https://github.com/NCodeGroup/Identity/issues/216)).

- **A host stopped twice at once could dispose its services under the usage ledger's final flush.** The stop that
  returned first let its caller dispose the service provider while the other stop's flush was still adding usage, and
  the flush failed. The final flush runs once and every stop waits for it, the usage it cannot add is logged as
  discarded (event 2051), and the stop does not throw
  ([ADR-0209](docs/decisions/0209-a-replica-adds-each-usage-batch-to-the-ledger-once.md), issue [#245](https://github.com/NCodeGroup/Identity/issues/245)).

- **A value that a client registers in a list, and that its tenant or a security profile excludes, was accepted and
  failed at the client's next request.** The effective list omitted the value silently, and nothing compared it with
  what the client registered. `RegisteredValueCoherenceRule` reports it, so the management API and the seeders refuse
  such a client, and a tenant or server change that would drop such a value is refused while a client registers it
  ([ADR-0206](docs/decisions/0206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue [#253](https://github.com/NCodeGroup/Identity/issues/253)).

- **A client that alone selected `fapi1-advanced` replaced its tenant's certificate-binding support.** The profile's
  default for `tls_client_certificate_bound_access_tokens` applied at every scope that selected the profile, including a
  client, where the setting does not apply. A default applies only within its scope, so the client takes its tenant's
  support, and `CertificateBoundAccessTokenCoherenceRule` reports the client while that support is off
  ([ADR-0148](docs/decisions/0148-security-profiles-accumulate-down-the-settings-cascade.md), issue [#259](https://github.com/NCodeGroup/Identity/issues/259)).

- **A concurrent second spend of a single-use grant reached its client as `500`.** Of two requests that spent one
  authorization code, device code, CIBA request, pushed authorization request, initial access token, or `state` of a
  paused authorization flow at the same moment, the one that lost the race on the grant row's concurrency token failed
  with an unhandled exception. `ConsumeOnceOrDefault` answers it as a second spend: `invalid_grant` for an authorization
  code, a device code, and a CIBA request, `invalid_request_uri` for a pushed request, `401` `invalid_token` for an
  initial access token, and `400` for the `state` of a paused authorization flow at the continue endpoint
  ([ADR-0208](docs/decisions/0208-a-lost-concurrency-race-surfaces-as-a-store-neutral-exception.md), issue [#257](https://github.com/NCodeGroup/Identity/issues/257)).

- **A `tls_client_auth` client whose certificate subject was encoded differently from its registered subject DN could
  not authenticate.** The handler compared DER bytes, so the same name written as a `UTF8String` or a
  `PrintableString`, in another case, or with other spacing did not match. The subject DN matches across string
  encodings, case, and spacing, and the `tls_client_auth_subject_dn` setting rule accepts the RFC 4514 forms an
  operator pastes. **Breaking:** a stored `tls_client_auth_subject_dn` written with `;` separators or in the
  `/C=US/O=Example` form does not parse, matches no certificate, and is refused by the setting rule; rewrite it as an
  RFC 4514 string such as `CN=client,O=Example`
  ([ADR-0173](docs/decisions/0173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md), issue [#209](https://github.com/NCodeGroup/Identity/issues/209)).

- **A security profile selected in the server's configuration supplied no defaults at the server root.** The root wrote
  every setting's descriptor default into the collection that holds the configuration, and a profile's default sits
  beneath a scope's own settings, so it never took effect there. The root's descriptor defaults, computed supported
  values, and default-settings providers are a baseline beneath the configuration, and
  `ISecurityProfileCatalog.Expand(scopeSettings, baselineSettings)` layers them: the configuration replaces the
  baseline, a profile's default replaces the baseline but never a configured value, and a ceiling bounds both. So
  `fapi1-advanced` advertises certificate-bound access token support, and the FAPI profiles' `PS256` signed-response
  defaults apply, when the server's configuration selects the profile
  ([ADR-0148](docs/decisions/0148-security-profiles-accumulate-down-the-settings-cascade.md), issue [#233](https://github.com/NCodeGroup/Identity/issues/233)).

- **A server settings PATCH loaded every tenant and client with their secrets, even for an unrelated change.** It now
  walks the clients only when a setting that the client response-algorithm rule reads changed, and it reads tenants and
  clients as untracked projections without their secrets (issue [#212](https://github.com/NCodeGroup/Identity/issues/212)).

- **A management write evicted the tenant only on its own replica.** Every other replica kept serving its cached tenant,
  disabled, deleted, or moved, until the entry expired. The write now announces the change, and every replica evicts the
  tenant ([ADR-0205](docs/decisions/0205-replicas-announce-changes-through-a-broadcast-port.md), issue [#240](https://github.com/NCodeGroup/Identity/issues/240)).

- **A server settings change that permitted a configured algorithm left running tenants without its key.** A
  background job now re-seeds each tenant that newly names the algorithm, so the key is provisioned without a restart
  ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md), issue [#241](https://github.com/NCodeGroup/Identity/issues/241)).

- **A tenant with provisioned keys could not be deleted.** The delete answered `409` because the tenant's own secrets
  and system resource servers blocked it
  ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md), issue [#242](https://github.com/NCodeGroup/Identity/issues/242)).

- **`response_modes_supported` was checked inconsistently.** When the setting is set, the authorization endpoint now
  requires both the response mode as the request wrote it, so `jwt` itself must be listed, and the mode it resolves to
  to be listed, for an explicit and a default mode alike. The check runs once, when the delivery is derived, and the
  request validator no longer repeats it. A response mode that is not listed is refused with `invalid_request` and a
  `400`, and is not redirected
  ([ADR-0201](docs/decisions/0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issue
  [#213](https://github.com/NCodeGroup/Identity/issues/213)).

- **A JWT-secured (JARM) response the server could not sign was refused only after the user had signed in.** The
  credentials that sign the response are now selected when the delivery is derived, before authentication and before a
  code is issued. A client with no signing credential, or a selector that fails, is refused with `server_error` and a
  `500` that is not redirected, instead of failing when the response was written
  ([ADR-0201](docs/decisions/0201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issue
  [#214](https://github.com/NCodeGroup/Identity/issues/214)).

- **A tenant whose settings later permitted a configured algorithm advertised it without a key until every replica
  restarted.** Every management write to a tenant (its record, its settings, a secret, or its deletion) now sends a
  `TenantChangedCommand` after it commits. The default handler evicts the tenant from the replica's tenant cache, so the
  change takes effect there at once, and after a settings change seeds the tenant again, so the key is provisioned
  before the write returns. `Ephemeral` developer mode now keeps one key per tenant for the life of the process, so the
  eviction does not invalidate issued tokens. **Breaking:** `IOpenIdTenantCache` gains `RemoveAsync`.
  ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md))
- **A tenant that signs JWT-secured authorization responses (JARM) with an algorithm none of its other lists names
  was provisioned no key for it.** `OpenId:Tenant:KeyProvisioning:AlgorithmSettings` now names
  `authorization_signing_alg_values_supported` by default, so the JARM list provisions its keys as the ID token and
  access token lists do (ADR-0169).

- **An unreachable Valkey server cost every request about 5 s per rate limit judged, and logged a warning with a
  stack trace each time.** The rate limiter now bounds each counter call to 100 ms and, after a failure, stops calling
  the store for 10 s, admitting every request uncounted (`store_unavailable`) before trying it again. The failure that
  opens the pause logs one warning and the answer that ends it logs one information message (new event
  `RateLimitStoreRecovered`), so an outage is logged once per replica. Valkey stays a readiness dependency, since
  single-use state fails closed without it (ADR-0196).

- **A client's `request_object_expected_audience` replaced the issuer as the audience a request object must name,
  even under a FAPI profile.** The setting is client-scoped and replace-merged, so no ceiling could cap it. The
  `fapi1-advanced` and `fapi2` profiles now compose `IssuerOnlyRequestObjectAudienceRequirement`, which refuses a
  request object from a client that sets it with `invalid_request_object` (ADR-0187, ADR-0202).

- **An authorization request with a request object still read `scope` and `response_type` from the unsigned outer
  query when the object omitted them.** Only the object's values are now used (RFC 9101 §6.3, FAPI 1.0 Advanced
  §5.2.2-10): an object without `response_type` or `scope` is refused with `invalid_request_object`, an outer
  `response_type` must still match the object's, and an outer `scope` is ignored (ADR-0202).

- **An authorization error raised after the request object was verified used the outer, unsigned `state` and
  `response_mode`.** When a request carried `redirect_uri` outside its request object, a later error went back without
  the object's `state` and, for `response_mode=jwt` only in the object, unsigned (RFC 6749 §4.1.2.1). Once the object
  is verified, the authorization endpoint now takes `state` and `response_mode` from it for every later response,
  errors included (RFC 9101 §6.3, ADR-0202).

- **Every management API request read the control-plane tenant from the store before checking the tenant cache.**
  `ITenantStrategy` and `ITenantResolver` gain `GetControlPlaneTenantId()`, which names the control-plane tenant from
  configuration, and `CreateControlPlaneTenantAsync` probes the cache with it first, reading the store only on a miss
  (ADR-0191).

- **The `fapi1-advanced` security profile accepted an authorization request without `nonce` or `state`.** It now
  refuses, with `invalid_request`, a request for the `openid` scope without `nonce` and any other request without
  `state` (FAPI 1.0 Advanced §5.2.2.2–3, ADR-0202).

- **Anonymous requests for discovery and the JSON Web Key Set spent a tenant's daily request quota.** Anyone could
  spend the quota, run up its billable overage, and then have the tenant's clients refused until midnight UTC. The two
  endpoints are no longer metered: they count only against the per-minute rate limits, and are not recorded as
  `requests` usage (ADR-0198, ADR-0199, ADR-0200).

- **Requests refused by the per-client rate limit or the token quota were recorded as usage, including overage.** The
  request filter now records a request once its endpoint has run, and skips one that any limit refused with `429`, so
  only served requests are billed (ADR-0199 §2).

- **Pruning the tenant usage ledger loaded every expired entry into memory.** `ITenantUsageStore.RemoveBeforeAsync`
  now removes them with one set-based delete, which takes effect immediately rather than when the unit of work is
  saved.

- **A JWT signed with a certificate key was checked against the certificate's validity in the host's local time.**
  The certificate-lifetime check compared the certificate's local-time validity with the UTC clock, so on a host
  outside UTC a client assertion, request object, or CIBA request signed with an expired certificate could still pass,
  or a current one fail. Validity is now compared in UTC. A client key registered or published with an `x5c`
  certificate is now subject to this check, which ADR-0161 records.

- **Dynamic registration accepted a response-encryption `alg` whose default `enc` the tenant does not permit.** A
  client registering `id_token_encrypted_response_alg`, `userinfo_encrypted_response_alg`,
  `introspection_encrypted_response_alg`, or `authorization_encrypted_response_alg` without the matching `enc` gets the
  default `enc` (the tenant's, normally `A128CBC-HS256`). Registration now refuses it with `invalid_client_metadata`
  when the tenant's supported `enc` values exclude that default, rather than registering a client whose responses
  cannot be encrypted.

- **The host chart's migration Job ignored the host's extra settings and volumes.** `extraEnv`, `extraEnvFrom`,
  `extraVolumes`, and `extraVolumeMounts` reached only the host's pods, so a setup whose database connection needs a
  CA bundle or SSL setting failed every install at the migration Job. The Job now receives them too, and runs with
  the host's `fsGroup` so its non-root user can read a mounted secret. Both pod specs now share their security
  contexts, image pull secrets, and scheduling constraints through `_helpers.tpl`.

- **The host chart's migration Job name could exceed 63 characters.** A long release name made the `-migrations`
  suffix push the name past the label-value limit Kubernetes copies it into. The name is now shortened to fit.

- **A release without Valkey could briefly run two replicas.** The rolling update with `maxSurge: 0` started the new
  pod as soon as the old one began terminating. The chart now uses the `Recreate` strategy without Valkey, which waits
  for the old pod to terminate
  ([ADR-0179](docs/decisions/0179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)).

- **A tenant usage query near the first representable day returned `500`.** `GET /api/tenants/{tenantId}/usage` with
  a `to` less than 30 days after `0001-01-01` and no `from` threw while computing the default range. It now returns a
  `400` validation problem.

- **The host chart suggested a key-protection certificate that cert-manager renews in place.** A renewal written
  into the same Secret replaces the only certificate that can unwrap the existing Data Protection keys, so after the
  next restart the key ring could not be read. The chart's README now documents hand rotation through
  `keyProtection.previousCertificateSecrets` and warns against in-place renewal
  ([ADR-0177](docs/decisions/0177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md)).

- **Deleting the root tenant broke every tenant.** A deleted tenant identifier is never reissued, so the root tenant —
  or the static single-tenant strategy's tenant — could not be created again, and every request that needed it failed.
  `DELETE /api/tenants/{tenantId}` now answers `409 Conflict` for a tenant the deployment requires. `ITenantStrategy`
  and `ITenantResolver` gain `IsRequiredTenant`, so a custom strategy can name the tenants it requires
  ([ADR-0189](docs/decisions/0189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md)).

- **The multi-architecture host image build failed in restore.** The `amd64` and `arm64` builds
  shared the NuGet cache mount concurrently, so one read a package the other was still extracting (`Could not find
  file '/root/.nuget/packages/…'`). The Dockerfile now locks the cache mount, so the two publishes take turns.

- **The management API authenticated callers against the tenant the request addressed.** Under `DynamicByPath` a
  collection route such as `POST /api/tenants` had no tenant to resolve, and `/api/tenants/{tenantId}/…` validated the
  caller's token with the target tenant rather than the tenant that issued it; under `DynamicByHost` the
  `manage.<apex>` host is no tenant's host, so every call returned `404`. The management API now authenticates every
  caller against the control-plane tenant — the root tenant, or a static strategy's own tenant — whatever host or path
  it is reached on, and the route's `tenantId` only scopes the data. `ITenantStrategy` and `ITenantResolver` gain
  `ResolveControlPlaneTenantAsync`, `IOpenIdTenantFactory` gains `CreateControlPlaneTenantAsync`, and
  `IOpenIdContextFactory` gains `CreateForControlPlaneAsync`
  ([ADR-0191](docs/decisions/0191-the-management-api-authenticates-against-the-control-plane-tenant.md)).

- **A `form_post` response delivered url-encoded values.** The `form_post` page url-encoded each hidden field's value,
  but the browser form-urlencodes the fields again when it submits the form, so a client received a `state` holding a
  space, `=`, `/`, or `+` as its escaped form (`%3D`, `+`, …) rather than the value it sent. Each value — and the form's
  action — is now HTML-encoded only, as the OAuth 2.0 Form Post Response Mode specification intends, and a parameter
  with several values posts one field per value.

- **A management console change refetched the caches of every other scope.** The Management API's cache tags shared
  their ids across the global scope and every tenant, so creating a client in one tenant also refetched the global
  client list and every other tenant's, and a secret or setting change refetched the same-named resource in other
  scopes. Every tag id now names the scope that owns the resource (`scopeTagId` in `@ncode/api`), so a mutation
  refetches only its own scope's caches, as [ADR-0064](docs/decisions/0064-frontend-presentation-platform.md) intends.

- **The server rejected the access tokens it encrypted.** A client or resource server that required encrypted access
  tokens received tokens that UserInfo, the management API, introspection, and token exchange could not read, because
  none of them named a decryption key. They now decrypt with the tenant's keys, and introspection also uses the calling
  resource server's own keys. ([ADR-0167](docs/decisions/0167-the-server-validates-the-tokens-it-issued-under-one-policy.md))

- **Concurrent first requests to a tenant failed one of them.** Two seeds of the same tenant — two replicas, or two
  requests that missed the tenant cache together — raced to commit, and the loser's request failed. A seed whose commit
  fails now runs once more in a fresh unit of work, and since seed contributors are idempotent it adds only what the
  other seed did not, so concurrent seeds converge on one set of keys and resource servers.
  ([ADR-0169](docs/decisions/0169-every-tenant-is-provisioned-with-persisted-keys.md))

- **Creating a tenant through the management API failed.** The creator's ownership role assignment looks its tenant up
  in the database, but it was written in the same unit of work as the tenant row, before that row was committed, so
  every create ended in an error. The tenant row is now committed first, then the ownership, and the response carries
  the tenant's concurrency token after seeding.

- **The management API could give a tenant an issuer nothing is served under.** Only the static tenant's configured
  issuer was checked, so a tenant created or updated through the management API could have an issuer with a query or
  fragment, a path tenant `acme` an issuer that does not end in `/acme`, or two host-routed tenants the same host — and
  conforming clients reject such a tenant's discovery document. Creating a tenant, and updating one's issuer, now
  validates its identifier and issuer through the tenant strategy that serves it: the issuer is an absolute `https` URL
  (`http` only for a loopback host) with no user information, query, or fragment, held by no other tenant, and ending
  with the tenant's route; a host-routed tenant needs an issuer whose host the configured host pattern maps to itself
  and no other tenant holds. An invalid value answers `400` and a conflict `409`. An empty issuer is stored as none,
  rather than as an empty string that collided under the issuer's unique index.
  ([ADR-0160](docs/decisions/0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md))

- **The management console could not edit a tenant, and dropped its issuer when creating one.** The console still sent
  the tenant's former `domainName` field, so an edit failed with a JSON Patch error and a create silently dropped the
  value. It sends and shows the tenant's `issuer`.

- **Two clients could not register a key with the same `kid`, and a key lost the `kid` it was imported with.**
  Registration used a key's `kid` as its secret identifier, which is unique across every tenant, so a second client —
  in any tenant — registering the same `kid` failed; the management import discarded a JSON Web Key's `kid`, so a client
  that signed with it never matched its key. A `kid` is now the client's own name for its key, unique among that
  client's keys. A registration update (RFC 7592) that changed `jwks` previously left the client's keys unchanged; it
  now replaces them.
  ([ADR-0161](docs/decisions/0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md))

- **An encrypted ID token was encrypted to the tenant's key, which the client cannot decrypt.** With
  `id_token_encryption_required`, the ID token was encrypted to a tenant key chosen from the server's lists. It is now
  encrypted to the client's own public key with the algorithms the client registered, and a client that registers no
  `id_token_encrypted_response_alg` under that policy is refused rather than given a guess. Logout tokens follow the
  client's ID token registration; one the server cannot protect as registered is skipped with a warning instead of
  silently. ([ADR-0155](docs/decisions/0155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md))

- **A request object that carried a numeric or boolean claim could not be read.** Standard JWT claims such as `iat`,
  `nbf`, and `exp` — and parameters such as `max_age` — are JSON numbers, which the request-object parser rejected, so
  a conformant signed request object failed with `invalid_request_object`. They are now read as their string form.
- **A client's narrower `token_endpoint_auth_signing_alg_values_supported`, and the ceiling of a security profile the
  client selects, now bind its client assertions.** The algorithm of a `private_key_jwt` or `client_secret_jwt`
  assertion was checked against the tenant's list only, so a client-level narrowing — including the FAPI 2.0 profile's
  ceiling when the client, not its tenant, selects the profile — was stored and never enforced. It is now checked
  against the authenticating client's effective settings, and a list narrowed to nothing permits no algorithm instead of
  every one. ([ADR-0149](docs/decisions/0149-settings-declare-the-scopes-they-apply-to.md))

- **The Management console no longer reads the access token.** When the token response carried no ID token, the
  console took the user's subject from the access token, which a client must treat as opaque (RFC 9068 §6). With an
  opaque access token, sign-in failed and silent renewal signed the user out. The subject now comes from the ID token
  only, and a refresh that returns none keeps the previous subject and ID token.
  ([ADR-0151](docs/decisions/0151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md))

- **The conformance dashboard could show one issuer's audit under another.** A discovery load superseded by an issuer
  change was ignored when it failed but not when it succeeded late, so a slow response for the previous issuer could
  overwrite the current one. A superseded load now never updates the dashboard.

- **A network error on the device-code token poll aborted the whole conformance-runner run.** The poll's rejection
  escaped the probe's error handling, so the runner printed "Runner failed" and exited `2` instead of reporting that
  one probe as failed. It now fails only the device-authorization probe.

- **The conformance runner rejects RSA-PSS-restricted keys up front.** It accepted an `id-RSASSA-PSS` key and then
  failed with Node's opaque "Unsupported JWK Key Type." when publishing its public key. It now explains that an
  ordinary RSA key is required — `PS256`/`PS384`/`PS512` already sign it with PSS padding.

- **A FAPI 2.0 client could not reach the authorization endpoint.** The endpoint identified the client through client
  authentication as a public client, which the profile's `token_endpoint_auth_methods_supported` ceiling refuses, so
  every request from such a client failed with `invalid_client` before the profile's own rules applied. It now
  identifies the client by its `client_id`. ([ADR-0145](docs/decisions/0145-a-client-authenticates-only-with-its-registered-method.md))

- **An empty `token_endpoint_auth_methods_supported` permitted every method.** Two disjoint ceilings — a tenant that
  allows only `client_secret_basic` and a client under the FAPI 2.0 profile — intersect to an empty list, which was read
  as unrestricted. An empty ceiling now permits no method.
  ([ADR-0145](docs/decisions/0145-a-client-authenticates-only-with-its-registered-method.md))

- **The pushed authorization request endpoint refused a request with `200 OK`.** A validation error that named no
  status code — an unsupported scope or response type, a missing `code_challenge` — was rendered as `200`. An OpenID
  protocol error without a status code is now `400 Bad Request` (RFC 6749 §5.2, RFC 9126 §2.3).
  ([ADR-0144](docs/decisions/0144-authorization-codes-are-bound-to-pkce-by-default.md))

- **A configured conformance client kept the defaults it meant to replace.** `ConformanceClientSeed`'s grant types and
  PKCE methods started out holding their defaults, and the configuration binder appends to an array that already has
  values, so a client configured with fewer grant types still received the default ones. The lists now start empty, and
  the defaults apply only when none are configured.

- **An implicit or hybrid response could be redirected in the query string.** Without a `response_mode`, the
  authorization endpoint returned its first-pass response — tokens or an error — in the query, whatever the response
  type, while validation assumed the fragment. A response type that returns a token or an ID token now defaults to the
  fragment on every path, as OAuth 2.0 Multiple Response Type Encoding Practices requires, so no token is placed in a
  URL query. ([ADR-0141](docs/decisions/0141-response-types-are-whole-values.md))

- **A hybrid client registered the standard way could not use the authorization endpoint.** The hybrid flow was checked
  against a `hybrid` grant type that OAuth does not define, so a client registered with `authorization_code` and
  `implicit` (OpenID Connect Registration §2) was refused, and the token endpoint accepted `grant_type=hybrid` to redeem
  a code. ([ADR-0141](docs/decisions/0141-response-types-are-whole-values.md))

- **A CIBA ping or push client with no notification endpoint failed to read the setting.** `client_notification_endpoint`
  had no default, so a client that registered none faulted the request instead of being rejected for the missing
  endpoint. It now defaults to empty. ([ADR-0140](docs/decisions/0140-setting-values-are-validated-against-their-domain.md))

- **A zero fetch timeout removed the bound on outbound fetches.** A `request_uri`, `jwks_uri`, or
  `sector_identifier_uri` fetch timeout of zero (or less) disabled the timeout. Such a value is now rejected.
  ([ADR-0140](docs/decisions/0140-setting-values-are-validated-against-their-domain.md))

- **The reference host could not start in Production.** `UseDeveloperCors()` added the CORS middleware
  unconditionally, but the developer CORS policy — and with it the CORS services — is registered only outside
  Production, so startup failed resolving `ICorsService`. It now adds no middleware when the policy is not registered.

- **A registration response claimed scopes the client could not obtain.** Dynamic client registration stored the
  requested `scope` under a setting nothing reads — scope entitlement comes from resource servers and client grants —
  and echoed it back. The response now reports the scopes the client may request, from `IClientScopeService`.
  ([ADR-0139](docs/decisions/0139-setting-names-are-a-closed-vocabulary.md))

- **The reference host's tenant configuration bound to nothing.** Its `appsettings.json` configured tenant selection
  under `OpenId:Tenant` (`Mode`, `StaticSingle:TenantPath`), which is the tenant *materialization* section; the
  selection section is `OpenId:TenantResolution` (`StrategyCode`, `StaticSingle:…`). The host only behaved because the
  defaults coincided with what it asked for. It now uses the canonical keys, and the misplaced shape stops the host.
  The `Conformance` launch profile pinned its issuer through the removed `tenant_issuer` setting, so the pin was
  silently dead; it now uses `OpenId:TenantResolution:StaticSingle:Issuer`.

- **A configured tenant route was never served.** The OpenID protocol endpoints were mapped at absolute routes
  whatever the tenant-resolution strategy declared, while the tenant's base address still included the tenant route —
  so a configured path such as `/identity` published an issuer nothing was served under, and the path-based strategy
  could not resolve a tenant at all. The protocol endpoints are now served under the tenant route
  (`/{tenantId}/oauth2/token`, `/identity/.well-known/openid-configuration`), and published URLs carry the tenant
  route's values. On the same path: a configured issuer is now the tenant's base address as-is (a path tenant's
  `https://op.example/acme` was given `https://op.example/acme/acme`); binding the tenant route no longer strips the
  request's route values or leaks an endpoint's parameters into the base address as a query string; and published
  URLs no longer carry an explicit default port the issuer does not.
  ([ADR-0138](docs/decisions/0138-protocol-endpoints-are-served-under-the-tenant-route.md))

- **A tenant's routing host and its published issuer could drift apart.** `DomainName` was the routing key for the
  host-based tenant strategy while `Issuer` carried the identity the tenant publishes — and nothing kept the two in
  step, so a tenant could be reachable at `op.example` while advertising `https://other.example`.
  [OIDC Discovery §4.3](https://openid.net/specs/openid-connect-discovery-1_0.html#ProviderConfigurationValidation)
  requires the returned `issuer` to be identical to the prefix the document was fetched from, so a drifted tenant is
  rejected outright by conforming clients.

  `DomainName` is removed. The host-based strategy now routes on the **host component of the issuer**, derived on
  write and never set independently, so the value routed on cannot diverge from the identity published. Two indexes
  replace the single one: a **filtered unique** index on the normalized issuer, and a deliberately **non-unique**
  index on the derived host.

  The non-uniqueness is required, not a concession: path-based tenants legitimately share a host
  (`https://op.example/tenant-a`, `…/tenant-b`), so a unique host index would make that strategy unusable beyond one
  tenant. Host uniqueness is a requirement of the host-based strategy alone and is therefore enforced by validation
  rather than by the schema. Conversely the issuer index enforces an invariant **nothing previously enforced**: two
  tenants sharing an issuer is a protocol-level collision, because the issuer is the tenant's identity and is asserted
  in every token it mints.

  **Breaking:** `PersistedTenant.DomainName` and the `DomainName` field on the management tenant contracts are
  removed; `ITenantStore.GetOrDefaultByDomainNameAsync` becomes `GetOrDefaultByIssuerHostAsync`, joined by a new
  `GetOrDefaultByIssuerAsync`. A migration drops the column, re-points the index, and clears the carried-over values
  so no tenant is routable by a host it does not claim.
  ([ADR-0129](docs/decisions/0129-the-tenant-issuer-is-the-single-authoritative-origin.md))

- **The server published its endpoint URLs from the incoming request, so a spoofed `Host` could redirect client
  credentials.** Discovery, the RFC 7592 `registration_client_uri`, the device `verification_uri`, the
  `check_session_iframe`, and the continue URL were all generated from the live `HttpContext`. A request carrying a
  foreign `Host` therefore produced a discovery document whose `issuer` was correct while its `token_endpoint`,
  `authorization_endpoint`, and `jwks_uri` pointed at the attacker — and a conforming client posts its client secret
  and authorization code to the advertised `token_endpoint`. Every absolute URL the server publishes is now anchored
  to the tenant's base address instead.

  **The tenant's issuer is now a first-class property rather than a setting.** `tenant_issuer` had to opt out of the
  settings merge lattice entirely (it was the only descriptor using the parent-wins `Keep` projection), and a
  server-level value would have forced every tenant to share one issuer with no way to override it. It is now
  `PersistedTenant.Issuer` — alongside `DomainName`, `DisplayName`, and `IsDisabled` — which is where tenant identity
  already lived. The setting existed only because it predated the tenant entity by five months.

  **Breaking:** the `tenant_issuer` setting is removed; configure `PersistedTenant.Issuer` (via the management API,
  or `OpenId:TenantResolution:StaticSingle:Issuer` for a single-tenant host) instead.
  `ITenantProvisioner.ProvisionAsync` takes an `issuer` argument. The management tenant contracts
  (`TenantResource`, `CreateTenantRequest`, `UpdateTenantRequest`) gain `Issuer`. A migration adds the column.

  A tenant with no issuer still derives its origin from the request — the development convenience that lets a
  developer's machine answer under any host, and the behaviour a deployment must not rely on.

- **ECDH-ES key derivation on P-384 and P-521 violated RFC 7518 and produced non-interoperable tokens.**
  `NCode.Identity.Jose.Algorithms.KeyManagement.EcdhKeyManagementAlgorithm` selected the Concat KDF digest from the
  key's curve size — SHA-256 for P-256, **SHA-384 for P-384, SHA-512 for P-521**. [RFC 7518
  §4.6.2](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.2) fixes the Digest Method at **SHA-256 for every
  curve**: the digest is a property of the JWE key-agreement algorithm, not of the key. The result was that `ECDH-ES`
  and `ECDH-ES+A{128,192,256}KW` with a P-384 or P-521 key derived a content encryption key that **no conforming JOSE
  implementation would reproduce** — tokens this library produced on those curves could not be decrypted by anyone
  else, and tokens produced by anyone else could not be decrypted by this library. P-256 was unaffected and remains
  byte-for-byte identical. **This is a breaking change to token compatibility:** a JWE previously issued by this
  library using ECDH-ES over P-384 or P-521 can no longer be decrypted by it. Pre-release, so this lands as a clean
  cutover with no compatibility shim ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)); the
  reasoning and the testing rule that let the defect hide are recorded in
  [ADR-0126](docs/decisions/0126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md).

  The defect was invisible because the cross-check tests asserted against the **Windows CNG** oracle
  (`EcdhKeyManagementWin` + `CngKey`), whose `SP800_56A_CONCAT` KDF has the same curve-dependent behavior —
  jose-jwt's own source documents CNG as "unusable on NIST-384 and NIST-521 curves due to broken key derivation".
  The tests now drive jose-jwt's RFC-conforming managed implementation (`EcdhKeyManagementUnix`, backed by the BCL
  `ECDiffieHellman`), which disagrees with CNG on exactly P-384 and P-521 and so turns the defect into a failing test.

### Added

- **Forwarded-header support for deployments behind a reverse proxy
  (`AddReverseProxy` / `UseReverseProxy`).** When TLS terminates at a proxy, the connection the application sees is
  plaintext HTTP to an internal address. The framework defaults are wrong in both directions here — they trust
  loopback, so a proxy in another container is not trusted, while the usual workaround of clearing the allow-lists
  trusts every peer. Trust is therefore granted only to the proxies and CIDR networks named in the `ReverseProxy`
  configuration section, and no middleware is added at all when none are configured, which is the correct posture for
  a direct-TLS deployment. `ForwardLimit` defaults to a single hop so a client cannot prepend its own entries.

- **Reusable `NCode.Json.ObjectJsonConverter` for loosely-typed JSON deserialization.** The `System.Text.Json`
  converter that reads JSON into an `object` graph (objects → `Dictionary<string, object?>`, arrays →
  `List<object?>`) was promoted out of `NCode.Identity.Jose` (where it was an internal `JoseObjectJsonConverter`)
  into the dependency-free `NCode.Json` package as the public `ObjectJsonConverter`, co-locating it with the existing
  `JsonElements` helpers so any package can reuse it. Scalar mapping is now driven by a cohesive
  `ObjectJsonConverterOptions` (passed to the constructor; `ObjectJsonConverter.Default` is the shared default
  instance). `ObjectJsonConverterOptions.Default` preserves the original lenient coercion — most-specific integers,
  `double` reals, and strings coerced to the most specific matching type (`DateTimeOffset`, then `Guid`, then a numeric
  type) before falling back to `string` — rather than the narrower built-in `System.Text.Json` behavior. The new
  policies make each facet configurable: integer handling (`ObjectJsonIntegerHandling.MostSpecific` / `Int64`), real
  handling (`ObjectJsonRealHandling.Double` / `Decimal`), oversized integers (`AllowBigInteger` →
  `System.Numerics.BigInteger` instead of a lossy `double`), date-string parsing (`ObjectJsonDateStringHandling.None` /
  `DateTime` / `DateTimeOffset`), and `CoerceStringToGuid` / `CoerceStringToNumber` (both default on; disable for strict
  round-trip fidelity so a value such as `"123"` stays a `string`). A ready-made `ObjectJsonConverterOptions`.`Default`
  and `.NewtonsoftCompatible` preset are provided — the latter widening integers to `long` and parsing date strings
  while, like Newtonsoft.Json, leaving number- and `Guid`-like strings as `string`. A bare JSON number is still never
  interpreted as an RFC 7519 `NumericDate` (the target type is unknown when deserializing to `object`) — that
  interpretation remains exclusive to `JsonElements.TryGetValue<T>`, where the caller supplies an explicit date
  target type. To keep the two helpers consistent, both share the same lenient, invariant-culture date/time parsing
  (`DateCoercion`) — so they recognize the same broad set of string formats (e.g. date-only values and
  non-hyphenated `Guid`s) and normalize kind identically: a `Z`/UTC value is `Utc`, an explicit offset is `Local`,
  and a zone-less value is labeled `Utc` (never `Unspecified`), so a parsed `DateTime` is never ambiguously treated
  as machine-local downstream.

- **Reusable `NCode.Json.UriJsonConverter` and `NCode.Json.DelegatingJsonConverter<TInterface, TImplementation>`.**
  Two framework-free `System.Text.Json` converters were promoted into the dependency-free `NCode.Json` package,
  co-locating them with `ObjectJsonConverter` / `JsonElements` so any package can reuse them. `UriJsonConverter`
  (round-trips `System.Uri` with absolute/relative fidelity) moved out of `NCode.Identity.OpenId.Core` — where it was
  an internal converter — and is now public with a shared `UriJsonConverter.Singleton` (mirroring the shared
  `ObjectJsonConverter.Default` instance, since it is stateless); `DelegatingJsonConverter<TInterface, TImplementation>`
  (serializes an interface by delegating to a concrete implementation) moved out of
  `NCode.Identity.OpenId.Abstractions` — it keeps no singleton because it carries mutable `MatchExact` state per
  interface/implementation pair. Neither carried any OpenID-specific coupling, so the move keeps `NCode.Json`
  dependency-free. The OpenID-coupled
  `StringValuesJsonConverter` (which relies on `OpenIdConstants.ParameterSeparatorChar`) and the claims/message/JWK
  converters deliberately remain in their domain packages.

- **JWS/JWE JSON serialization — General + Flattened, with multiple signatures / recipients (RFC 7515 §7.2, RFC 7516
  §7.2).** The JOSE serializer, previously compact-only, now also encodes and decodes the Flattened and General **JSON**
  serializations, which carry **multiple signatures** (JWS) / **multiple recipients** (JWE), **unprotected** header
  members, and JWE additional authenticated data (`aad`) — the shape many verifiable-credential and multi-audience
  scenarios require. Rather than add a parallel JSON-only options family, the encode options are **unified**:
  `JoseSigningOptions` / `JoseEncryptionOptions` now model any serialization through a `JoseSerializationFormat`
  (`Compact` / `Flattened` / `General`) property, with signatures/recipients always expressed as collections
  (`JoseSignatureOptions` / `JoseRecipientOptions`); compact is the degenerate one-element case. The shared
  content-encryption (`enc`) and compression (`zip`) algorithms live on the operation while per-recipient key management
  is carried by the new `JoseKeyManagementCredentials`, and `KeyManagementAlgorithm.SupportsKeyWrapping` gates
  multi-recipient encodes (direct `dir` / `ECDH-ES` agreement remains single-recipient). Decode transparently
  auto-detects the JSON serialization on the existing `IJoseSerializer.Decode` / `Deserialize` surface and
  verifies/decrypts against each signature/recipient until one matches the supplied key. This is a **breaking** change
  to the JOSE encode options: the former `JoseEncodingOptions` base and the single-credential `ExtraHeaders` shapes were
  removed in favor of the unified model (greenfield clean cutover). Closes issue [#75](https://github.com/NCodeGroup/Identity/issues/75).

- **Explicit `typ` / media-type validation hardening (RFC 8725 §3.11 BCP + RFC 9068 `at+jwt`).** JWT validation can now
  enforce an explicit, expected `typ` JOSE header to defeat token-substitution / cross-JWT-confusion, via the new
  `ValidateJwtParameters.ValidateTokenType(params string[])` validator — cohesive with the existing
  `ValidateIssuer` / `ValidateAudience` / `ValidateTokenLifeTime` validators. Matching follows the media-type rules of
  RFC 7515 §4.1.9 (the `application/` prefix is optional and the comparison is case-insensitive), exposed as the reusable
  `JoseTokenTypes.Normalize` / `JoseTokenTypes.AreSame` helpers. `JoseTokenTypes` now also carries the registered
  application `typ` constants `DPoPJwt` (`dpop+jwt`, RFC 9449), `LogoutJwt` (`logout+jwt`, OIDC Back-Channel Logout),
  `SecurityEventJwt` (`secevent+jwt`, RFC 8417), and `AuthorizationRequestJwt` (`oauth-authz-req+jwt`, RFC 9101)
  alongside `Jwt` and `AccessTokenJwt`. The resource-server access-token authentication path now requires the `at+jwt`
  type per RFC 9068 §4, and the `access_token_type` setting defaults to `at+jwt` (RFC 9068 §2.1) so issued JWT access
  tokens are explicitly typed by default.

- **First-class `JSON Web Key` / `JSON Web Key Set` types with `JWK Thumbprint` (RFC 7517 / 7638 / 9278), in the new
  `NCode.Identity.Jwk` package.** `JsonWebKey` and `JsonWebKeySet` are rich, immutable types backed by a detached
  `System.Text.Json.JsonElement` (net9+ — no backing `JsonDocument`, no cloning). `JsonWebKey` round-trips any key type
  (`RSA`/`EC`/`OKP`/`oct`/`AKP`/…) with full member fidelity, surfaces the standard members as typed accessors, exposes
  key-type-specific members by name (`GetMember`), detects/strips private material (`ContainsPrivateKeyMaterial` /
  `ToPublicKey`), and computes the RFC 7638 thumbprint and RFC 9278 thumbprint URI as instance methods
  (`ComputeThumbprint` / `ComputeThumbprintUri`) — the `jkt` fingerprint DPoP and mutual-TLS bind tokens to. The
  package is referenced by `NCode.Identity.Secrets.Abstractions` and `NCode.Identity.Jose.Abstractions`, so JWK is
  first-class in both the Secrets and JOSE stacks without any `*.Abstractions` package carrying logic. Construction is
  cohesive: public `JsonWebKey(JsonElement)` / `JsonWebKey(JsonObject)` constructors for in-memory nodes and
  `JsonWebKey.Parse(string)` for JSON text. The descriptor seam
  ([ADR-0105](docs/decisions/0105-secret-key-type-descriptor-first-class-seam.md)) still owns per-key-type member production;
  `ExportedPublicKey.Jwk` is now a `JsonWebKey`, the JWKS endpoint returns a `JsonWebKeySet`, and the DPoP proof
  validator parses the embedded key into a `JsonWebKey` and uses `ComputeThumbprint()` instead of a hand-rolled
  canonical form. See [ADR-0119](docs/decisions/0119-first-class-jwk-jwks-and-thumbprint.md).

- **Post-quantum ML-DSA signing (FIPS 204 / RFC 9964) now ships in the core libraries.** The JWA `alg` values
  `ML-DSA-44`, `ML-DSA-65`, and `ML-DSA-87` are registered by default alongside RSA/ECC — no opt-in package — composing
  the in-box .NET 10 `System.Security.Cryptography.MLDsa` primitive with **no** third-party cryptographic dependency. A
  new first-class `AKP` (Algorithm Key Pair) `AsymmetricSecretKey` (`AkpSecretKey`; JWK `kty` `AKP`, `alg`
  `ML-DSA-*`, raw `pub`, 32-byte-seed `priv`) and its `ISecretKeyTypeDescriptor` provide generate, JWK/PEM public
  import, JWK + SPKI-PEM public export, and PKCS#8 / SPKI deserialize, with the private key held data-protected in
  memory. The three `ML-DSA-*` signature algorithms are advertised only where `MLDsa.IsSupported` (OpenSSL 3.5+ / recent
  Windows 11), so discovery / JWKS never offer an algorithm the running OS cannot fulfill; discovery lists them in the
  asymmetric token-signing metadata automatically (the signing-algorithm partition keys on `AsymmetricSecretKey`). See
  [ADR-0118](docs/decisions/0118-ml-dsa-post-quantum-signatures-in-core.md).

- **EdDSA (Ed25519 / Ed448) signing is now available as the opt-in `NCode.Identity.Jose.EdDsa` package.** The JWA `alg`
  value `EdDSA` (RFC 8037) over both Edwards curves is implemented **clean-room** from the specifications
  (RFC 8032 / 8037 / 7748 / 8410) — constant-time field, scalar, and group arithmetic composing only the BCL hash
  primitives (`SHA512`, `Shake256`), with **zero** third-party cryptographic dependency in shipping code. The package
  depends only on abstractions (`NCode.Identity.Jose.Abstractions`, `NCode.Identity.Secrets.Abstractions`). It adds a
  first-class `OKP` (Octet Key Pair) `AsymmetricSecretKey` (`OkpSecretKey`; JWK `kty` `OKP`, `crv` `Ed25519` / `Ed448`)
  with its `ISecretKeyTypeDescriptor` — generate, JWK/PEM public import, JWK + SPKI-PEM public export, and PKCS#8 / SPKI
  deserialize, with the private seed held data-protected in memory and exported only through `ExportPrivateKey` — and a
  single `EdDsaSignatureAlgorithm` that selects the curve from the signing key's `crv`. A host enables it with one
  `serviceCollection.AddEdDsa()` call; discovery then advertises `EdDSA` in the asymmetric token-signing metadata
  automatically (the signing-algorithm partition keys on `AsymmetricSecretKey`), and the existing FAPI signing-algorithm
  ceiling already lists it. See [ADR-0117](docs/decisions/0117-eddsa-clean-room-implementation-opt-in-package.md).

- **Asymmetric secret keys now model their public half as `SubjectPublicKeyInfo` DER through a buffer writer, making the
  key model forward-compatible with Edwards-curve (OKP) and post-quantum keys.** The `AsymmetricSecretKey` base replaces
  `ExportAsymmetricAlgorithm()` (which coupled every asymmetric key to `System.Security.Cryptography.AsymmetricAlgorithm`)
  with `ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)` writing into a caller-supplied
  `IBufferWriter<byte>`, and derives the PEM convenience from it via a pooled non-sensitive writer. RSA/ECC implement it
  from their family exports; OKP and future PQC keys implement it from their own DER encoders. Callers that need a live
  classical algorithm still use the family-specific `ExportRSA()` / `ExportECDsa()` accessors. This is a breaking change
  to the `NCode.Identity.Secrets.Abstractions` public surface (acceptable under the greenfield pre-release posture). See
  [ADR-0115](docs/decisions/0115-asymmetric-keys-model-der-material-via-buffer-writers.md) and
  [ADR-0116](docs/decisions/0116-memory-allocations-and-buffer-operations.md).

- **Non-redirectable authorization errors are now content-negotiated: a browser gets a human-readable HTML error page,
  programmatic callers keep JSON.** When the authorize endpoint refuses to redirect an error back to the client — an
  unregistered/invalid `redirect_uri` or an unresolvable `client_id`
  ([OIDC Core §3.1.2.4](https://openid.net/specs/openid-connect-core-1_0.html#AuthError)) — an interactive request
  (`Accept: text/html`) now receives a styled, SPA-enhanced error page carrying a stable `authorization-error` element
  (and `data-error-code`) for automation, while wildcard-only (`*/*`) and programmatic callers keep the machine-readable
  JSON body. The representation is chosen by a new open **`IOpenIdResponseFormatter`** strategy collection: each
  formatter declares the media types it emits and a `CanFormat(IOpenIdResponse)` predicate, so a new representation for
  any response type is an additive registration. `OpenIdResult<T>` is now a thin `IResult` that delegates to
  `IResultExecutor<IOpenIdResult>` (`DefaultOpenIdResultExecutor` in Core), which applies the response envelope and does
  the server-driven negotiation — keeping the `NCode.Identity.OpenId.Abstractions` package lean, mirroring the existing
  `OpenIdRedirectResult` → `DefaultOpenIdRedirectResultExecutor` pattern. The HTML page is registered by the interactive
  surface (`AddInteractiveAuthentication`) and written via `SecureEncoding.UTF8`, matching `DefaultAuthorizationResult`-
  `Executor`; a deployment without the interactive package degrades gracefully to JSON. See
  [ADR-0114](docs/decisions/0114-non-redirectable-authorization-errors-are-content-negotiated.md).

    Every grant payload (`PersistedGrant<TPayload>.Payload` — authorization codes, refresh tokens, device/CIBA requests,
    pushed authorization requests, DCR tokens, the end-user `session` grant, continue envelopes) is written through a new
    `IPersistedPayloadSerializer` seam that wraps it in a self-describing envelope `{ "$ver": N, "$payload": … }`, stamping
    the schema version declared by `[PersistedPayloadVersion(N)]` (default `1`). On read, registered
    `IPersistedPayloadUpcaster` steps migrate an older stored document up to the current version before deserialization; a
    document written before the envelope existed reads as version `1`, and a newer-than-current document (a rollback) is
    read forward-compatibly. The long-lived, in-place-mutated payloads (`SessionGrant`, `CibaGrant`, `DeviceCodeGrant`)
    gain a `[JsonExtensionData]` bag so a mixed-version node cannot drop unknown members on a read-modify-write. There are
    no upcasters today — the pipeline is dormant until the first non-additive change — so the value realized now is the
    version stamp in the data and the single migration seam. **The at-rest shape of grant payloads changed; a dev-store
    reset is required** (no shipped data exists under the pre-release posture). See
    [ADR-0113](docs/decisions/0113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md).

- **Authorization-code replay now revokes the end-user session, and first-party resource endpoints enforce it (issue
  [#131](https://github.com/NCodeGroup/Identity/issues/131)).** Per RFC 6749 §4.1.2 (reaffirmed by the OAuth Security BCP, RFC 9700), a second redemption of a one-time
  authorization code is a replay/compromise signal. The token endpoint now treats it as such: on a replay it revokes the
  end-user **session's** grants — the access and refresh tokens issued from that code — via the same session-scoped
  grant revocation the logout seam uses, but without the back-channel-logout/audit side effects. The session identifier
  (`sid`, RFC 9068 §2.2.1) is now **always** emitted into subject-bound access tokens (the opt-in
  `send_session_id_in_access_token` client setting is retired), and every endpoint that authenticates a self-issued
  access token (UserInfo, introspection, the management API) rejects a token whose `sid` resolves to a revoked session
  (`invalid_token`). There is no global access-token denylist and no per-token store: one indexed session lookup on
  first-party access-token authentication, and one session-scoped revocation on the rare replay path; external resource
  servers continue to treat the JWT as self-contained. This fixes the OpenID Foundation suite's
  `oidcc-codereuse-30seconds` warning. `IPersistedGrantService` gains `RevokeBySessionAsync`. See
  [ADR-0112](docs/decisions/0112-authorization-code-replay-revokes-session.md), which refines
  [ADR-0028](docs/decisions/0028-token-revocation-refresh-tokens-only.md).

- **Scope-driven claims now return from UserInfo (not the ID token) when an access token is issued, sourced from the
  local account (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** Per OpenID Connect Core 1.0 §5.4, the `profile`/`email`/`address`/`phone` scope claims
  belong in the UserInfo response whenever an access token is issued (the authorization-code flow), and only in the ID
  token when no access token is issued (a bare `id_token` response). Three coordinated changes land this: (a)
  `DefaultGetIdTokenSubjectClaimsHandler` now gates the scope-driven claim set on no access token being issued
  (signalled by a non-null `AccessToken` on the token request), so they no longer appear in the ID token for the code
  flow — explicitly requested `claims.id_token` claims, `sub`, `auth_time`, `acr`, `idp`, and `amr` are unaffected; (b)
  a new account-sourced UserInfo enricher (`DefaultGetLocalAccountUserInfoClaimsHandler`, registered by
  `AddLocalAccountAuthentication` / `AddLocalAccountInteractiveAuthentication`) resolves the caller's local account from
  its self-issued connection and returns the scope-permitted `profile`/`email`/`address`/`phone` claims from the account
  store (the access token's `scope` claim gates which) — it also honors individual claims requested via the OIDC
  `claims` request parameter's `userinfo` member (OIDC Core §5.5), serving them from the account store even when the
  owning scope was not granted, and coerces the standard typed claims (`updated_at` → number, `address` → JSON object,
  `phone_number_verified` → boolean) so the UserInfo response carries the correct JSON types; and (c) the conformance
  test-user seed now provisions the full standard `profile`/`address`/`phone` claim set (new
  `ConformanceSeedOptions.TestUserGivenName` / `TestUserFamilyName` for the name parts, deterministic values for the
  rest). This clears the OpenID Foundation suite's `oidcc-scope-{profile,email,address,phone,all}`,
  `oidcc-claims-essential` (`EnsureUserInfoContainsName`), and `oidcc-server`
  (`EnsureIdTokenDoesNotContainNonRequestedClaims`) warnings.

- **The ID token no longer carries the `tid` and `client_id` claims (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** Those are JWT access-token
  claims (RFC 9068 §2.2), not ID-token claims — the ID-token claim set is defined by OpenID Connect Core 1.0 §2, and
  emitting a tenant id and a redundant client id the client never requested leaks data and tripped the suite's
  `oidcc-server` "id_token contains non-requested claim" warning. `DefaultGetIdTokenPayloadClaimsHandler` now emits only
  the standard `azp` (authorized party) to identify the client in the ID token; `tid` and `client_id` remain on the
  access token (`DefaultGetAccessTokenPayloadClaimsHandler`) where tenant- and client-scoped authorization reads them.
  With this and the UserInfo scope-claim work above (plus a browser-automation refinement for the unregistered
  `redirect_uri` error page), the browser-driven **Basic OP** certification plan now runs **32 PASSED, 0 WARNING,
  3 REVIEW, 4 SKIPPED, 0 FAILED** with a clean gate (the former `oidcc-codereuse-30seconds` warning is now fixed — see
  the authorization-code-replay entry above).

- **The UserInfo endpoint now accepts the access token in a form-encoded request body (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** Per RFC 6750
  §2.2 / OpenID Connect Core 1.0 §5.3.1, a client may present the bearer access token as an `access_token` form
  parameter. `DefaultAuthenticateSubjectHandler` now honors it for a non-GET request whose body is
  `application/x-www-form-urlencoded`, in addition to the preferred `Authorization: Bearer` header (RFC 6750 §2.1) which
  still takes precedence when both are present. The URI-query transport (§2.3) remains intentionally unsupported because
  RFC 9700 deprecates passing access tokens in the query string. This clears the suite's `oidcc-userinfo-post-body`
  warning.

- **`IReadOnlySettingCollection.GetValueOrNull<TValue>()` reads an optional value-type setting as a genuine
  `Nullable<TValue>`.** A new `SettingCollectionExtensions.GetValueOrNull<TValue>(SettingKey<TValue>) where TValue :
struct` returns the setting's value when present and `null` when absent, so a caller can distinguish "unset" from a
  materialized zero/default without hand-rolling a `TryGetValue` dance — the reusable replacement for the
  `settings.TryGetValue(key, out var v) ? v : (TValue?)null` pattern at freshness/limit call sites. **Public surface:**
  the new `GetValueOrNull` extension in `NCode.Identity.Settings`.

- **Numeric-tolerant `NCode.Json.ObjectGraphExtensions.TryGetValue<T>` for loosely-typed object graphs.** The typed
  dictionary accessor that reads a value out of a loosely-typed object graph (the kind `ObjectJsonConverter` produces)
  moved out of the `NCode.Identity.Jose.Abstractions` package — where it was
  `NCode.Identity.Jose.Extensions.DictionaryExtensions.TryGetValue<T>`, logic that violated the no-logic-in-abstractions
  rule — into the dependency-free `NCode.Json` package as `ObjectGraphExtensions.TryGetValue<T>`, co-located with
  `JsonElements` and `ObjectJsonConverter`. The receiver is the common `IEnumerable<KeyValuePair<string, object>>` base
  that both `IDictionary<string, object>` and `IReadOnlyDictionary<string, object>` derive from (the two are otherwise
  unrelated), so a **single** overload serves both interface-typed callers — and any concrete dictionary that implements
  both — without an ambiguous call; dictionary receivers are fast-pathed to an O(1) lookup. Unlike the old strict
  `obj is T` match, which required the value to be read back as the exact CLR width it happened to box as, the new
  accessor is **numeric-width tolerant** — a boxed integer converts across `sbyte`…`ulong`/`System.Numerics.BigInteger`
  and the real types (range-checked, so an out-of-range read fails rather than wraps) — and interprets a boxed integer
  requested as `DateTimeOffset`/`DateTime` as an RFC 7519 `NumericDate` (Unix seconds, UTC), matching the `JsonElement`
  path. The epoch conversion is shared with that path via a new internal `DateCoercion` helper (the former
  `JsonDateParsing`, broadened to cover both the string and NumericDate paths) so the two cannot drift. This also
  **fixes** a latent bug where `DefaultJsonWebTokenService` read a caller-supplied `iat` as `long` while a subject-claim
  `iat` boxed as `int` (`ClaimValueTypes.Integer`), so the strict match missed it and the token's `iat`/`nbf`/`exp`
  were silently re-stamped to "now". **Public surface:** adds `NCode.Json.ObjectGraphExtensions`; removes
  `NCode.Identity.Jose.Extensions.DictionaryExtensions`.

### Changed

- **Every remaining `Logic` grab-bag namespace is split into purpose-based namespaces (issue [#27](https://github.com/NCodeGroup/Identity/issues/27)).** No `Logic`
  namespace or folder remains in the family. A contract, its DTOs, and its default implementation now share one purpose
  namespace across each `.Abstractions`/implementation pair: `NCode.Identity.Cryptography` (`ICryptoService`,
  `CryptoServiceExtensions`, and the `BinaryEncodingType` / `HashAlgorithmType` enums it takes, which move out of the
  `NCode.Identity` root); `NCode.Identity.Secrets.Persistence` (the package root now holds `ISecretSerializer`,
  `ISecretGenerator`, `ISecretImporter`, and their request/result DTOs, since the package serves one purpose);
  `NCode.Identity.OpenId.Principals` (the `GetSubjectIdDelegate` / `GetSubjectIdentityDelegate` /
  `NormalizeSubjectIdDelegate` hooks on `OpenIdOptions`); and, in `NCode.Identity.OpenId.Authentication`, `…Pkce`
  (`ICodeChallengeMethod`, `ICodeChallengeMethodCollectionProvider`), `…OutboundHttp` (`IOutboundAddressFilter`,
  `IOutboundHttpContentReader`, `OutboundHttpReadResult`), `…ResourceServers` (`IClientScopeService`,
  `IScopeDescriptionService`, `IResourceServerCredentialResolver`, `ResolvedResourceServer`,
  `ResourceServerCredentialResolution`), `…Persistence` (`IPersistedGrantService`, `PersistedGrant<TPayload>`,
  `PersistedGrantId`, `PersistedGrantStatus`, beside the payload serializer), and `…Tokens`
  (`IAuthorizationCodeService`, beside `ITokenService`). The `…Authentication.Models` and `…Endpoints.Token.Models`
  namespaces are gone with them. An endpoint's own services now live at that endpoint's root namespace instead of an
  `Endpoints.*.Logic` sub-bucket (`ICibaRequestService`, `ICibaNotificationService`, `IUserCodeGenerator`,
  `IPushedAuthorizationRequestService`, `IInitialAccessTokenValidator`, `ISectorIdentifierValidator`, and
  `ITokenGrantHandler` in `…Endpoints.Token`), and the token-exchange `ISubjectTokenValidator` /
  `SubjectTokenValidationResult` pair moves to `…Endpoints.Token.TokenExchange`. Two vaguely named types are renamed:
  `IAuthorizationEndpointLogic` → `IAuthorizationRequestProcessor`, and the internal `OutboundHttpForgeryGuard` →
  `DefaultOutboundHttpForgeryGuard` (the canonical `DefaultFoo` name). The catch-all internal `AddLogicServices()`
  registration is replaced by one registration per purpose folder (auditing, consent, outbound HTTP, persistence, PKCE,
  resource servers, sessions, tokens). Consumers update their `using` directives; no behavior changes. Pre-release, so
  this lands on `dev` as a clean cutover with no compatibility shim
  ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Claim dates parse only by declared type, through the now-public `NCode.Json.DateCoercion` (issue [#32](https://github.com/NCodeGroup/Identity/issues/32),
  [ADR-0121](docs/decisions/0121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md)).** `DateCoercion` —
  the date/time policy shared by `ObjectJsonConverter`, `JsonElements`, and `ObjectGraphExtensions` — is public, exposing
  `TryParseDateTimeOffset` and `TryParseDateTime` so every package parses date strings under one lenient,
  invariant-culture policy instead of hand-rolling `DateTime(Offset).TryParse`. The JWT encoder parses a claim typed
  `ClaimValueTypes.DateTime` through it; a value that does not parse — including an instant outside the representable
  `DateTimeOffset` range, which the encoder previously clamped to a wrong machine-local value — is emitted as its
  verbatim string. **Behavior change:** the JWT claims-identity factory no longer sniffs JSON strings for dates. It
  re-typed any date-looking string as `ClaimValueTypes.DateTime` and rewrote its value after signature verification —
  `"1.2"` became January 2 of the current year, `"10:30"` became today's date, and an OpenID Connect `birthdate` of
  `"1990-03-15"` became `"1990-03-15T00:00:00.0000000Z"`. A JSON string now always decodes verbatim as a
  `ClaimValueTypes.String` claim; parse it with `DateCoercion` where the claim is known to be a date. **Public surface:**
  adds `NCode.Json.DateCoercion` (`TryParseDateTimeOffset`, `TryParseDateTime`).

- **JWT claim values map to and from JSON by declared type, losslessly, in one symmetric mapper (issue [#34](https://github.com/NCodeGroup/Identity/issues/34),
  [ADR-0122](docs/decisions/0122-jwt-claim-values-map-to-json-by-declared-type.md)).** Both directions of the JWT claim-value
  mapping — encoding a `Claim` into a payload value and decoding a payload value into a `Claim` — now live in one
  internal `ClaimValueMapper`, guarded by a decode → encode round-trip test over every JSON scalar kind. **Decode:** a
  JSON number keeps its exact literal and is typed most specific first — `ClaimValueTypes.Integer32`, `Integer64`,
  `UInteger64`, the unbounded `Integer` for larger integers, and `Double` only for a fraction or exponent. Previously
  every number was typed `Double`, so re-encoding lost integers above 2^53 (`9007199254740993` → `9007199254740992`).
  **Encode:** each declared type parses in its XML Schema lexical space (invariant culture; `NumberStyles.Integer` /
  `Float` instead of the lenient `NumberStyles.Any`, which coerced `"1,000"` to `1000`); `UInteger32` / `UInteger64`
  emit numbers instead of strings; `Integer` is unbounded (beyond 64 bits it emits the exact JSON number); `JSON_NULL`
  emits JSON `null` instead of `""`; and `JSON` / `JSON_ARRAY` emit the parsed value only when well formed and of the
  declared kind. A value outside its declared type — including a `Double` of `NaN`/`Infinity` or malformed `JSON`, both
  of which previously made encoding throw — is emitted as its verbatim string. **Fixed:** `IJwtEncoder` silently dropped
  a lone entry in `JwtEncodingParameters.ExtraPayloadClaims` (the guard required more than one), so an ID token whose
  only payload claim was `azp` lost it.

- **RFC 7519 `NumericDate` values are read strictly and never fault the server (issue [#33](https://github.com/NCodeGroup/Identity/issues/33),
  [ADR-0123](docs/decisions/0123-numeric-date-is-read-strictly-and-range-guarded.md)).** `NCode.Json.DateCoercion` adds the
  public, non-throwing `TryFromNumericDate(long)`, `TryFromNumericDate(double)` (fractional seconds, kept to tick
  precision as RFC 7519 allows), and `TryParseNumericDate(string?)`; `JsonElements` adds the strict
  `TryGetNumericDate()` / `TryGetPropertyNumericDate(name)`, which accept only a JSON **number**. The loosely-typed
  `TryGetValue<DateTimeOffset>` accessors now accept fractional `NumericDate`s too. Every wire-facing read routes through
  them: the DPoP proof `iat`, the client-assertion `exp`, token-exchange and session `auth_time`, the `$created_when`
  parameter, and the encoder's `iat`. **Fixed:** an out-of-range value (e.g. a self-signed DPoP proof with
  `iat: 99999999999999`, or a client assertion whose `exp` is the maximum `NumericDate`) raised an unhandled
  `ArgumentOutOfRangeException` instead of the endpoint's normal rejection; a fractional `iat`/`exp`/`auth_time` was
  rejected; and a culture-sensitive parse read `auth_time`. **Security:** the JWT lifetime validator treated a present
  but unreadable `nbf` or `exp` as absent — an unparseable `nbf` let a token be used early, and an unparseable `exp`
  never expired when expiry was not required — and accepted date **strings** for both. A present time claim must now be
  a valid `NumericDate` or validation fails. `DateTimeOffset.FromUnixTimeSeconds` / `FromUnixTimeMilliseconds` are
  banned in shipping code (`RS0030`). **Public surface:** adds `DateCoercion.TryFromNumericDate` (two overloads),
  `DateCoercion.TryParseNumericDate`, `JsonElements.TryGetNumericDate`, and `JsonElements.TryGetPropertyNumericDate`.

- **Client assertions have a bounded lifetime, and JWT lifetime validation reads `iat` (issue [#78](https://github.com/NCodeGroup/Identity/issues/78),
  [ADR-0124](docs/decisions/0124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md)).** A `private_key_jwt` /
  `client_secret_jwt` assertion was accepted for any `exp` that had not passed — up to the top of the `NumericDate`
  range, holding its `jti` in the replay cache effectively forever. It is now rejected with `invalid_client` when its
  lifetime exceeds the new non-discoverable `client_assertion_max_lifetime` setting (default **five minutes**, Auth0's
  maximum). The setting is a `Min` ceiling, so a tenant or client may only shorten it, and the FAPI 2.0 security profile
  pins it at five minutes so a raised server-wide value never reaches a FAPI client. The lifetime runs from the earlier
  of the assertion's `iat` and `nbf` to its `exp` — or, when it carries neither, from the validation time plus the clock
  skew — so it bounds both how long an assertion stays usable and how stale a pre-minted one may be (RFC 7523 §3). The
  cap is a general JWT-validation capability: `ValidateTokenLifeTimeOptions.MaxLifetime` sets
  `TokenLifetimeRequirement.MaxLifetime`, and a set maximum also requires `exp`. **Behavior change:** the lifetime
  requirement now validates `iat` for every token — a present `iat` must be a valid `NumericDate` and may not be later
  than the clock skew in the future, matching `nbf` (FAPI 2.0 §5.3.2.1). **Fixed:** the client-assertion replay record
  was retained for `exp` plus a fixed five minutes regardless of the configured `clock_skew`, so a server-wide skew above
  five minutes left a replay window; it is now retained for `exp` plus the effective clock skew. **Public surface:** adds
  `ValidateTokenLifeTimeOptions.MaxLifetime`, `TokenLifetimeRequirement.MaxLifetime`,
  `OpenIdSettingNames.ClientAssertionMaxLifetime`, and `OpenIdSettingKeys.ClientAssertionMaxLifetime`.

- **The JWT library is renamed `NCode.Identity.JsonWebTokens` → `NCode.Identity.Jwt` (and the paired `.Abstractions`
  package likewise), its service is segregated, and validation is redesigned as ASP.NET Core-style
  requirements/handlers.** ([#28](https://github.com/NCodeGroup/Identity/issues/28), [ADR-0120](docs/decisions/0120-jwt-validation-requirements-and-handlers.md).) The package,
  assembly, directory, and root namespace were renamed to the shorter, family-consistent `Jwt` form — pairing with the
  sibling `NCode.Identity.Jose(.Abstractions)` and the dominant `Jwt*` type naming. The dual-purpose
  `IJsonWebTokenService` is **split** into `IJwtEncoder` (`Encode`) and `IJwtValidator` (`ValidateAsync`); of the OpenID
  consumers, validation-only and encode-only usage is disjoint, so interface segregation decouples each side.
  Built-in validators are no longer an imperative fluent DSL but **pure-data `IJwtValidationRequirement` records**
  (`ClaimValueRequirement` — with `ValidateIssuer`/`ValidateAudience` as sugar over it — plus `TokenTypeRequirement`,
  `TokenLifetimeRequirement`, `CertificateLifetimeRequirement`, and the inline `AssertionRequirement`) carried on
  `JwtValidationParameters.Requirements` and executed by `IJwtValidationHandler` / `JwtValidationHandler<TRequirement>`
  strategies resolved from DI; external code contributes its own validators via `AddJwtValidationHandler<T>()`. Handlers
  vote on a shared `JwtValidationContext` (`Succeed`/`Fail`) instead of throwing, and the validator aggregates failures
  into a single `JwtValidationException` on the result (so `result.IsValid`/`result.Exception` consumers are
  unaffected). All contracts, requirement records, fluent builders, exceptions, and `JsonClaimValueTypes` moved into the
  `.Abstractions` package under purpose namespaces (`…Jwt.Encoding` / `…Jwt.Validation` / `…Jwt.Claims`), leaving the
  evaluator, handlers, key resolver, and claims factory in the implementation package; `NCode.Identity.OpenId.Authentication`
  now references `NCode.Identity.Jwt.Abstractions` only. Types were renamed uniformly to `Jwt*`
  (`ValidateJwtParameters`→`JwtValidationParameters`, `ValidateJwtResult`→`JwtValidationResult`,
  `ValidateJwtContext`→`JwtValidationContext`, `EncodeJwtParameters`→`JwtEncodingParameters`,
  `JsonWebTokensLibrary`→`JwtLibrary`, `AddJsonWebTokensLibrary`→`AddJwtLibrary`, `TokenValidation*Exception`→`Jwt*Exception`).

- **`IJoseSerializer` is redesigned around a parsed `JoseToken` instead of a sprawl of `out`-header overloads.**
  The previous surface — spread across many partial files — repeated every decode / deserialize / verify operation
  once per raw-`string` entry point and threaded the JOSE header back through an `out JsonElement header` parameter.
  It is replaced by a cohesive, token-centric model: `Parse(string)` returns a `JoseToken` **once** (exposing the JOSE
  header via `JoseToken.Header` **before** validation/decryption), and `Decode` / `Deserialize<T>` / `VerifyJws` then
  operate on that parsed token; the header is always read from `JoseToken.Header` rather than returned via `out`.
  `JoseToken` is a new abstract base in `NCode.Identity.Jose.Abstractions` with a `JoseSerializationFormat` and two
  sealed subclasses — `CompactJwt` (compact serialization) and `JsonJwt` (flattened/general JSON serialization) — and
  one-shot overloads that accept a raw token `string` are retained for convenience. Typed `Deserialize<T>` / strongly
  typed `VerifyJws<T>` are expressed as explicit no-options / with-options overload pairs (no optional-parameter
  overloads, satisfying the public-API analyzer). This is a **breaking** change to `IJoseSerializer` and to
  `NCode.Identity.JsonWebTokens.DecodedJwt` (now constructed from a `JoseToken`); it lands on `dev` as a clean cutover
  with no compatibility shim ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **`NCode.Identity.Jose.Abstractions` is trimmed to contracts-only, depending solely on
  `NCode.Identity.Secrets.Abstractions`.** As part of the token-centric redesign the abstractions package dropped its
  `NCode.Identity.Jwk`, `NCode.Json`, `Microsoft.Extensions.Primitives`, and `NCode.Buffers` dependencies: `CompactJwt`
  no longer carries segment-splitting logic (`StringSegments`) and instead exposes its segments as zero-allocation
  `ReadOnlyMemory<char>` slices computed by span-splitting the token once in the implementation, keeping the
  `*.Abstractions` package free of logic and heavy transitive references.

- **The secret-key `Logic` grab-bag namespace is split into purpose-based namespaces.** The vague
  `NCode.Identity.Secrets.Logic` (and the `NCode.Identity.Jose.EdDsa.Logic` grab-bag in the EdDsa plugin) are gone,
  replaced by cohesive, discoverable namespaces that match the family's house style (`…Keys`, `…Algorithms`):
  `NCode.Identity.Secrets.KeyTypes` (secret-key type descriptors, the type catalog, and the ECC-curve / ML-DSA
  reference data), `NCode.Identity.Secrets.Collections` (the `ISecretKeyCollection` family and its ordering comparer),
  `NCode.Identity.Secrets.KeyMaterial` (the `ISecretKeyFactory`, the generate / import / export parameter + result DTOs,
  the material protector, and the key-size / data-protector helpers), and `NCode.Identity.Jose.EdDsa.KeyTypes` (the
  `OkpSecretKeyTypeDescriptor` and its OKP curve support). Consumers update their `using` directives accordingly; no
  type was renamed or retyped, only moved. Pre-release, so this lands on `dev` as a clean cutover with no compatibility
  shim ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **The browser-driven Basic OP conformance plan keeps `parallel=2` — the earlier "cookie collision" concern was
  mooted, not worked around by serializing (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** An earlier run observed one module's scripted login
  landing on another module's `continue` URL under `parallel=2`, which suggested dropping the plan to `parallel=1`.
  That would have forfeited the speed boost for no correctness gain: the suite already isolates browser state **per
  test module** (each module gets its own `BrowserControl`/`CookieManager`), the plan runs under
  `client_registration=dynamic_client` so each module registers its **own** client and `redirect_uri`, and the OP's
  interactive login is **request-bound** (`PostLoginAsync` round-trips the `ReturnUrl` carrying the request's `state`
  grant key, with no ambient server-side "current pending request"). With that three-way isolation, cross-module
  contamination is structurally impossible; the observation did not reproduce and two consecutive full harness runs are
  green at `parallel=2` (`Conformance run PASSED`, 39 modules, `0 failures`). Harness-only (not a library/API change);
  details in [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md) § "Open follow-ups".

- **The conformance harness fails fast when the external suite goes unreachable mid-run (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** Previously, if
  the OpenID Foundation Conformance Suite's Docker container dropped out mid-plan, the suite's `run-test-plan.py`
  reconnect loop retried with backoff and [`build/conformance/run-conformance.ps1`](build/conformance/run-conformance.ps1)
  blocked indefinitely — an overnight hang. The wrapper now runs each plan invocation under a hard wall-clock watchdog
  (`-PlanTimeoutSeconds`, default `1800`) that tree-kills a plan on overrun and surfaces a fast, clear failure, and it
  pins the suite script's own retry bounds explicitly (`CONFORMANCE_RETRY_TIMEOUT`, `CONFORMANCE_RESTART_RETRIES`,
  `CONFORMANCE_MAX_CONSECUTIVE_FAILURES`) so the suite self-aborts quickly rather than depending on whatever defaults the
  freshly-cloned upstream ships. This is harness-only (not a library/API change); details in
  [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md) § "Open follow-ups".

- **The conformance harness checks its machine prerequisites up front instead of timing out opaquely.** On a fresh
  machine, [`build/conformance/run-conformance.ps1`](build/conformance/run-conformance.ps1) waited the full 120 s for
  the OP discovery document and then reported only a timeout, because a fresh Docker Desktop install had not written
  `host.docker.internal` into the Windows hosts file, so the host-side readiness probe could not resolve the issuer. The
  script now fails immediately with the exact fix when the issuer or suite hostname does not resolve. It also rejects
  the Microsoft Store `python` alias, which is on PATH but fails when run. The pnpm check now runs after the frontend
  bootstrap puts the NVS-managed Node 22 on PATH. A readiness timeout now includes the last probe error. The one-time
  Windows setup (Python install manager, per-user NVS, both hosts entries) is documented in
  [`build/conformance/README.md`](build/conformance/README.md), and persisting `nvs auto on` in the pwsh / Git Bash
  profiles in [`src/frontend/README.md`](src/frontend/README.md). Harness-only (not a library/API change).

    ([ADR-0111](docs/decisions/0111-complete-outbound-fetch-ssrf-coverage.md)).** Every server-initiated outbound HTTP call to a
    client-controlled URL is now SSRF-guarded uniformly. The JAR `request_uri` fetch (RFC 9101) gains the same bound set
    as the `jwks_uri` / `sector_identifier_uri` fetches: it now requires an absolute `https` URI, enforces an optional
    host allow-list, bounds the request with a wall-clock timeout, and caps the response body — closing a prior
    no-size-cap DoS vector — via a new non-discoverable `request_uri_*` settings family (`request_uri_allowed_hosts`,
    `request_uri_fetch_timeout` default 10 s, `request_uri_max_size_bytes` default 100 KB). The two server→client POST
    **deliveries\*_ that target client-registered URLs — back-channel logout (`backchannel_logout_uri`) and CIBA ping/push
    notification (`client_notification_endpoint`) — previously used the default unnamed `HttpClient` with no SSRF control;
    they now POST through dedicated SSRF-guarded named clients (connection pinned away from private/loopback/link-local
    addresses, redirects disabled), so a registered internal URL can no longer turn the OP into a confused deputy. The
    streamed response-size-cap read is extracted into one replaceable, DI-registered `IOutboundHttpContentReader` seam
    consumed by all three body-reading fetches; it writes into a caller-supplied (pooled) `IBufferWriter<byte>` and the
    callers parse directly from the resulting `ReadOnlySequence<byte>`, so the capped read no longer allocates an
    intermediate array. New public surface: the three `request*uri*_`settings and the`IOutboundHttpContentReader`/`OutboundHttpReadResult` seam.

- **FAPI 2.0 behavioral rules + an extensible security-profile registry — advances issue [#54](https://github.com/NCodeGroup/Identity/issues/54) (Phase 3)
  ([ADR-0109](docs/decisions/0109-fapi-behavioral-rules-and-discovery-validation-details.md)).** The FAPI behavioral rules
  that cannot be expressed as a floor/ceiling setting are now modeled as an **open registry of requirement objects**
  rather than scattered `security_profile == "fapi2"` checks. A profile is an `ISecurityProfile` (its name, its ceiling
  settings, and its composed `Requirements`); `ISecurityProfileCatalog` is the single registry that resolves the active
  profile and runs its requirements for a given enforcement phase via `EvaluateRequirementsAsync<TContext>`. Each
  requirement is a self-contained rule object implementing a typed phase seam
  (`IAuthorizationRequestProfileRequirement`, `ITokenRequestProfileRequirement`,
  `IClientAuthenticationProfileRequirement`), so adding a new requirement kind or a whole new profile (FAPI 1.0, a
  custom organizational profile) is additive — no change to existing enforcement sites. FAPI 2.0 now ships three
  requirements: a **confidential-client** rule (rejecting public clients at the authorization endpoint), a
  **sender-constrained access token** rule (requiring a DPoP or mutual-TLS binding at the token endpoint), and an
  **issuer-only `client_assertion` audience** rule (FAPI 2.0 §5.3.2.1). The OP also now advertises
  `authorization_response_iss_parameter_supported` (RFC 9207, default `true`), the authorization **response** redirect
  uses **303 See Other** (FAPI 2.0 §5.2.2 / OAuth 2.1) for every client, and the operator-configured `clock_skew`
  setting is now honored when validating signed request objects / client assertions (a latent bug — it was previously
  ignored in favor of a 5-minute default — with the FAPI profile tightening it to ≤ 60 s via the merge lattice). New
  surface: `ISecurityProfile`, `ISecurityProfileRequirement` (+ generic `ISecurityProfileRequirement<TContext>`),
  `ISecurityProfileCatalog`, the three phase requirement interfaces and their evaluation contexts, and the
  `authorization_response_iss_parameter_supported` setting. The standalone `ISecurityProfileSettingsExpander` was folded
  into the catalog and removed.

- **`ISecretGenerator.GenerateSharedSecret` — a correct-by-default primitive for issuing a presented secret
  ([ADR-0110](docs/decisions/0110-shared-secret-vs-cryptographic-key-storage.md)).** A symmetric `PersistedSecret` serves two
  distinct purposes that need two storage shapes: a **shared secret** an external party presents (an OAuth
  `client_secret`) must be stored as the **UTF-8 bytes of its opaque string**, whereas a **cryptographic key** the
  server itself uses (an `HS256` key, a symmetric JWE key) is stored as **raw bytes** and revealed `base64url`.
  `GenerateSharedSecret` generates a URL-safe random string and persists it in the first shape; the existing
  `GenerateSecret` keeps the second. The distinction is made at the call site (per the no-flag-argument convention), so
  a client-secret issuer can no longer silently pick the wrong representation.

- **`ISecretGenerator.ImportSharedSecret` — a correct-by-default primitive for persisting a _caller-supplied_ shared
  secret — closes issue [#69](https://github.com/NCodeGroup/Identity/issues/69) ([ADR-0110](docs/decisions/0110-shared-secret-vs-cryptographic-key-storage.md)).** The
  complement to `GenerateSharedSecret`: it persists a given `client_secret` value (supplied as the UTF-8 octets of its
  opaque string) in the identical `symmetric_raw` shape, deriving the bit length from the value and sharing a single
  internal construction with `GenerateSharedSecret` so the stored UTF-8 contract cannot drift between the two. The three
  call sites that had each re-implemented the data-protect-and-wrap logic inline — the conformance and bootstrap-admin
  seed handlers and the integration-test host factory — now route through it. New surface: `ImportSharedSecret` and the
  `ImportSharedSecretRequest` metadata type on `NCode.Identity.Secrets.Persistence`.

- **FAPI 2.0 `security_profile` mechanism — advances issue [#54](https://github.com/NCodeGroup/Identity/issues/54) (Phase 2)
  ([ADR-0108](docs/decisions/0108-fapi-security-profile-merge-lattice-ceiling-injection.md)).** A tenant or client opts into
  the OpenID Foundation FAPI 2.0 Security Profile with a single `security_profile=fapi2` setting. The profile expands
  into concrete ceiling settings at the scope that declares it, and the existing floor/ceiling merge lattice carries
  them down the `server → tenant → client` cascade un-loosenably, so **discovery and request-time enforcement read one
  source of truth**: signing restricted to `PS256`/`ES256`/`EdDSA`, strong client authentication only
  (`private_key_jwt` / `tls_client_auth` / `self_signed_tls_client_auth`), `response_type=code` + PKCE `S256` only,
  required PKCE and PAR, and short authorization-code / PAR `request_uri` lifetimes. A tenant can mandate the profile
  and a client cannot loosen it or clear the flag. The client-authentication service now also enforces that the method
  used is within the client's effective `token_endpoint_auth_methods_supported` (a general control that, under `fapi2`,
  rejects weak and public authentication at the token endpoint). New surface: the `security_profile` setting, the
  `OpenIdConstants.SecurityProfiles.Fapi2` constant, the extensible security-profile registry (`ISecurityProfile` /
  `ISecurityProfileCatalog`; see Phase 3 below), and the `AlgorithmCodes.DigitalSignature.EdDsa` algorithm code (the
  EdDSA signing implementation ships in the opt-in `NCode.Identity.Jose.EdDsa` package,
  [ADR-0117](docs/decisions/0117-eddsa-clean-room-implementation-opt-in-package.md)).

- **Mutual-TLS client authentication (`tls_client_auth` / `self_signed_tls_client_auth`) — advances issue [#54](https://github.com/NCodeGroup/Identity/issues/54)
  (Phase 1) ([ADR-0107](docs/decisions/0107-mutual-tls-client-authentication.md)).** A confidential client can now authenticate
  at the token endpoint with a mutual-TLS client certificate (RFC 8705 §2), the second FAPI-permitted strong
  client-authentication mechanism alongside `private_key_jwt`. `self_signed_tls_client_auth` matches the presented
  certificate against one the client registered by SHA-256 thumbprint; `tls_client_auth` matches its subject
  distinguished name or a subject alternative name (`dns`/`uri`/`ip`/`email`) against the client's registered metadata.
  The handlers reuse the existing `IClientCertificateResolver` (direct TLS or a trusted forwarded header) and are gated
  by the client's registered `token_endpoint_auth_method`; both methods are auto-advertised in
  `token_endpoint_auth_methods_supported`. New surface: the `tls_client_auth` / `self_signed_tls_client_auth` auth-method
  constants, the per-client `token_endpoint_auth_method` and `tls_client_auth_subject_dn` / `tls_client_auth_san_*`
  settings, and `AsymmetricSecretKey.CertificateRawData` (raw DER access that avoids materializing a certificate to
  compute a thumbprint).

- **Runtime `jwks_uri` client-key resolution and a shared, extensible outbound-fetch SSRF guard — advances issue
  [#54](https://github.com/NCodeGroup/Identity/issues/54) (Phase 0) ([ADR-0106](docs/decisions/0106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md)).** A
  client's **public** verification keys can now be resolved at runtime from its `jwks_uri` setting (previously stored but
  never consumed), so a FAPI client can authenticate via `private_key_jwt` — or present a signed request object (JAR) —
  with keys it rotates without re-registering. A new `IClientKeyResolver` seam merges the client's stored `SecretKeys`
  with the keys fetched from its `jwks_uri`; the `client_assertion` handlers and the JAR loader verify against the merged
  set. The fetch is SSRF-guarded, requires absolute `https`, and is bounded and cached by five new per-client settings
  (`jwks_uri`, `jwks_uri_allowed_hosts`, `jwks_uri_fetch_timeout`, `jwks_uri_max_size_bytes`, `jwks_uri_cache_duration`,
  mirroring the `sector_identifier_uri*` family); resolution **fails closed** to the client's stored keys, so an
  unreachable or malformed key set never grants access. The `sector_identifier_uri` SSRF floor is generalized into a
  shared, injectable `IOutboundHttpForgeryGuard` now applied to **every** server-initiated client-URL fetch —
  `sector_identifier_uri`, `jwks_uri`, and the JAR `request_uri` fetch, which previously ran through an unguarded client
  that followed redirects. The hardcoded private/reserved-address floor (the IANA special-use registry) is a
  non-weakenable security control; deployments extend it **additive-deny only** by registering `IOutboundAddressFilter`
  implementations. New public surface: `IClientKeyResolver`, `IOutboundAddressFilter`, and the five `jwks_uri*` settings.

- **The interactive login & consent pages now work without JavaScript (progressive enhancement).** The server renders a
  real `<form>` (login: `username`/`password`/`login`; consent: a per-scope checkbox plus `consent-allow`/`consent-deny`)
  carrying the antiforgery token and return URL, injected into the single-page bundle's `#root` so the React `apps/auth`
  SPA still replaces it on mount — and served standalone when no bundle is configured. One surface, no user-agent
  sniffing: no-JS clients (assistive technology, hardened browsers, the OpenID conformance suite's headless
  HtmlUnitDriver) get a working form, everyone else gets the SPA. This unblocks the browser-driven conformance profiles,
  whose no-JS driver previously saw only an empty `<div id="root">` and could not find the login fields.

- **External conformance suite wired as the certified oracle behind the in-house pre-flight — advances issue [#129](https://github.com/NCodeGroup/Identity/issues/129)
  (Phase 4 / [#124](https://github.com/NCodeGroup/Identity/issues/124) Phase D), [ADR-0084](docs/decisions/0084-spec-coverage-conformance-harness.md).** A committed, runnable
  harness under [`build/conformance/`](build/conformance) drives the **OpenID Foundation Conformance Suite** against the
  OpenID host end to end: `run-conformance.ps1` launches the OP on the reachable HTTPS origin (the `Conformance`
  launch profile), runs the headless `@ncode/app-conformance-runner` probe runner as a fast pre-flight that fails early,
  brings the suite up from prebuilt Docker images (no Java build), drives the Config OP + Basic OP plans headlessly via
  `run-test-plan.py` gated on the committed `expected-failures.json` / `expected-skips.json` baselines, and tears
  everything down — surfacing one non-zero exit code. The config templates (`oidcc-basic.json` with its `browser`
  automation block, `oidcc-config.json`) are resolved against the deterministic `AddConformanceSeed` values. It is
  **not** part of `build/dod.ps1` (Docker + Python 3, slow) and runs as an opt-in / nightly gate; the nightly CI
  pipeline itself remains the sole [#129](https://github.com/NCodeGroup/Identity/issues/129) follow-up. Documented in
  [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md).

- **Public-key import / BYOK across the management API and Dynamic Client Registration — closes issue [#161](https://github.com/NCodeGroup/Identity/issues/161), advances
  [#54](https://github.com/NCodeGroup/Identity/issues/54) ([ADR-0104](docs/decisions/0104-import-public-asymmetric-secret-material.md)).** A relying party's **public**
  asymmetric key can now be imported — as a **JWK**, **PEM**, or **X.509 certificate** — and persisted as a first-class
  secret that round-trips and is consumed for client authentication / JAR verification. A new persisted `SecretEncoding`
  axis (`symmetric_raw` / `pkcs8` / `spki` / `x509`) records the stored form orthogonally to the crypto family, so a
  bare public key (the native JWKS shape) persists and deserializes unambiguously; a certificate is simply a family plus
  the `x509` encoding (the former `SecretTypes.Certificate` discriminator is removed). New surface: `ISecretImporter` +
  `SecretKeyImportRequest` + `SecretKeyImportFormat` (persistence), a `POST .../secrets/import` endpoint on every secret
  owner (server, tenant, client, resource server) returning the material-free `SecretResource`, the
  `ImportSecretRequest` management contract, the `SupportsPublicKeyImport` capability flag, and `ISecretKeyFactory`
  `CreateRsaSpki` / `CreateEccSpki`. Import is **public-only by construction** — a private or symmetric key is rejected —
  so the reveal-once model ([ADR-0050](docs/decisions/0050-secret-material-revealed-once-on-create.md)) remains the sole path
  a symmetric value leaves the server. DCR consumes an inline `jwks` set at registration; runtime `jwks_uri` resolution
  stays deferred ([#54](https://github.com/NCodeGroup/Identity/issues/54) Phase 0). An EF migration adds the `SecretEncoding` column and backfills existing rows.

- **The secret-key-type descriptor is now the first-class seam for every key operation
  ([ADR-0105](docs/decisions/0105-secret-key-type-descriptor-first-class-seam.md)).** `ISecretKeyTypeDescriptor` owns the full
  lifecycle of a key type — validate, generate, **import**, **export** (`ExportPublicKey` → JWK + PEM), and
  **deserialize** — so registering one descriptor adds a key type end-to-end; the persistence generator / importer /
  serializer become thin wrappers over the catalog. The parallel `IJsonWebKeyConverter` strategy registry and the typed
  `JsonWebKey` / `RsaJsonWebKey` / `EccJsonWebKey` result classes are **removed** (the JWKS `keys` array is now JSON
  objects produced by the descriptor; the management public-key read path uses the same seam). The extension bases
  `SecretKeyTypeDescriptor` and `AsymmetricSecretKeyTypeDescriptor` are now **public** so an application can add a custom
  key type by deriving from them. Duplicated `SecretKey` state (metadata / key size / certificate handling) is lifted
  onto `AsymmetricSecretKey`, leaving the `Default*` / `Public*` concretes holding only their key material.

- **JWT-bearer client authentication (`private_key_jwt` / `client_secret_jwt`) — advances issue [#54](https://github.com/NCodeGroup/Identity/issues/54)
  ([ADR-0102](docs/decisions/0102-jwt-bearer-client-authentication.md)).** The token endpoint now authenticates confidential
  clients that present a signed `client_assertion`
  (`client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`, OpenID Connect Core §9 / RFC 7523).
  Two handlers share one base and are selected by the assertion's signing-key family (asymmetric ⇒ `private_key_jwt`,
  the FAPI-eligible method; HMAC ⇒ `client_secret_jwt`, non-FAPI), so each assertion is handled once and each method is
  advertised independently in `token_endpoint_auth_methods_supported`. The client is identified from the assertion
  (`iss == sub == client_id`), the signature is verified against the client's registered keys, the `aud` must be the
  tenant issuer or the receiving endpoint URL, `exp` is required, and `jti` is single-use via the new
  `IClientAssertionReplayCache` seam (default `IDistributedCache`-backed; a cluster registers a shared provider). New
  public surface: `OpenIdConstants.ClientAssertionTypes.JwtBearer` and
  `NCode.Identity.OpenId.Authentication.Clients.IClientAssertionReplayCache`. `private_key_jwt` end-to-end requires the
  OP to hold the client's public verification key, provided by the verify-only key primitive below.

- **Verify-only (public-only) asymmetric secret keys
  ([ADR-0103](docs/decisions/0103-public-only-asymmetric-secret-keys.md)).** `ISecretKeyFactory` gains `CreateRsaPublicKey`
  and `CreateEccPublicKey`, producing `RsaSecretKey` / `EccSecretKey` instances that hold only the **public** half (a
  `SubjectPublicKeyInfo`, stored unprotected): they verify signatures but cannot sign. `Create(KeyMetadata,
X509Certificate2)` now accepts a **public-only certificate** (yielding a verify-only key) instead of throwing, so a
  relying party's registered public certificate (persisted as an `x509` secret) becomes a usable verification key with
  no new secret type. This is the primitive that lets the OP hold a client's public key for `private_key_jwt`, JAR, and
  the forthcoming `jwks_uri` / BYOK onboarding ([#54](https://github.com/NCodeGroup/Identity/issues/54) Phase 0, [#161](https://github.com/NCodeGroup/Identity/issues/161)).

- **Node-`crypto` signer for the probe runner's asymmetric legs — completes issue [#130](https://github.com/NCodeGroup/Identity/issues/130)
  ([ADR-0084](docs/decisions/0084-spec-coverage-conformance-harness.md)).** The `@ncode/app-conformance-runner`
  `private_key_jwt` and DPoP probes gain a real signer instead of an injected fake: new `--private-key` / `--dpop-key`
  options load a PEM- or JWK-encoded private key, the JWA signature algorithm is inferred from the key (the curve for
  EC `ES256`/`ES384`/`ES512`, `EdDSA` for Ed25519/Ed448, `PS256` for RSA) and is overridable via `--private-key-alg` /
  `--dpop-key-alg` (e.g. `RS256`), and the signer builds the `private_key_jwt` client assertion (OIDC Core §9, carrying
  the JWK `kid` when present) and the DPoP proof (RFC 9449 §4, embedding the public JWK in the `dpop+jwt` header). ECDSA
  uses the JOSE raw `ieee-p1363` encoding and RSA-PSS uses digest-length salt. The asymmetric legs now exercise the OP
  for real once it advertises the capability (`private_key_jwt`: [#54](https://github.com/NCodeGroup/Identity/issues/54) / [#161](https://github.com/NCodeGroup/Identity/issues/161); DPoP: [#45](https://github.com/NCodeGroup/Identity/issues/45)). Development/conformance
  tooling only, outside the .NET inner loop ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Headless conformance probe runner — advances issue [#129](https://github.com/NCodeGroup/Identity/issues/129) (Phase 3)
  ([ADR-0084](docs/decisions/0084-spec-coverage-conformance-harness.md)).** The spec-coverage harness gains the Node/backend
  **probe runner** for the confidential and server-initiated legs a browser cannot drive: a new
  `@ncode/app-conformance-runner` CLI exercises `client_credentials`, token **introspection** and **revocation** with
  client authentication, **device-code** issuance + polling, **CIBA**, `private_key_jwt`, **DPoP**/**mTLS**, and
  **back-channel logout**. Each probe reports `verified` / `failed` / `skipped` (with an actionable reason — an
  unadvertised capability or a missing credential), so unimplemented backend features ([#41](https://github.com/NCodeGroup/Identity/issues/41) CIBA, [#45](https://github.com/NCodeGroup/Identity/issues/45) DPoP/mTLS, [#54](https://github.com/NCodeGroup/Identity/issues/54)
  / [#161](https://github.com/NCodeGroup/Identity/issues/161) `private_key_jwt`) surface as explicit gaps and light up as they land; the runner exits non-zero on any
  failure, making it a usable CI pre-flight for the external certified suite (Phase 4 / [#124](https://github.com/NCodeGroup/Identity/issues/124)). The framework-free,
  injectable-fetch conformance logic (discovery fetch, the security audit, the spec-coverage catalog, and the
  browser-drivable probes) moved into a shared, unit-tested `@ncode/conformance-core` package consumed by both the
  dashboard (`apps/conformance`) and the runner — no behavior change to the dashboard. The asymmetric legs
  (`private_key_jwt`, DPoP) take an injected signer, now wired to a real Node-`crypto` signer ([#130](https://github.com/NCodeGroup/Identity/issues/130), above).
  Development/conformance tooling only, outside the .NET inner loop
  ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **OAuth 2.0 Token Exchange (RFC 8693) — completes issue [#61](https://github.com/NCodeGroup/Identity/issues/61)
  ([ADR-0101](docs/decisions/0101-oauth-token-exchange-rfc8693.md)).** The token endpoint gains a
  `DefaultTokenExchangeGrantHandler` for `urn:ietf:params:oauth:grant-type:token-exchange`, registered on the existing
  `ITokenGrantHandler` extension-grant seam (no endpoint change). A client presents a `subject_token` (and, for
  delegation, an `actor_token`) and receives an `access_token`, `refresh_token`, or `id_token` output (`requested_token_type`
  in, `issued_token_type` out). Both **impersonation** and **delegation** are supported: a delegated exchange emits an
  `act` claim identifying the actor and nests any pre-existing `act` to preserve the delegation chain, and requires the
  subject token to pre-authorize the actor via a `may_act` claim (RFC 8693 §4). Presented tokens are validated through a
  new replaceable `ISubjectTokenValidator` seam whose default (`DefaultSubjectTokenValidator`) accepts only
  **self-issued** JWTs (validated against the tenant's own signing keys, issuer, and lifetime); the requesting client
  must be **confidential** and be the client the token was issued to (or be named in its `aud`). The subject is resolved
  forward through `IPrincipalResolver` (never by reversing a pairwise `sub`); effective scopes/audiences are downscoped
  via the RFC 8707 `resource` and RFC 8693 `audience` parameters; and refresh-token issuance from an exchange is gated
  per client by a new `token_exchange_allow_refresh_token` setting (off by default). The grant is **secure-by-default**
  — it is not in the baseline `grant_types_supported`, so an operator opts in (the conformance tenant does, flipping the
  `grant-token-exchange` coverage probe). New public surface: `OpenIdConstants.GrantTypes.TokenExchange`,
  `OpenIdConstants.TokenTypeIdentifiers.*`, the RFC 8693 `OpenIdConstants.Parameters.*` and `TokenTypes.NotApplicable`,
  `JoseClaimNames.Payload.MayAct`, `ITokenRequest` / `ITokenResponse` additions, `ISubjectTokenValidator` /
  `SubjectTokenValidationResult`, and `TokenExchangeGrant`. External / enterprise-IdP federation is intentionally out of
  scope and tracked as issue [#64](https://github.com/NCodeGroup/Identity/issues/64) (the `ISubjectTokenValidator` seam is its extension point).

- **Session claims & lifecycle enhancements (advances issue [#63](https://github.com/NCodeGroup/Identity/issues/63)).** The end-user session subsystem
  ([ADR-0074](docs/decisions/0074-session-management-and-logout.md)) gains server-authoritative lifecycle controls, modeled as
  **tenant settings** (a session spans every client, so its policy is tenant-scoped, not per-client):
  `session_absolute_lifetime`, `session_idle_timeout` (a sliding inactivity window, renewed on each authorize request
  and enforced server-side via `ISessionService.ValidateAndTouchAsync` on the authorize path — an idle/expired session
  self-heals by prompting for login), and `max_concurrent_sessions` (the oldest sessions beyond the cap are evicted,
  via the swappable `ISessionEvictionPolicy`). These supersede the single `InteractiveAuthenticationOptions.SessionLifetime`
  option. A new **per-client** `send_session_id_in_access_token` setting emits the `sid` claim into the access token
  (RFC 9068 §2.2.1, off by default) so a resource server can honor session-scoped revocation. Session creation and
  termination are now **audited** (`IAuditEventRecorder.RecordSessionCreatedAsync` / `RecordSessionTerminatedAsync`,
  `session.created` / `session.terminated`) and logged.
- **Unified session-termination mediator seam ([ADR-0098](docs/decisions/0098-session-lifecycle-termination-seam.md)).** The
  logout-only `OnEndSessionCommand` is **replaced** by `OnSessionTerminatedCommand`, which every termination cause flows
  through (`SessionTerminationReason`: logout, idle timeout, absolute expiry, concurrent-session eviction,
  administrative revocation, global sign-out). Default handlers revoke the session's (or subject's) grants, send
  back-channel logout notifications, and audit — so an idle/evicted/admin-revoked session now notifies its relying
  parties and audits exactly like an RP-initiated logout, closing a correctness gap. Applications extend termination by
  registering another `ICommandHandler<OnSessionTerminatedCommand>`.
- **Self-service active-sessions surface (completes issue [#63](https://github.com/NCodeGroup/Identity/issues/63)).** The OP-hosted `apps/auth` account surface gains an
  **active sessions** page: the end-user reviews every session they hold (device/browser and origin IP captured at
  sign-in, plus last-active and sign-in times), with the request's own session marked, and can sign out of an individual
  session, **everywhere else** (retaining the current session), or **everywhere**. New same-origin endpoints
  (`GET /account/sessions/list`, `POST /account/sessions/revoke`, `POST /account/sessions/revoke-all`) delegate to
  `ISessionService`, so each revocation runs the full termination seam. A per-session revoke is guarded to the
  authenticated subject (a crafted `sid` cannot terminate another subject's session). To support the surface,
  `SessionGrant` / `CreateSessionRequest` capture the sign-in `UserAgent` and `RemoteIpAddress`, a new
  `SessionTerminationReason.UserInitiated` distinguishes self-service revocation in the audit trail, and
  `ISessionService` gains `RevokeAllExceptAsync` (and `RevokeAllAsync` now takes the termination reason).
- **Tenant-addressed admin sessions management endpoint (completes issue [#63](https://github.com/NCodeGroup/Identity/issues/63),
  [ADR-0100](docs/decisions/0100-explicit-tenant-context-materialization.md)).** `/api/tenants/{tenantId}/sessions` lists a
  subject's active sessions (`GET ?subjectId=…` → `SessionResource`) and revokes a single session (`DELETE /{sessionId}`)
  or every session a subject holds (`DELETE ?subjectId=…`). Unlike bulk grant revocation, each revocation runs the full
  session-termination seam (grant revocation, back-channel logout, and session auditing) in the addressed tenant's
  security domain, and attributes the acting administrator (`session.revoked.admin`). This is enabled by a new
  capability to **materialize an `OpenIdContext` for an explicit, route-addressed tenant**
  (`IOpenIdContextFactory.CreateForTenantAsync`, `IOpenIdTenantFactory.CreateTenantByIdAsync`), decoupled from the
  request's tenant-resolution strategy and derived from the tenant's own configured issuer.

### Changed

- **An unsigned (`alg:none`) JWT-secured authorization request now returns `request_not_supported` (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).**
  Per OpenID Connect Core 1.0 §6.1, an OP that does not support unsigned request objects must answer exactly
  `request_not_supported`. `DefaultLoadAuthorizationRequestHandler` previously surfaced a generic
  `invalid_request_object` for any request-object decode failure; it now detects an `alg:none` request object (inline
  `request` or a fetched `request_uri`) up front and rejects it with `request_not_supported`, while a genuinely
  malformed request object still returns `invalid_request_object`. The OP advertises only asymmetric request-object
  signing algorithms (RFC 9700 / FAPI), so an unsigned request object is never accepted.

- **Self-documenting tenant/context factory method pairs ([ADR-0100](docs/decisions/0100-explicit-tenant-context-materialization.md)).**
  `IOpenIdContextFactory.CreateAsync` is renamed to `CreateForRequestAsync` (paired with the new `CreateForTenantAsync`),
  and `IOpenIdTenantFactory.CreateTenantAsync` is renamed to `CreateTenantForRequestAsync` (paired with the new
  `CreateTenantByIdAsync`), so each request-driven method is distinguished by name from its explicit-tenant counterpart.
  "Resolve" stays reserved for `ITenantResolver` selecting a `PersistedTenant`.

- **Forward-compatible store filters ([ADR-0099](docs/decisions/0099-forward-compatible-evolving-parameters.md)).**
  `IGrantStore.GetPageAsync` and `RevokeWhereAsync` now take a `GrantFilter` init-only record struct
  (`SubjectId`/`ClientId`/`SessionId`/`GrantType`) instead of positional filter parameters, so adding a future
  selection criterion is a source-compatible change rather than a breaking signature change. The grants management
  query gains a `grantType` filter.

- **Forward-compatible store pagination — `PageRequest` (completes issue [#26](https://github.com/NCodeGroup/Identity/issues/26),
  [ADR-0099](docs/decisions/0099-forward-compatible-evolving-parameters.md)).** The positional `(cursor, limit)` pagination
  pair is replaced by a shared `PageRequest { Cursor, Limit }` init-only record struct (in `NCode.Persistence.Stores`,
  beside `PagedResult<T>`) across every store `GetPageAsync` — `IGrantStore`, `IClientStore`, `IClientGrantStore`,
  `IResourceServerStore`, `IServerStore`, `ITenantStore`, `ILocalAccountStore`, and the generic `BaseStore` — so
  pagination can grow (e.g. a future sort direction) without a breaking signature change, the same forward-compatible
  shape the filter DTOs already use.

- **Sender-constrained access tokens — DPoP (RFC 9449) + mutual-TLS (RFC 8705), issuance side (advances issue
  [#45](https://github.com/NCodeGroup/Identity/issues/45)).** Access tokens can now be bound to a proof-of-possession key instead of being plain bearer tokens. A single
  `IProofOfPossessionService` evaluates, once per token request, an optional **DPoP proof** (the `DPoP` header — a JWS
  with `typ: dpop+jwt` and an embedded public JWK, validated for signature, `htm`/`htu`/`iat`/`jti` and replay against
  the asymmetric `dpop_signing_alg_values_supported` set) and/or a **mutual-TLS client certificate**, then binds the
  issued access token's `token_type` (`DPoP`/`Bearer`) and `cnf` confirmation claim (`jkt` / `x5t#S256`) uniformly
  across every grant: the evaluated `ProofOfPossessionBinding` is passed first-class to `ITokenGrantHandler.HandleAsync`
  and carried on `CreateSecurityTokenRequest.Confirmation`, which the access-token payload-claims handler reads to emit
  `cnf`. New
  per-client/tenant settings: `require_dpop`, `dpop_nonce_required`, `dpop_proof_lifetime`, and
  `tls_client_certificate_bound_access_tokens`; discovery advertises `dpop_signing_alg_values_supported` and
  `tls_client_certificate_bound_access_tokens` (secure-by-default, the latter off until opted in). New error codes
  `invalid_dpop_proof` and `use_dpop_nonce` (with a `DPoP-Nonce` response header). Certificate ingestion lives in the
  default `IClientCertificateResolver`, which reads the connection certificate directly or, when
  `MutualTlsForwardingOptions.CertificateHeaderName` is configured (from the `OpenId:MutualTls` section), a **trusted**
  forwarded header for any reverse-proxy / load-balancer / service-mesh topology (Base64-DER, PEM, URL-encoded PEM, or
  Envoy XFCC) — no host code. The DPoP replay cache (`IDPoPProofReplayCache`) and nonce service (`IDPoPNonceService`)
  run over the standard `IDistributedCache`: an in-memory provider is registered by default (single node) and a cluster
  makes them cross-node by registering a shared provider (e.g. `AddStackExchangeRedisCache`). New public surface across
  `NCode.Identity.OpenId.Abstractions` (constants, error codes, settings), `…Authentication.Abstractions` (the service
  and resolver seams, `TokenConfirmation`, `ProofOfPossessionBinding`, `CreateSecurityTokenRequest.Confirmation`, the
  `ITokenGrantHandler.HandleAsync` binding parameter, `MutualTlsForwardingOptions` / `MutualTlsClientCertificateFormat`).
  **Resource-server validation** re-checks a bound token's `cnf` against the presented proof/certificate (including the
  DPoP `ath` access-token hash) at the UserInfo and management-API endpoints, which now accept both the `Bearer` and
  `DPoP` authorization schemes (`IProofOfPossessionService.ValidateResourceAccessAsync`). **DPoP at the authorization
  endpoint** is supported via the `dpop_jkt` request parameter (RFC 9449 §10): its format is validated, the token
  endpoint requires the redeeming proof's key to match, and the hybrid front-channel access token is bound to it.
  **Refresh-token binding continuity** (RFC 9449 §5) persists the `cnf` on the refresh-token grant
  (`RefreshTokenGrant.Confirmation`) so rotated tokens stay bound to the original key and a bound refresh token
  presented without a matching proof is rejected. **mTLS endpoint aliases** (RFC 8705 §5) are advertised via the new
  `mtls_endpoint_alias_authority` setting. Design and rationale:
  [ADR-0097](docs/decisions/0097-sender-constrained-access-tokens-dpop-and-mtls.md).

- **Reachable HTTPS host for the external conformance suite (completes issue [#126](https://github.com/NCodeGroup/Identity/issues/126)).** A dedicated
  `Conformance` launch profile turns the OpenID host into a real, externally reachable HTTPS process for the OpenID
  Foundation Conformance Suite. It binds all interfaces on the stable port (`https://0.0.0.0:5001;http://0.0.0.0:5000`)
  so the suite's Docker containers reach it via `host.docker.internal:5001` (a `localhost`-only bind is unreachable
  through `host.docker.internal`), and pins the discovery `issuer` to that exact origin
  (`OpenId__Server__Settings__tenant_issuer = https://host.docker.internal:5001`) so it byte-matches the suite's
  `server.discoveryUrl`. The pin rides the parent-owned (`Keep`) `tenant_issuer` setting, so the server-level value
  flows down to the single static tenant deterministically; it lives in the launch profile (not
  `appsettings.Conformance.json`) so the Aspire-orchestrated `ConformanceAspire` dashboard instance on `:5003` keeps its
  own request-derived issuer. The profile reuses the conformance tenant/seed ([#127](https://github.com/NCodeGroup/Identity/issues/127) — `AddConformanceSeed`) and the
  interactive login/consent UI ([#125](https://github.com/NCodeGroup/Identity/issues/125)). Run with
  `dotnet run --project src/NCode.Identity.OpenId.Host --launch-profile Conformance`. Detailed plan:
  [`docs/conformance/reachable-host.md`](docs/conformance/reachable-host.md).

- **Registration-time `sector_identifier_uri` fetch + validation with an SSRF-guarded client (completes issue
  [#58](https://github.com/NCodeGroup/Identity/issues/58)).** Dynamic Client Registration (RFC 7591/7592) now accepts, validates, persists, and round-trips `subject_type`
  and `sector_identifier_uri` client metadata, and performs the OpenID Connect Core 1.0 §8.1 remote step that
  [ADR-0093](docs/decisions/0093-pairwise-subject-identifiers.md) deferred: it **fetches** the JSON document the
  `sector_identifier_uri` references and **verifies** it lists every one of the client's `redirect_uris`, so a pairwise
  client can only share a sector when a party controlling that document vouches for its redirect URIs. The fetch is the
  work of a replaceable `ISectorIdentifierValidator` seam (sibling of `IInitialAccessTokenValidator`, `TryAddSingleton`,
  DI-overridable) whose default implementation is hardened against server-side request forgery by four layered controls:
  an **always-on private-address block** (a dedicated `NCode.Identity.OpenId.SectorIdentifier` named `HttpClient` whose
  `SocketsHttpHandler.ConnectCallback` pins the socket to the resolved IP and refuses loopback/private/CGNAT/link-local/
  unique-local/multicast/reserved ranges — defeating DNS-rebinding — with redirects disabled), an optional host
  allow-list (`sector_identifier_uri_allowed_hosts`), a timeout (`sector_identifier_uri_fetch_timeout`, default 10s),
  and a response size cap (`sector_identifier_uri_max_size_bytes`, default 100 KB). The `https`-only requirement and the
  §8.1 rule that a pairwise client whose `redirect_uris` span multiple hosts must supply a `sector_identifier_uri` are
  enforced at registration (stricter than the runtime sector fallback, which remains for clients created outside DCR).
  See [ADR-0095](docs/decisions/0095-sector-identifier-uri-fetch-and-ssrf-guard.md).

- **Uniform principal resolution at every authentication entry point + `SubjectAuthentication.PrincipalId`.** The
  interactive login path now resolves the verified credential to its stable, server-owned principal id and normalizes
  the cookie subject to it (after the pluggable `IInteractiveCredentialVerifier`, before the session is established) —
  closing a gap where interactive logins keyed grants/consent/sessions by the raw `LocalAccountId` while ROPC and bearer
  re-auth keyed them by the resolved principal, so "revoke everything for this subject" and consent reuse now span a
  user's activity regardless of how they authenticated, and the internal `LocalAccountId` is never emitted as `sub`
  (ADR-0035). `IPrincipalResolver` gains a self-contained-unit-of-work overload
  `ResolvePrincipalIdAsync(OpenIdContext, ClaimsPrincipal, CancellationToken)` (no persistence types in its signature,
  so the interactive leaf package resolves without a persistence dependency), and `SubjectAuthentication.SubjectId` is
  renamed **`PrincipalId`** to name the single authoritative subject (the `Subject` `ClaimsPrincipal` is raw
  authentication material; `PrincipalId` is what grants, consent, sessions, revocation, and token `sub` projection key
  on). The resource-owner-password/interactive `sub` is now the resolved opaque principal id rather than the raw
  `LocalAccountId` (a privacy improvement; greenfield, no shim). The persistence/request `SubjectId` names are retained
  and documented as holding the resolved principal id. See
  [ADR-0094](docs/decisions/0094-resolve-principal-at-every-authentication-entry-point.md).

- **`Subject.sub == PrincipalId` enforced by construction at every ticket site (completes issue [#60](https://github.com/NCodeGroup/Identity/issues/60)).** Every
  authentication entry point now builds its `SubjectAuthentication` through the shared `SubjectAuthentication.Create`
  factory, which normalizes the raw credential principal's subject claim to the resolved `PrincipalId` before
  constructing the ticket — so the ROPC and bearer/UserInfo paths can no longer leave the raw `sub` claim
  (`LocalAccountId` / the presented token's `sub`) on `SubjectAuthentication.Subject` while `PrincipalId` holds the
  resolved id. The normalization is the inverse of subject extraction and is exposed as a tenant-aware, host-replaceable
  seam symmetric with `GetSubjectId`: `OpenIdContext.NormalizeSubjectId(ClaimsPrincipal, string)` →
  `OpenIdEnvironment.NormalizeSubjectId(...)`, configurable via the new `OpenIdOptions.NormalizeSubjectId` delegate. The
  interactive login path's cookie re-stamp now routes through the same seam (dropping its duplicated inline rewrite), so
  the two-sources-of-truth trap cannot return. See
  [ADR-0094](docs/decisions/0094-resolve-principal-at-every-authentication-entry-point.md).

- **Pairwise subject identifiers (PPID) via the `subject_type` setting (completes issue [#47](https://github.com/NCodeGroup/Identity/issues/47)).** A client registered
  with `subject_type = pairwise` now receives a per-sector Pairwise Pseudonymous Identifier as its `sub` instead of the
  stable public subject, so colluding relying parties in different sectors cannot correlate the same end-user (OpenID
  Connect Core 1.0 §8). The projection is a **stateless, deterministic** derivation —
  `base64url(SHA-256(sectorIdentifier ∥ principalId ∥ tenantId ∥ pairwise_salt))` — exposed through the replaceable
  `ISubjectIdentifierService` (`TryAddSingleton`, DI-overridable), so there is **no new table, store, or migration**.
  The stable principal identifier remains the single internal subject behind grants, consent, sessions, and revocation;
  pairwise only changes the emitted `sub` at the outbound seams (ID token, access token, back-channel `logout_token`,
  and refresh-token introspection — UserInfo echoes the access token's already-projected `sub`). The sector follows
  §8.1: the host of the new `sector_identifier_uri` client setting when present, else the common host of the client's
  `redirect_uris`, else the `client_id`. A new optional secret `pairwise_salt` tenant setting hardens the derivation.
  `subject_types_supported` now advertises `["public", "pairwise"]` (was `["public"]`) and `subject_type` defaults to
  `public`. See [ADR-0093](docs/decisions/0093-pairwise-subject-identifiers.md).

- **Token-endpoint `401` + `WWW-Authenticate` challenge on failed `Authorization`-header client authentication (completes
  issue [#48](https://github.com/NCodeGroup/Identity/issues/48)).** A failed `client_secret_basic` authentication now answers with `401 Unauthorized` and a
  `WWW-Authenticate: Basic realm="<issuer>"` challenge, as [RFC 6749 §5.2](https://datatracker.ietf.org/doc/html/rfc6749#section-5.2)
  requires when the client authenticated via the HTTP `Authorization` header; body-based (`client_secret_post`) and
  assertion-based methods stay `400`. The challenge rides a new general response-header seam — `ISupportResponseHeaders`
  (sibling of `ISupportStatusCode`) that `IOpenIdError` now implements and `OpenIdResult<T>` renders — so the fix covers
  the token endpoint and every other client-authenticating endpoint (introspection, revocation, device-authorization,
  CIBA, PAR) through the shared `IClientAuthenticationService` funnel, and the same seam will carry the
  `WWW-Authenticate: Bearer` challenge for sender-constrained tokens (issue [#45](https://github.com/NCodeGroup/Identity/issues/45)). Extension grants
  ([RFC 6749 §4.5](https://datatracker.ietf.org/doc/html/rfc6749#section-4.5)) need no endpoint change: registering an
  `ITokenGrantHandler` whose `GrantTypes` includes the extension grant's absolute-URI identifier is the seam. See
  [ADR-0092](docs/decisions/0092-token-endpoint-401-www-authenticate-challenge.md).

- **Conformance server unlocks the implemented-but-off-by-default capabilities (discovery coverage).** The spec-coverage
  dashboard ([ADR-0084](docs/decisions/0084-spec-coverage-conformance-harness.md)) is discovery-driven, so it flagged
  Session Management (`check_session_iframe`), Front-Channel Logout, and the `client_credentials` grant as **missing** —
  not because they are unimplemented, but because they are **available-but-disabled by default** (the first two are
  third-party-cookie-dependent per
  [ADR-0088](docs/decisions/0088-opt-in-front-channel-logout-and-session-management.md); `client_credentials` is excluded from
  the secure-default `grant_types_supported`). Rather than weaken the stock secure defaults, the local-dev Aspire graph
  ([ADR-0071](docs/decisions/0071-aspire-local-dev-orchestrator.md)) now runs a **second, dedicated conformance OP instance**
  (`conformanceapi`) — the same host project launched under the Conformance environment on its own ports
  (`https://localhost:5003`), sharing the one Postgres — and the conformance dashboard reads **its** discovery instead of
  the locked-down `api`. That instance's `appsettings.Conformance.json` defines its own `OpenId:Server:Settings` root
  ceiling (the operator ceiling that widens what downstream tenant/client scopes inherit) enabling
  `session_management_supported`, `frontchannel_logout_supported`, `frontchannel_logout_session_supported`, and adding
  `client_credentials` to `grant_types_supported` (this grant must be widened at the **root** level because
  `grant_types_supported` merges by intersect — a child can only narrow), plus an explicit `Cors:AllowedOrigins` for the
  dashboard (the Development CORS default does not apply outside Development). A seeded confidential client also gains the
  `client_credentials` grant so the capability is exercisable, not merely advertised. The default/production server stays
  locked down; only the dedicated conformance instance opts in. Development/conformance convenience only
  ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Essential `acr` enforcement (completes issue [#57](https://github.com/NCodeGroup/Identity/issues/57)).** When an authorization request asks for `id_token.acr` as
  an **essential** claim with specific `values` (OpenID Connect Core 1.0 §5.5.1.1), the subject-authentication
  validator (`DefaultValidateSubjectAuthenticationHandler`) now denies with `access_denied` unless the satisfied `acr`
  on the subject principal is among the requested values — previously such a mismatch silently issued a token whose
  `acr` did not match. Voluntary `acr` requests, including the entire `acr_values` parameter (which the spec defines as
  voluntary), remain best-effort and impose no constraint. This builds directly on the emitted, principal-stamped `acr`
  from [ADR-0091](docs/decisions/0091-acr-emission-and-conditional-auth-time.md).

- **`acr` claim emission via a replaceable resolver, `amr` recording, and conditional `auth_time` (completes issue
  [#49](https://github.com/NCodeGroup/Identity/issues/49)).** The authorization server now tracks the authentication context at the two authentication entry points (local
  interactive sign-in and the ROPC subject handler): it records `amr=["pwd"]` (RFC 8176) — previously wired through the
  token pipeline but never actually recorded — and resolves an `acr` (Authentication Context Class Reference) value
  through a new **async, DI-overridable** seam `IAuthenticationContextReferenceResolver`. OpenID Connect defers the
  meaning of `acr` to the deployment, so the default resolver asserts the PAPE multi-factor URN
  (`OpenIdConstants.AuthenticationContextClassReferences.MultiFactor`) when multiple factors were used and otherwise the
  configured `default_acr` **setting** baseline (unset ⇒ the claim is omitted); a deployment overrides it by setting,
  by registering its own resolver, or by a custom verifier. The resolved `acr` is stamped on the principal and captured
  on the session (`SessionGrant.AuthenticationContextClassReference`), and the ID-token and access-token subject-claims
  handlers now emit `acr`, replacing the standing `// TODO: acr` markers. `auth_time` emission is now **conditional**:
  the new `always_include_auth_time` setting (default `true`) governs whether the ID token always includes it, while it
  is always included when **required** (a `max_age` request or an essential `auth_time` claim request); the access token
  no longer force-synthesizes `auth_time` (RFC 9068 includes it only when available). See
  [ADR-0091](docs/decisions/0091-acr-emission-and-conditional-auth-time.md).

- **Generic background job-dispatch subsystem with a BYO-orchestration adapter port — and durable CIBA push/ping
  delivery (completes issue [#24](https://github.com/NCodeGroup/Identity/issues/24)).** New packages `NCode.Identity.Dispatch(.Abstractions)` provide a small, reusable
  way to run deferred, at-least-once work: a serializable `IDispatchJob`, an `IDispatchJobHandler<TJob>` (throw to
  retry), and the adapter port `IJobDispatcher` (`DispatchAsync<TJob>(job, DispatchOptions?, …)`). The default,
  registered via `AddDispatchServices().AddInProcessJobDispatcher()`, is an **in-process, self-scheduling** adapter: a
  bounded in-memory queue drained by a hosted worker that retries with exponential back-off. A deployment swaps in a
  **durable or distributed** backend (a transactional outbox, a message broker such as RabbitMQ, or a scheduling cluster
  such as Quartz) by registering its own `IJobDispatcher` — the producers and handlers are unchanged. **CIBA ping/push
  delivery now rides this port:** `ICibaRequestService` composes a `CibaDeliveryJob` on approval and dispatches it, and
  `CibaDeliveryJobHandler` performs the HTTP `POST` and throws on failure so the dispatcher retries — turning the former
  best-effort single attempt into at-least-once delivery. `ICibaNotificationService` is narrowed to just the
  authentication-device seam (`NotifyAuthenticationDeviceAsync`); its `NotifyClientAsync` / `DeliverTokensAsync` HTTP
  methods are **removed**. **Back-channel logout ([ADR-0074](docs/decisions/0074-session-management-and-logout.md)) also rides
  the port:** the default `IBackChannelLogoutNotifier` now builds the signed `logout_token` and dispatches a delivery
  job rather than `POST`ing inline, so a failed `logout_token` delivery is retried. The concrete delivery jobs are
  internal (the dispatcher is generic over `IDispatchJob`). The proposed durable-audit outbox
  ([ADR-0048](docs/decisions/0048-durable-audit-delivery-outbox.md))
  is now framed as one adapter of this port ([ADR-0090](docs/decisions/0090-durable-outbound-delivery-dispatch-adapter.md)).

- **CIBA request-object encryption advertisement and enforcement (completes issue [#24](https://github.com/NCodeGroup/Identity/issues/24)).** New server settings
  `backchannel_authentication_request_encryption_alg_values_supported` / `…_enc_values_supported` /
  `…_zip_values_supported` are advertised in discovery alongside the existing signing-alg setting. The backchannel
  request-object loader now enforces them: an encrypted (JWE) request object must use an advertised key-management
  `alg`, content-encryption `enc`, and (if present) `zip`; a signed (JWS) one must use an advertised signing `alg`
  (also fixing a latent case where a JWE's key-management `alg` was checked against the signing set)
  ([ADR-0090](docs/decisions/0090-durable-outbound-delivery-dispatch-adapter.md)).

- **Pushed Authorization Requests (PAR, RFC 9126) — completes issue [#46](https://github.com/NCodeGroup/Identity/issues/46).** The OP now exposes a
  `pushed_authorization_request_endpoint` (`/oauth2/par`) where a client authenticates (identically to the token
  endpoint) and POSTs its authorization request parameters as `application/x-www-form-urlencoded`; the request is
  loaded and validated exactly as the authorization endpoint would, then persisted as a single-use grant and answered
  with `201 Created` carrying an opaque `request_uri` (`urn:ietf:params:oauth:request_uri:<reference>`) and
  `expires_in`. The client then invokes the authorization endpoint with just `client_id` + `request_uri`; the pushed
  parameters are replayed from storage, consumed once (a replay is rejected), and bound to the client that pushed them
  (a `request_uri` issued to another client is rejected with `invalid_request_uri`). Client-authentication secrets
  (`client_secret`, `client_assertion`, `client_assertion_type`) are stripped before persistence and never replayed.
  New per-client/tenant settings: `require_pushed_authorization_requests` (secure opt-in; when set, a direct
  authorization request without a pushed `request_uri` is rejected — advertised in discovery) and
  `pushed_authorization_request_lifetime` (default 90 seconds). The endpoint is auto-advertised in discovery metadata
  ([ADR-0089](docs/decisions/0089-pushed-authorization-requests.md)).

- **Front-Channel Logout 1.0 and Session Management 1.0, available but disabled by default — completes issue [#22](https://github.com/NCodeGroup/Identity/issues/22).**
  The OP now implements both browser-mediated session mechanisms behind opt-in server settings, complementing the
  cookie-independent RP-Initiated and Back-Channel Logout it already shipped. **Front-Channel Logout:** when
  `frontchannel_logout_supported` is enabled, the `end_session_endpoint` renders a hidden iframe per participating client
  (each client's `frontchannel_logout_uri`, with `iss`/`sid` appended when `frontchannel_logout_session_required`) so
  relying parties clear their own sessions in the user agent. **Session Management:** when `session_management_supported`
  is enabled, the authorization response carries the `session_state` parameter, the OP maintains an opaque browser-state
  cookie, and discovery advertises a `check_session_iframe` the relying party polls via `postMessage`. Both default to
  **off** because they depend on third-party cookies the browser platform is removing (prefer Back-Channel Logout); a
  deployment that still needs them enables the corresponding setting
  ([ADR-0088](docs/decisions/0088-opt-in-front-channel-logout-and-session-management.md), which reverses the earlier deferral
  in [ADR-0074](docs/decisions/0074-session-management-and-logout.md)). New settings: `frontchannel_logout_supported`,
  `frontchannel_logout_session_supported`, per-client `frontchannel_logout_uri` /
  `frontchannel_logout_session_required`, and `session_management_supported`.

- **Supporting index for resource-server scope lookups — completes issue [#23](https://github.com/NCodeGroup/Identity/issues/23).** The resource-server runtime reads
  push their scope-value predicate to the DBMS ([ADR-0080](docs/decisions/0080-store-query-methods-push-filtering-to-dbms.md)),
  but the scope-driven `SelectMany` join could not seek on scope value: the only index leading with `NormalizedValue`
  was the composite unique `(ResourceServerId, NormalizedValue)`, whose leading column is the owner. A standalone
  non-unique index on `ScopeEntity.NormalizedValue` (new EF migration) backs the owns-any-scope lookup. The companion
  option — a filtered index on `ResourceServerEntity.IsDisabled` — was deliberately **not** added: `IsDisabled` is a
  low-selectivity boolean (nearly every resource server is enabled), so a standalone or partial index on it would not be
  chosen by the query planner and would only add write cost; the selective scope-value index above is what carries those
  queries.

- **Refresh-token reuse detection with family revocation — completes issue [#42](https://github.com/NCodeGroup/Identity/issues/42).** A revoked refresh token presented
  again is a replay signal; the OP already detected and audited it (`token.refresh.replay`), and now responds per the
  new per-client `refresh_token_reuse_policy` setting: `revoke_all` (the secure default) revokes the entire **rotation
  family** — every refresh token descended from the same authorization grant — so a stolen token cannot be exchanged
  even if the attacker races ahead of the legitimate client, per the OAuth 2.0 Security BCP
  ([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700)); `none` preserves audit-only behavior
  ([ADR-0087](docs/decisions/0087-refresh-token-reuse-detection-and-family-revocation.md)). Rotated refresh tokens are now
  linked by a server-generated **lineage id** persisted on the grant (new nullable `RefreshTokenLineageId` column on the
  grants table, with an EF migration); a fresh issuance starts a new family and each rotation inherits its parent's
  lineage. New public surface: `OpenIdConstants.RefreshTokenReusePolicy`, the `refresh_token_reuse_policy` setting
  (`OpenIdSettingNames`/`OpenIdSettingKeys`), `IGrantStore.RevokeByRefreshTokenLineageAsync`,
  `IPersistedGrantService.RevokeByRefreshTokenLineageAsync`, and the `RefreshTokenLineageId` members on `PersistedGrant`,
  `PersistedGrant<TPayload>`, `CreateSecurityTokenRequest`, and `GrantEntity`.

- **Device-authorization endpoint migrated to the canonical endpoint shape — completes issue [#56](https://github.com/NCodeGroup/Identity/issues/56).** The
  `/oauth2/device_authorization` endpoint (`DefaultDeviceAuthorizationEndpointHandler`) is now a thin orchestrator over
  a strongly-typed `DeviceAuthorizationRequest : OpenIdMessage<…>` and mediator commands
  (`ValidateDeviceAuthorizationRequestCommand`, `CreateDeviceAuthorizationRequestCommand` →
  `DeviceAuthorizationResult`), matching the token, end-session, and CIBA endpoints and the shape codified in
  [ADR-0086](docs/decisions/0086-canonical-openid-endpoint-shape.md). It no longer reads the form directly or inlines its
  validation and persistence, so each protocol step is independently testable and replaceable. Behavior is unchanged.
  New public surface across `NCode.Identity.OpenId.Authentication(.Abstractions)`: the `IDeviceAuthorizationRequest` /
  `DeviceAuthorizationRequest` messages and the two mediator commands.

- **CIBA grant (OpenID Connect Client-Initiated Backchannel Authentication) — completes issues [#41](https://github.com/NCodeGroup/Identity/issues/41) and [#55](https://github.com/NCodeGroup/Identity/issues/55).** The
  OP gains a decoupled backchannel-authentication flow (**poll, ping, and push** delivery) that reuses the device flow's
  pending-authorization-as-persisted-grant pattern with **no new table or EF migration**
  ([ADR-0085](docs/decisions/0085-ciba-grant.md)). A new `backchannel_authentication_endpoint` (`/oauth2/bc-authorize`) accepts
  a strongly-typed `BackchannelAuthenticationRequest` and — like the token and end-session endpoints — is a thin
  orchestrator over mediator commands (`ValidateBackchannelAuthenticationRequestCommand`,
  `AuthenticateBackchannelRequestCommand` → `AuthenticateSubjectDisposition`,
  `CreateBackchannelAuthenticationRequestCommand` → `BackchannelAuthenticationResult`); the
  `urn:openid:params:grant-type:ciba` token grant polls by `auth_req_id` with the same
  `authorization_pending` / `slow_down` / `expired_token` / `access_denied` semantics as the device grant. Login-hint
  resolution is an injectable mediator seam whose default reports `unknown_user_id` (a deployment registers a CIBA
  authentication capability that replaces it); the authentication-device notification (`ICibaNotificationService`) and
  the out-of-band approval (`ICibaRequestService`) are injectable services with safe defaults, mirroring
  `IBackChannelLogoutNotifier`. New settings `backchannel_token_delivery_modes_supported` (default `poll` / `ping` /
  `push`) and `backchannel_user_code_parameter_supported`, plus `ciba_request_lifetime` / `ciba_polling_interval`, are
  advertised in discovery along with the endpoint and grant — which flips the spec-coverage harness's CIBA rows to
  _implemented_ ([ADR-0084](docs/decisions/0084-spec-coverage-conformance-harness.md)). **Push** delivery issues the tokens
  when the user approves on their authentication device and delivers them to the client's registered
  `client_notification_endpoint` (the default `ICibaNotificationService` performs the ping and push `POST`s; the
  authentication-device transport stays a deployment seam), and the token endpoint rejects a push-mode poll. A client
  may also send its parameters as a signed (and optionally encrypted) `request` JWT, validated against the client's keys
  and advertised via `backchannel_authentication_request_signing_alg_values_supported`. New public surface across
  `NCode.Identity.OpenId.Abstractions`,
  `.Messages.Abstractions`, and `.Authentication(.Abstractions)` (grant type, persisted-grant type, endpoint name/path,
  CIBA parameters, `unknown_user_id`, `BackchannelTokenDeliveryModes`, the settings — including
  `client_notification_endpoint` and `backchannel_authentication_request_signing_alg_values_supported` — the
  request/result messages with the `request` parameter, the mediator commands including
  `LoadBackchannelAuthenticationRequestCommand`, and the `ICibaNotificationService` (with push token delivery) /
  `ICibaRequestService` seams).

- **Spec-coverage conformance harness + exercisable consent — advances issue [#129](https://github.com/NCodeGroup/Identity/issues/129).** The conformance surface
  (`apps/conformance`) gained a **spec-coverage matrix** that measures the OP against the full published
  OAuth/OIDC/CIBA/FAPI catalog — independent of what is implemented — so missing functionality (CIBA [#41](https://github.com/NCodeGroup/Identity/issues/41), DPoP/mTLS
  [#45](https://github.com/NCodeGroup/Identity/issues/45), PAR [#46](https://github.com/NCodeGroup/Identity/issues/46), token exchange, pairwise [#47](https://github.com/NCodeGroup/Identity/issues/47), `acr` [#49](https://github.com/NCodeGroup/Identity/issues/49), the FAPI 2.0 profile [#54](https://github.com/NCodeGroup/Identity/issues/54)) surfaces as explicit,
  attributable gap rows classified from the **live discovery document**; because the classifier is pure over discovery,
  the same matrix is a **regression** detector (a capability that stops being advertised, or flips to non-conformant,
  changes its row). It also adds a user-triggered **active-probe** panel (Phase 2) that exercises the browser-reachable
  endpoints — JWKS validity, the RFC 6749 §5.2 token-error contract, UserInfo's `401`, and PAR when advertised — to
  turn _advertised_ into _verified_. Design and the `browser` / `probe-runner` / `external-suite` exercise boundary are
  in [ADR-0084](docs/decisions/0084-spec-coverage-conformance-harness.md). Separately, the Playground flow explorer gained a
  **Force consent (`prompt=consent`)** toggle and the developer-seeded `playground-spa` client now sets
  `require_consent = true`, so the OP's durable, granular consent surface is exercised out of the box; `@ncode/auth`'s
  `buildAuthorizeUrl` gained an optional OIDC `prompt`. New public surface: `DeveloperSpaClientSeed.RequireConsent`.
  The dashboard is organized into **tabs** (Overview · Security audit · Coverage · Probes · Suite config) with
  **collapsible** coverage categories (gaps start expanded) so the growing surface stays scannable; adds
  `@mui/icons-material` to the app. Development/conformance convenience only
  ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Deterministic conformance tenant, clients, and test-user seeding — completes issue [#127](https://github.com/NCodeGroup/Identity/issues/127).** A new
  **conformance-only**, config-driven startup seeder provisions the confidential clients and a fixed test user the
  OpenID Foundation conformance suite drives, into each workload tenant, so a launched host always exposes
  the same identities with **no manual API calls**. `AddConformanceSeed(IConfiguration)` (chained off
  `AddIdentityServer()`) registers an idempotent `SeedTenantCommand` handler modeled on the bootstrap-admin seeder: it
  is inert unless the `ConformanceSeed` configuration section is supplied, re-running never duplicates, and it never
  targets the control-plane root tenant. The new `Conformance` launch profile and `appsettings.Conformance.json` overlay
  seed the three Basic-OP clients (two `client_secret_basic`, one `client_secret_post`, all `S256`) and the
  `conformance-user`. New public surface: `ConformanceSeedRegistration.AddConformanceSeed`, `ConformanceSeedOptions`,
  and `ConformanceClientSeed` (one type per file under a `ConformanceEnvironment/` folder). Client secrets are supplied
  `Base64Url`-encoded and never linger on the GC heap. Must not be used in production. Development/conformance
  convenience only ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Playground flow explorer and conformance posture dashboard — completes issue [#135](https://github.com/NCodeGroup/Identity/issues/135).** The two frontend scaffolds
  became real surfaces on the shared `@ncode/*` platform
  ([ADR-0082](docs/decisions/0082-playground-and-conformance-surfaces.md)). `apps/playground` is an interactive OAuth 2.1 /
  OpenID Connect **authorization-code + PKCE (S256) flow explorer** that discovers the provider and walks the flow end
  to end — surfacing the PKCE verifier/challenge, `state`, `nonce`, authorize URL, token response, and decoded JWT
  header/payload claims, then exercising UserInfo, refresh, and RP-Initiated Logout; implicit, hybrid, and
  resource-owner-password flows are intentionally not offered. `apps/conformance` is a **security-posture &
  conformance-readiness dashboard** that audits the OP's published discovery metadata against modern guidance (OAuth
  2.1, RFC 9700, OpenID Connect Core/Discovery, RFC 7636/8414/7009/7662/9126), flags insecure/deprecated configuration,
  and reports OpenID Foundation Basic/Config OP readiness plus the external-suite configuration (issuer, alias, RP
  callback URL). Both are standalone Vite SPAs wired into the Aspire AppHost (`playground` → `5174`, `conformance` →
  `5175`). The host bootstrap document is now **surface-aware**: `@ncode/config`'s `loadRuntimeConfig` sends the
  bundle's `SurfaceKind` as `?surface=`, and `FrontendBootstrapOptions.Surfaces` maps each surface to a per-surface
  override (client id + scopes **replace** the default so a read-only surface can publish none; feature flags merge;
  branding falls back). `@ncode/auth`'s `buildAuthorizeUrl` gained an optional OIDC `nonce`. **Breaking (pre-release):**
  `DeveloperInteractiveSeedOptions` now seeds a **list** of public SPA clients (`SpaClients`, defaulting to `mgmt-spa`
    - `playground-spa`); the single `SpaClientId`/`SpaRedirectUris`/`SeedSpaClient` members were removed, and the
      developer CORS defaults now include the `5174`/`5175` dev-server origins. New public surface:
      `FrontendBootstrapOptions.Surfaces`, `FrontendSurfaceOptions`, and `DeveloperSpaClientSeed`. The frontend
      bootstrap types (`FrontendBootstrapOptions`, `FrontendSurfaceOptions`, `FrontendBrandingOptions`) live in the
      `NCode.Identity.Server.Frontend` namespace, one type per file under a `Frontend/` folder. Greenfield, pre-release
      — no compatibility shims ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Resource-server runtime reads push filtering into the DBMS — completes issue [#23](https://github.com/NCodeGroup/Identity/issues/23).** The six runtime readers that
  previously paged the tenant's **entire** resource-server set and filtered in memory
  (`DefaultDiscoverScopesHandler`, `DefaultClientScopeService.GetAllowedScopesAsync` / `ResolveAudiencesAsync` /
  `GetEnabledResourceServerIdentifiersAsync`, `DefaultScopeDescriptionService.DescribeScopesAsync`, and
  `DefaultResourceServerCredentialResolver`) now call discrete, intent-named `IResourceServerStore` query methods that
  translate their predicate to SQL ([ADR-0080](docs/decisions/0080-store-query-methods-push-filtering-to-dbms.md)). New public
  surface: `IResourceServerStore.GetEnabledScopesAsync`, `GetEnabledIdentifiersAsync`,
  `GetEnabledScopeProjectionsByScopesAsync`, `GetEnabledScopeProjectionsBySystemOrIdsAsync`, and `FindEnabledByScopesAsync`,
  plus the lightweight `ResourceServerScopeProjection` data contract. The identifier/scope readers no longer load server
  settings or secrets. **Behavior change:** `IScopeDescriptionService.DescribeScopesAsync` now ignores **disabled**
  resource servers, consistent with every other runtime reader. Case-sensitive scope/identifier matching (RFC 6749 / RFC 8707) is preserved: the DBMS filter is a widening pre-filter on the normalized column and the exact match is re-applied
  in memory. Greenfield, pre-release — no compatibility shims
  ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Token-request validation hardening — completes issue [#50](https://github.com/NCodeGroup/Identity/issues/50).** Three related gaps in the authorization-code
  grant validator are closed. (1) **Resource indicators (RFC 8707)** are now a first-class, multi-valued `resource`
  parameter on the authorization and token requests
  ([ADR-0078](docs/decisions/0078-resource-indicators-rfc-8707.md)): the authorization endpoint rejects an unknown or disabled
  resource (`invalid_target`), the authorization-code token endpoint rejects a resource not authorized at the
  authorization endpoint, and `IResourceServerCredentialResolver.ResolveAsync` — the single choke point every grant
  funnels through — **narrows** the access-token `aud` to the requested resources and rejects any that do not resolve to
  an enabled resource server owning one of the token's effective scopes. A new `invalid_target` error code and
  `IOpenIdErrorFactory.InvalidTarget(...)` carry the rejections. (2) The hardcoded PKCE `plain`/`S256` switch becomes an
  extensible **`ICodeChallengeMethod` strategy collection**
  ([ADR-0079](docs/decisions/0079-extensible-pkce-code-challenge-methods.md)); the boolean
  `allow_plain_code_challenge_method` setting is **removed** in favor of the standard, discoverable
  `code_challenge_methods_supported` set (default `["plain", "S256"]`, `Intersect` merge), now advertised in the
  discovery document. (3) Requested **scope values** are confirmed against the live resource-server scope definitions
  (already enforced via `GetAllowedScopesAsync`). New public surface: `OpenIdCommonParameters.Resources`,
  `IBaseAuthorizationRequestValues.Resources`/`IAuthorizationRequest.Resources`/`ITokenRequest.Resources`,
  `CreateSecurityTokenRequest.Resources`, `IClientScopeService.GetEnabledResourceServerIdentifiersAsync`,
  `ICodeChallengeMethod` + `ICodeChallengeMethodCollectionProvider`, and `OpenIdSettingKeys.CodeChallengeMethodsSupported`;
  `IResourceServerCredentialResolver.ResolveAsync` gains a `requestedResources` parameter. Greenfield, pre-release — no
  compatibility shims ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Granular per-scope consent, friendly consent screen, and self-service authorized applications — completes issue
  [#40](https://github.com/NCodeGroup/Identity/issues/40).** Durable consent ([ADR-0076](docs/decisions/0076-consent-persistence-and-enforcement.md)) becomes **granular**: the
  end-user grants a **subset** of the requested scopes and the OP issues only that subset (OIDC down-scoping), reviews
  and revokes standing consent from a new surface, and sees friendly names instead of raw identifiers
  ([ADR-0081](docs/decisions/0081-granular-consent-and-authorized-applications.md)). The consent record now tracks both the
  scopes the subject was asked to **decide** on and the subset they **granted** (`PersistedConsent.RequestedScopes` +
  `ConsentEntity.RequestedScopesJson`, an `AddConsentRequestedScopes` EF migration), so the authorize endpoint prompts
  only when a request introduces a previously-undecided scope and otherwise issues the request ∩ granted intersection;
  `HasConsentAsync` is replaced by `ResolveConsentAsync` returning a `ConsentResolution`, `GrantConsentAsync` takes the
  requested **and** granted sets (re-consent can revoke a scope), and a new mutable `IAuthorizationRequest.GrantedScopes`
  override carries the narrowed set into the authorization code and tokens so the token endpoint cannot re-expand it. The
  consent subsystem resolves the client's `client_name` and each scope's friendly description (a new
  `IScopeDescriptionService`): `ResolvePendingConsentAsync` and the `GET /account/consent/details` payload now carry the
  client display name and per-scope descriptions and already-granted state, the `apps/auth` consent screen renders a
  per-scope checkbox (keeping the `consent-allow` / `consent-deny` locators), and a new OP-hosted authorized-applications
  surface (`/account/applications` page, `/account/applications/list` JSON, and an antiforgery-protected
  `/account/applications/revoke` POST, backed by `IConsentService.GetAuthorizedApplicationsAsync` / `RevokeConsentAsync`)
  lets the end-user review and revoke the applications they have authorized.
- **Session-scoped grant revocation on logout — completes issue [#55](https://github.com/NCodeGroup/Identity/issues/55).** Logout now revokes **only the terminating
  session's grants** instead of every grant the subject holds, so signing out of one login no longer invalidates the
  subject's other sessions ([ADR-0077](docs/decisions/0077-session-scoped-grant-revocation-on-logout.md)). A grant records the
  opaque `sid` of the session it was issued under: `PersistedGrant` (and the typed `PersistedGrant<TPayload>` envelope)
  gains a nullable `SessionId`, `GrantEntity` gains a `SessionId` column and `(TenantId, SessionId)` index (an
  `AddGrantSessionId` EF migration), and the authorization-code, refresh-token, and per-access-token UserInfo-claims
  grants — plus the session grant itself — stamp the `sid` read from the authenticated subject (a new
  `SubjectAuthentication.SessionId` convenience). `IGrantStore.RevokeWhereAsync` and `GetPageAsync` gain an optional
  `sessionId` filter (combined with the existing subject/client filters), surfaced on the management grant list and
  bulk-revoke endpoints, `GrantResource`, and the bulk-revocation audit event;
  `DefaultRevokeGrantsOnEndSessionHandler` revokes by `sid` when a session is resolved and falls back to subject-wide
  revocation only when it is not. Subject-wide "sign out everywhere" remains the deliberate management bulk-revoke
  ([ADR-0022](docs/decisions/0022-grant-filters-and-bulk-revocation.md)). The redundant `ISessionService.RevokeAsync` (which
  revoked only the session grant, leaving its child tokens live) is **removed**.

    records and enforces end-user **consent** ([ADR-0076](docs/decisions/0076-consent-persistence-and-enforcement.md)). Consent
    is a first-class persisted aggregate — a new `PersistedConsent` DTO, `IConsentStore`, and `ConsentEntity` table (with
    an `AddConsents` EF migration) keyed by `(tenant, client, subject)` and **upserted** in place — deliberately **separate
    from the ephemeral grant store** so it survives logout and powers a future "authorized applications" surface. A new
    client **`require_consent`** setting (default off; also forced by `prompt=consent`) makes `DefaultAuthorizeHandler`
    route a subject whose recorded consent does not cover the requested scopes to the interactive consent screen (via a new
    `AuthorizeDisposition.ConsentRequired` and a `ConsentChallengeCommand` seam the interactive module handles), or return
    the OIDC `consent_required` error under `prompt=none`. The interactive surface gains
    `GET /account/consent/details` (resolving the pending request from the return URL without consuming it) and a
    decision-recording `POST /account/consent`, and the `apps/auth` consent screen now shows the **real** client and
    requested scopes. The Playground (`apps/playground`) and OIDC conformance (`apps/conformance`) surfaces are scaffolded
    as apps on the shared `@ncode/*` platform. The unused `InteractiveAuthenticationOptions.AutoApproveConsent` placeholder
    is **removed** (use `require_consent = false`).

- **Runtime consumption of resource-server secrets/settings and uniform resource-server auditing — completes issue
  [#139](https://github.com/NCodeGroup/Identity/issues/139).** Access tokens now consume the per-audience material a resource server owns
  ([ADR-0075](docs/decisions/0075-runtime-consumption-of-resource-server-secrets.md)): at issuance the OP resolves the
  resource servers a token targets (a new `IResourceServerCredentialResolver`) and, when the token targets **exactly
  one** whose settings require it, **encrypts** the token to that resource server's own key so only it can read the
  token — falling back to the tenant's key otherwise. **Signing is always the tenant's asymmetric key** validated via
  JWKS (RFC 9068; aligned with FAPI's `PS256`/`ES256`-only, no-symmetric rule) — a resource server never holds a
  token-signing key. A token spanning several resource servers where any one **requires** encryption is rejected rather
  than silently downgraded. Separately, the resource-server management API now emits audit events uniformly for
  **every** operation (create/update/delete, scopes, settings, and secrets) through `IManagementAuditRecorder`,
  matching the client/tenant/server handlers; new `ResourceServerChangedAuditEvent`, `ResourceServerScopeChangedAuditEvent`,
  and `ResourceServerSecretChangedAuditEvent` carry the non-sensitive snapshots.

- **PostgreSQL developer database orchestrated by Aspire, with a dedicated migration worker — completes issue [#144](https://github.com/NCodeGroup/Identity/issues/144).**
  The host's local database is now **PostgreSQL** run by the Aspire AppHost
  ([ADR-0073](docs/decisions/0073-postgres-dev-database-and-aspire-migration-worker.md)), replacing the in-process
  SQLite/SQL Server/in-memory provider selection. A stable **named data volume** makes data persist across restarts; a
  new `build/dev-db-reset.ps1` (auto-detecting Docker or Podman) purges it **on demand**. A dedicated
  `NCode.Identity.OpenId.Host.MigrationService` worker applies the EF Core migrations and exits, and the `api`
  `WaitForCompletion`s on it — so a migration failure surfaces as a **failed dashboard resource** that blocks the `api`
  instead of crashing it. The migrations and Npgsql wiring move into a new host-owned
  `NCode.Identity.OpenId.Host.Database` project shared by the `api` and the worker. The developer **Data
  Protection key ring** is now persisted in the **same** database (`PersistKeysToDbContext`) rather than a file under
  `App_Data/dp-keys`, so one purge resets the OpenID data, the seeded signing key, and the ring that protects it
  together, with nothing left on the filesystem. A container runtime (Docker or Podman) is now required to run the
  AppHost.

- **End-user sessions, the `sid` claim, and RP-Initiated + Back-Channel Logout — completes issue [#39](https://github.com/NCodeGroup/Identity/issues/39) and the `sid`
  portion of [#49](https://github.com/NCodeGroup/Identity/issues/49).** The OpenID Provider now establishes a first-class, server-side **session** at interactive
  sign-in (persisted as a `PersistedGrant` of type `session` keyed by an opaque `sid`, with no new table or migration),
  mints the `sid` onto the login cookie, and emits the `sid` claim into ID tokens. A new RP-Initiated Logout
  `end_session_endpoint` (self-advertised in discovery) validates the `id_token_hint`, clears the OP login, revokes the
  subject's grants and session, notifies participating relying parties, and redirects to a validated
  `post_logout_redirect_uri` (echoing `state`). **Back-Channel Logout** is delivered by a pluggable
  `IBackChannelLogoutNotifier` whose default implementation builds a signed `logout_token` and `POST`s it,
  server-to-server, to each client's `backchannel_logout_uri`. The endpoint follows the family's discrete-message +
  mediator-pipeline conventions: the request is a typed `IEndSessionRequest` loaded via `LoadEndSessionRequestCommand`,
  and the logout side effects are an extension seam — applications add an `ICommandHandler<OnEndSessionCommand>` (for
  example, custom auditing) alongside the default revocation and back-channel handlers. New per-client settings
  (`post_logout_redirect_uris`, `backchannel_logout_uri`, `backchannel_logout_session_required`) and server discovery
  settings (`backchannel_logout_supported`, `backchannel_logout_session_supported`) are added. The two
  third-party-cookie/iframe mechanisms (Front-Channel Logout and Session Management `check_session_iframe`) are
  **deliberately not** implemented because the browser platform is removing third-party cookies; the decision and the
  forward-looking design are captured in [ADR-0074](docs/decisions/0074-session-management-and-logout.md).
- **End-user and administrator sign-out UX, and a first-party account page on the OP.** Both front-ends gained a
  sign-out control. The **Management console** (`@ncode/app-mgmt`) shows the signed-in subject and a header **Sign out**
  that clears local tokens and redirects to the OP's `end_session_endpoint` (RP-Initiated Logout), replaying the ID
  token as `id_token_hint`; the auth store now persists the ID token (across silent renewals) for exactly this, and the
  logout URL is built by a new `buildEndSessionUrl` helper in `@ncode/auth`. The OP's **interactive surface**
  (`@ncode/app-auth`) gained a first-class account ("me") page at `/account` that fetches `/account/me` (a new
  same-origin JSON endpoint) and renders the current session (subject, `auth_time`, `amr`, `idp`, `sid`) plus a
  **Sign out** control. That control `POST`s (antiforgery-protected) to `/account/logout`, which is hardened from a bare
  cookie-clearing `GET` into a `POST` that runs the **full server-side logout pipeline** — the same `EndSessionCommand`
  used by the `end_session_endpoint`, but cookie-identified (no `id_token_hint`), so a first-party sign-out also revokes
  the session and grants and fires back-channel notifications ([ADR-0074](docs/decisions/0074-session-management-and-logout.md)).
  The `apps/auth` shell now resolves its surface (account/consent/login) through a small **declarative route table**
  instead of nested conditionals.
- **Runtime frontend bootstrap endpoint on the OpenID Provider host — completes issue [#136](https://github.com/NCodeGroup/Identity/issues/136).** The tier-agnostic
  frontend bundles ([ADR-0064](docs/decisions/0064-frontend-presentation-platform.md)) discover their deployment coordinates
  at boot from `/config/bootstrap.json` on their serving host; previously only a static dev copy existed.
  `NCode.Identity.Server` now exposes a first-class
  `IIdentityServerBuilder.AddFrontendBootstrap(configuration)` seam that binds the `Frontend` configuration section
  into `FrontendBootstrapOptions` and registers the endpoint (as an `IEndpointProvider`) that publishes the bootstrap
  document (API base, issuer, client id, scopes, feature flags, branding) — so the host's single `MapEndpointGroups()`
  call maps it alongside every other endpoint. The document falls back to the serving request's own origin for the API
  base and issuer when they are unset, so a single-origin host serves every tier from one bundle with no configuration.
  The host opts in (`AddFrontendBootstrap`) and publishes the dev document (`mgmt-spa`, `dcr` feature flag, dev
  branding). For local development the standalone Management Vite dev server no longer ships a static copy: it
  **proxies** `/config/bootstrap.json` to the host (the Aspire service-discovery target injected via
  `WithReference(api)`, [ADR-0071](docs/decisions/0071-aspire-local-dev-orchestrator.md)), so local dev consumes the real
  runtime endpoint and cannot drift.
- **Algorithm capabilities discovery drives the create-secret form — completes issue [#164](https://github.com/NCodeGroup/Identity/issues/164).** The management
  capabilities document ([ADR-0067](docs/decisions/0067-secret-type-capability-catalog-and-capabilities-discovery.md)) grows
  an `algorithms` section published by a new `AlgorithmCapabilitiesProvider`
  (`ICommandHandler<DiscoverManagementCapabilitiesCommand>`). It projects every supported signature and key-management
  algorithm from the live `IAlgorithmCollectionProvider` as an `AlgorithmCapabilityResource` carrying its `code`, the
  implied `use` (`sig`/`enc`), the compatible secret-type discriminators (cross-referenced against the
  `ISecretKeyTypeCatalog`), and its legal `keyBitSizes` ranges — so the console create-secret form drives its
  **algorithm**, **use**, and **key-size** inputs from discovery instead of free-text defaults: the algorithm picker
  offers only algorithms compatible with the chosen secret type, selecting one derives the read-only use and clamps the
  key size into the algorithm's legal range (min/max/step). A new capability is still added only by registering a
  provider, never by editing the endpoint or resource type.
- **Server create/edit management across the stack — completes issue [#141](https://github.com/NCodeGroup/Identity/issues/141).** The Servers surface is no longer
  read-only: an OpenID server now carries an editable top-level **display name**, and the management console lists
  servers with a **Create server** action plus row-level **edit/delete** (mirroring tenants and resource servers) in
  addition to the existing drill-in to a server's secrets/settings. Backend: `ServerResource`/`CreateServerRequest`
  gain `DisplayName`, a new `UpdateServerRequest` + `PATCH /api/servers/{serverId}` endpoint lands (JSON Patch with
  `If-Match`, validated by `IServerValidator.ValidateUpdateAsync`), and `DisplayName` is persisted on `PersistedServer`
  (and the EF `ServerEntity`) and round-tripped through `IServerStore`. Audit reaches parity with the other resources:
  a new `ServerChangedAuditEvent` (`ManagementResourceTypes.Server`) and `IManagementAuditRecorder.RecordServerChangedAsync`
  record **create/update/delete** (servers are the `GlobalAdmin`-only control plane, so the event carries no tenant).
  Frontend: new `createServer`/`updateServer`/`deleteServer` RTK Query mutations and a `ServerFormDialog`.
- **Public-key export, a secret-type capability catalog, and composable capabilities discovery
  ([ADR-0067](docs/decisions/0067-secret-type-capability-catalog-and-capabilities-discovery.md)).** The management console
  can now export an asymmetric secret's **public key** (JWK + PEM, copy/download) from the secrets table; the action
  is gated by server discovery, not a hard-coded list. Server-side, a single `ISecretKeyTypeCatalog` (core
  `NCode.Identity.Secrets`) becomes the one source of truth for the secret key-type taxonomy, key-size validation,
  key generation, and public-key-export capability — the generator, validator, and public-key converter all consult
  it and their per-type `switch` statements are deleted. PEM export moves into the domain
  (`AsymmetricSecretKey.ExportAsymmetricAlgorithm()` + base `ExportSubjectPublicKeyInfoPem()`). A new
  `GET /api/capabilities` publishes a `[JsonExtensionData]` discovery document composed — like the OpenID discovery
  document — from registered `ICommandHandler<DiscoverManagementCapabilitiesCommand>` providers, so new capabilities
  are exposed by registering a provider rather than editing the endpoint.
- **Unified, extensible elliptic-curve catalog with per-request curve selection
  ([ADR-0068](docs/decisions/0068-unified-extensible-elliptic-curve-catalog.md)).** A single core `IEccCurveCatalog`
  replaces the generator's hard-coded `bits → curve` switch **and** the authentication-layer
  `IEccCurveSpecificationRegistry`; curves are selected by identity (name/OID), not by size, so same-size curves
  (for example `secp256k1` vs `P-256`) are disambiguated for both generation and JWK `crv` naming. `CreateSecretRequest`
  gains an optional `curve`, generation flows through a growable `SecretKeyGenerationParameters`, the JWKS converter
  names `crv` from the key's actual curve, the capabilities document exposes an `eccCurves` section, and the console
  create-secret form offers a server-driven curve picker. Applications add a curve by registering one
  `EccCurveSpecification`.

- **Management console resource-server secrets/settings (`apps/mgmt`) — completes issue [#134](https://github.com/NCodeGroup/Identity/issues/134).** Selecting a resource
  server (API) in a tenant scope now drills into a `ResourceServerDetailPage` with **Scopes**/**Secrets**/**Settings**
  tabs, reusing the shared owner-agnostic `SecretsManager`/`SettingsManager` containers so the resource server behaves
  exactly like clients/tenants/the server. The tabs wire the backend owner endpoints added in
  [ADR-0066](docs/decisions/0066-resource-server-settings-and-secret-owner.md) via new scoped RTK Query
  endpoints (`listResourceServerSecrets`/`createResourceServerSecret`/`deleteResourceServerSecret`,
  `getResourceServerSettings`/`getResourceServerEffectiveSettings`/`updateResourceServerSettings`), honoring the
  one-time secret reveal ([ADR-0050](docs/decisions/0050-secret-material-revealed-once-on-create.md)) and the top-level
  add/remove/replace JSON Patch `diffPatch` save with the owner `concurrencyToken` as `If-Match`. This closes [#134](https://github.com/NCodeGroup/Identity/issues/134)
  (management console secrets/settings for every backend-supported owner + the initial-access-token mint); runtime
  consumption of resource-server secrets and resource-server audit events remain tracked under [#139](https://github.com/NCodeGroup/Identity/issues/139).
- **Resource servers are first-class settings and secret owners (backend).** Completes the backend half of issue
  [#134](https://github.com/NCodeGroup/Identity/issues/134): a resource server (an API, in Auth0 terms) now owns a wrapped settings object (its own concurrency token) and a
  secret collection, structurally identical to clients/tenants/servers. New management endpoints —
  `GET`/`PATCH api/tenants/{tenantId}/resource-servers/{id}/settings`, `GET .../effective-settings` (server → tenant →
  resource-server merge), and the full `.../secrets` CRUD honoring the one-time reveal
  ([ADR-0050](docs/decisions/0050-secret-material-revealed-once-on-create.md)) with optimistic concurrency
  (`concurrencyToken` → `If-Match`). The persistence model gains `PersistedResourceServerSettings`/`…Secrets` (and a
  `BasePersistedResourceServerResource` base, `ISupportResourceServerId`, the `resource_server` resource type),
  `IResourceServerStore` gains `UpdateSettingsAsync` + secret CRUD, and EF Core adds
  `SettingsConcurrencyToken`/`SecretsConcurrencyToken` columns and a tenant-scoped `ResourceServerSecrets` join table
  (migration `AddResourceServerSecrets`); deleting a resource server cascades its scopes and secrets. A
  `resource_server_secrets` scope family is seeded. Rationale and the "an API _is_ a secret owner (per-audience HS256 /
  JWE material)" decision: [ADR-0066](docs/decisions/0066-resource-server-settings-and-secret-owner.md). Runtime consumption
  of resource-server secrets and resource-server audit events are deferred (see [GitHub issues](https://github.com/NCodeGroup/Identity/issues)).
- **Management console secrets/settings for tenants + the server, and initial-access-token mint (`apps/mgmt`).**
  Extends the client **Secrets**/**Settings** pattern to the remaining backend-supported settings/secret owners and
  surfaces protected dynamic-client-registration bootstrap. The client secrets/settings tab logic was factored into
  shared, owner-agnostic `SecretsManager`/`SettingsManager` containers (the client detail view now consumes them).
  **Tenant:** a tenant-scope **Tenant** tab manages the tenant's own secrets and settings
  (`listTenantSecrets`/`createTenantSecret`/`deleteTenantSecret`, `getTenantSettings`/`getTenantEffectiveSettings`/
  `updateTenantSettings`). **Server:** a global-scope **Servers** tab (new stateless MUI `ServerTable`) drills into a
  server to manage its secrets and settings (`listServerSecrets`/`createServerSecret`/`deleteServerSecret`,
  `getServerSettings`/`getServerEffectiveSettings`/`updateServerSettings`). Both honor the one-time secret reveal
  ([ADR-0050](docs/decisions/0050-secret-material-revealed-once-on-create.md)) and the top-level add/remove/replace JSON
  Patch `diffPatch` save with the owner `concurrencyToken` as `If-Match`. **Initial access token (RFC 7591):** the
  **Grants** tab gained a **Mint initial access token** action (`createInitialAccessToken`, `Grant`-tag invalidation)
  with a form (optional description + lifetime) and a one-time `RevealInitialAccessTokenDialog` that shows the plaintext
  token exactly once. Fourth slice of issue [#134](https://github.com/NCodeGroup/Identity/issues/134); the only [#134](https://github.com/NCodeGroup/Identity/issues/134) remnant is resource-server (API) settings/secrets,
  which is blocked on backend owner endpoints that do not yet exist.
- **Management console grants, secrets, and settings write operations (`apps/mgmt`).** Advances issue [#134](https://github.com/NCodeGroup/Identity/issues/134) by
  extending the shared platform to the remaining management surfaces. **Grants:** a new tenant **Grants** tab lists
  persisted grants with subject/client filters and revokes them one-by-one or in a filtered bulk operation
  (`listGrants`/`revokeGrant`/`revokeGrants` with `Grant`-tag invalidation; the server requires a bulk revoke to carry
  at least one filter). Grants are read-only administratively, so only active grants expose a revoke control. **Client
  secrets:** selecting a client drills into a detail view whose **Secrets** tab creates/lists/deletes secrets
  (`listClientSecrets`/`createClientSecret`/`deleteClientSecret`), honoring the one-time reveal — the generated key
  material is shown exactly once in a `RevealSecretDialog` with a copy affordance and never re-fetched
  ([ADR-0050](docs/decisions/0050-secret-material-revealed-once-on-create.md); asymmetric secrets have no material to
  reveal). **Client settings:** the detail view's **Settings** tab edits the opaque persisted settings JSON alongside a
  read-only effective-settings view (`getClientSettings`/`getClientEffectiveSettings`/`updateClientSettings`); because
  settings are an open object, saves send a top-level add/remove/replace JSON Patch diff (a new UI-free `@ncode/api`
  `diffPatch` helper) with the owner `concurrencyToken` as `If-Match`. New stateless MUI `GrantTable`/`SecretTable`/
  `SecretFormDialog`/`RevealSecretDialog`/`SettingsEditor` (all Storybook-driven and unit-tested). All client-owned
  surfaces are addressed on the tenant-nested path so a tenant administrator can never reach another tenant.
- **Management console resource-server + scope write operations (`apps/mgmt`).** Within a tenant scope the console now
  creates/edits/deletes resource servers (APIs) and manages their nested scopes (permissions): scoped RTK Query
  mutations (`createResourceServer`/`updateResourceServer`/`deleteResourceServer` and `createScope`/`updateScope`/
  `deleteScope`) with `ResourceServer`-tag invalidation, stateless MUI `ResourceServerTable`/`ScopeTable` with row
  actions and `ResourceServerFormDialog`/`ScopeFormDialog`, reusing the shared `ConfirmDialog`. Resource servers embed
  their scopes inline, so selecting one drills into a scopes view fed by the same list query (a scope mutation
  refreshes both); resource-server edits honor optimistic concurrency (`concurrencyToken` → `If-Match` over a
  `replace`-only JSON Patch) while a scope update is a whole-resource `PUT` (the value is immutable), and system-owned
  resource servers/scopes expose no delete control. The tenant workspace switches between **Clients** and **Resource
  servers** via tabs. All addressed on the tenant-nested path so a tenant administrator can never reach another tenant.
  Third slice of issue [#134](https://github.com/NCodeGroup/Identity/issues/134).
- **Management console client write operations (`apps/mgmt`).** Within a tenant scope the console now creates/edits/
  deletes clients: scoped RTK Query mutations (`createClient`/`updateClient`/`deleteClient`) with `Client`-tag
  invalidation, a stateless MUI `ClientTable` (id + status + row actions) and `ClientFormDialog`, reusing the shared
  `ConfirmDialog`. Writes honor optimistic concurrency (`concurrencyToken` → `If-Match` over a `replace`-only JSON
  Patch) and the tenant-nested path (`api/tenants/{tenantId}/clients/...`), so a tenant administrator can never address
  another tenant. Second slice of issue [#134](https://github.com/NCodeGroup/Identity/issues/134).
- **Management console tenant write operations (`apps/mgmt`).** The console gained full tenant create/edit/delete on
  the shared frontend platform: RTK Query mutations (`createTenant`/`updateTenant`/`deleteTenant`) with tenant-scoped
  cache-tag invalidation, a stateless MUI `TenantFormDialog` and reusable `ConfirmDialog` (Storybook-driven), and
  row-level edit/delete actions on the tenant table. Edits honor optimistic concurrency by sending the resource's
  `concurrencyToken` as an `If-Match` header over a `replace`-only JSON Patch document; a UI-free `replacePatch` helper
  (`@ncode/api`) and an RFC 7807-aware `apiErrorMessage` helper (`@ncode/store`) are unit-tested directly. First slice
  of issue [#134](https://github.com/NCodeGroup/Identity/issues/134).
- **Interactive login/consent surface (`apps/auth`, OP-hosted).** The OpenID Provider gained its first interactive
  surface: a React + Material UI login/consent app (`@ncode/app-auth`) on the shared frontend platform, served by the
  host as same-origin static assets under `/account/*`, backed by an opt-in leaf capability package
  `NCode.Identity.OpenId.Authentication.Interactive` (+ `.Abstractions`)
  (`AddInteractiveAuthentication` / `UseInteractiveAuthentication`). Its endpoints register as an
  `IEndpointProvider` under an `InteractiveAuthenticationEndpointGroup`, so the host's single `MapEndpointGroups` call
  maps the `/account/*` surface alongside every other endpoint (no bespoke mapping extension). It registers a
  cookie scheme (`Identity.Application`) that the authorize endpoint's challenge redirects into, defers credential
  verification to a pluggable `IInteractiveCredentialVerifier` seam (the local-account implementation,
  `AddLocalAccountInteractiveAuthentication()`, reuses the same path the resource-owner password grant uses), issues the
  cookie session, and honors a server-validated local return URL. The forms expose stable automation ids (`username`,
  `password`, `login`, `consent-allow`, `consent-deny`). This resolves the rendering and first-class-vs-dev choices left
  open by [ADR-0062](docs/decisions/0062-opt-in-interactive-login-consent-ui.md); the surface is hosted per
  [ADR-0065](docs/decisions/0065-interactive-login-consent-op-hosted-auth-surface.md) and extracted into its own leaf package
  with the pluggable verifier seam per
  [ADR-0070](docs/decisions/0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md). Issues [#125](https://github.com/NCodeGroup/Identity/issues/125), [#135](https://github.com/NCodeGroup/Identity/issues/135).
- **Silent access-token renewal for the Management console.** `@ncode/auth` gained a framework-agnostic renewal loop
  (`createTokenRenewer`) that refreshes the access token ahead of expiry, re-arms itself from each new token, and falls
  back to interactive sign-in when renewal is no longer possible; the console's `AuthGate` drives it while signed in.
  Issue [#133](https://github.com/NCodeGroup/Identity/issues/133).
- **Development seeding for end-to-end sign-in (`AddDeveloperInteractiveSeed`).** A development-only, idempotent seeder
  provisions a public (PKCE-only) single-page-application client (`mgmt-spa`, with redirect URIs) and a deterministic
  administrator user into each workload tenant, so the authorization-code + PKCE flow and the management console sign in
  end to end. Inert unless configured; the administrator password has no default. Issues [#139](https://github.com/NCodeGroup/Identity/issues/139), [#133](https://github.com/NCodeGroup/Identity/issues/133).

- **Management console sign-in (authorization-code + PKCE).** The frontend gained a framework-free `@ncode/auth`
  package implementing OAuth 2.0 authorization-code with PKCE (S256) — verifier/challenge generation, the authorize
  redirect, state/CSRF validation, code-for-token exchange, refresh, and JWT subject extraction, all injectable and
  unit-tested (including the RFC 7636 test vector). The Management console (`@ncode/app-mgmt`) wires an `AuthGate` that
  completes the redirect callback for a `urn:ncode:management`-audience token and populates the auth slice, or renders a
  sign-in screen to start the flow; when no client id is discovered it runs open (local dev). The SPA's public client
  id and login scopes are discovered at runtime via the bootstrap document (tier-agnostic). Issue [#133](https://github.com/NCodeGroup/Identity/issues/133).
- **One-stop full-stack dev launch via .NET Aspire ([ADR-0071](docs/decisions/0071-aspire-local-dev-orchestrator.md)).** A
  sibling AppHost project (`NCode.Identity.OpenId.LocalDev`) orchestrates the whole local stack — the
  OpenID host host (`api`), the interactive-login build-watcher (`auth`), and the Management console Vite dev server
  (`mgmt`) — as one Aspire resource graph behind the `Aspire: Full stack (F5)` launch. Aspire gates each UI resource on
  the API being **healthy** (`WithHttpHealthCheck("/health")` + `WaitFor`), deterministically eliminating the previous
  compound launch's UI-before-API race, and its dashboard is one place for ordered startup, logs, and traces. Ports
  stay pinned (`api` on `https://localhost:5001`, `mgmt` on `http://localhost:5173`, un-proxied) so the developer seed
  redirect URI, developer CORS origins, and the browser bootstrap document stay valid; `apps/auth` is modeled as a
  build-watcher (no endpoint) whose `dist` the host serves. The AppHost is never packed. This replaces the VS
  Code `API + UI (F5)` compound; the old `build/dev-ui.ps1` is relegated to `build/frontend-install.ps1`, a one-time
  pnpm-workspace bootstrap, now that Aspire owns `pnpm dev`. Backing SQL Server/Redis remain opt-in (SQLite/in-memory
  stays the default, so there is no container-runtime prerequisite). Issue [#143](https://github.com/NCodeGroup/Identity/issues/143).
- **Opt-in developer CORS for the OpenID server (`AddDeveloperCors` / `UseDeveloperCors`).** A reusable
  development-oriented registration (in `NCode.Identity.Server`, beside `AddDeveloperKeys`) lets a local SPA dev server
  read discovery and call the management API cross-origin. Allowed origins come from the `Cors:AllowedOrigins`
  configuration array, defaulting to the local Vite dev/preview origins in Development; it is a no-op otherwise. The
  host opts in with a single call.

- **Frontend presentation platform (`src/frontend`).** Introduced a single cohesive TypeScript UI platform shared by
  every surface (Management, Auth/Consent/Login, Playground, OIDC conformance): React + Material UI, Redux Toolkit +
  RTK Query, Vite, Vitest + React Testing Library, and Storybook, orchestrated by pnpm workspaces + Turborepo and
  pinned via Corepack. Shared `@ncode/*` packages (`config` runtime discovery, `api` RTK Query client + global/tenant
  scope model, `store` Redux assembly, `ui` MUI design system) back thin per-surface app shells; the first is the
  Management console (`@ncode/app-mgmt`) with global-vs-tenant boundary browsing. Bundles are tier-agnostic — all
  configuration is discovered at runtime from the serving host, nothing is baked at build time — and the branding seam
  is in place for future per-tenant theming. The toolchain is isolated under `src/frontend` and does not touch the
  .NET Definition of Done. See [ADR-0064](docs/decisions/0064-frontend-presentation-platform.md).

- **Initial access token provisioning for protected Dynamic Client Registration.** The management API can now mint the
  `initial_access_token` that protected registration (the default, `registration_endpoint_allow_anonymous = false`)
  requires: a tenant-scoped `POST api/tenants/{tenantId}/grants/initial-access-tokens` creates a hash-keyed
  `initial_access_token` grant (optional `description` and `lifetimeSeconds`) and returns the one-time plaintext token
  — revealed once, never retrievable again — together with the grant id for later revocation through the grant
  management endpoint, recording an `InitialAccessTokenCreatedAuditEvent`. This closes the provisioning gap in
  [ADR-0063](docs/decisions/0063-dynamic-client-registration.md): a deployment that wants protected registration no longer
  has to seed grants out of band or replace the `IInitialAccessTokenValidator` seam. See
  [ADR-0063](docs/decisions/0063-dynamic-client-registration.md).
- **OAuth 2.0 Dynamic Client Registration (RFC 7591/7592).** Clients can now register themselves and manage their own
  registration. A new `registration_endpoint` (`POST /oauth2/register`), advertised in discovery, accepts a JSON
  client-metadata document and returns the assigned `client_id`, a one-time `client_secret` for a confidential client,
  and a `registration_access_token` plus `registration_client_uri`; the per-client management endpoint
  (`GET`/`PUT`/`DELETE /oauth2/register/{client_id}`, RFC 7592) lets the client read, replace, or delete its
  registration using that token. Metadata maps onto the existing client settings document and the `client_secret` is
  the existing one-time secret, so client authentication works unchanged; both the `registration_access_token` and the
  RFC 7591 `initial_access_token` are modeled as hash-keyed persisted grants — no new schema. Registration is
  configurable and protected by default: a new `registration_endpoint_allow_anonymous` setting (default `false`, merged
  with the boolean meet so it cannot be loosened downward) governs whether an initial access token is required, and the
  deployment-specific `IInitialAccessTokenValidator` seam decides what counts as a valid initial access token. New
  error codes `invalid_client_metadata` and `invalid_redirect_uri` are added. See
  [ADR-0063](docs/decisions/0063-dynamic-client-registration.md).
- **OAuth 2.0 Device Authorization Grant (RFC 8628).** Input-constrained clients (smart TVs, CLIs, IoT devices) can now
  obtain tokens by having the end user authorize on a second device. A new `device_authorization_endpoint`
  (`POST /oauth2/device_authorization`) issues a `device_code`, a human-typable `user_code`, a `verification_uri`, and
  a polling `interval`; a user-facing verification endpoint (`/oauth2/device`) authenticates the end user through the
  existing subject-authentication seam and records the approve/deny decision; and the
  `urn:ietf:params:oauth:grant-type:device_code` token grant polls the request, returning `authorization_pending`,
  `slow_down`, `expired_token`, or `access_denied` until it is approved and redeemed for tokens exactly once. The
  pending request is persisted as a `PersistedGrant` payload keyed by `device_code` with a companion `user_code`
  pointer grant — no new schema — enabled by a new general-purpose `IPersistedGrantService.UpdatePayloadAsync`. New
  settings `device_code_lifetime`, `device_code_polling_interval`, and `user_code_length` tune the flow, and
  `user_code` generation is a replaceable `IUserCodeGenerator` seam. The endpoint and grant type are advertised in
  discovery automatically. See [ADR-0059](docs/decisions/0059-device-authorization-grant.md).
- **Asymmetric secret public-key read endpoints.** A new read-only sub-resource,
  `GET /api/.../{servers|tenants|clients}/{ownerId}/secrets/{secretId}/public-key`, returns an asymmetric secret's
  **public** key material as a `PublicKeyResource` carrying both a JWK (RFC 7517, the same representation the JWKS
  endpoint publishes, via the key's `ISecretKeyTypeDescriptor.ExportPublicKey` seam) and a SubjectPublicKeyInfo PEM
  (`-----BEGIN PUBLIC KEY-----`) for copy-paste into an admin UI or crypto tooling. Private key material is never
  exposed; a symmetric secret has no public-key sub-resource and responds `404 Not Found`. The read is gated by the
  same `Operations.Read` authorization as reading the secret. See
  [ADR-0058](docs/decisions/0058-asymmetric-public-key-material-read-path.md).
- **Local-account management API.** A new `local-accounts` resource family under
  `/api/tenants/{tenantId}/local-accounts` lets a tenant/global administrator create, list, get, update, delete,
  disable, enable, reset the credential of, manage the profile claims of, and read/set the server-owned metadata of a
  local account. Account creation eagerly provisions the owning `FederatedPrincipal` and its one-to-one self-issued
  `FederatedIdentity` in the same unit of work, so principal-level metadata (`ProfileMetadata` / `SystemMetadata`) has a
  stable target from the moment the account exists; the metadata endpoint is the administrator-gated write path that
  upholds the "`SystemMetadata` is never end-user-writable" invariant. Local accounts are end-user subjects rather than
  ownable resources, so every operation authorizes against the owning tenant scope. The endpoints require a registered
  `ILocalAccountStore` and otherwise respond `501 Not Implemented`. Changes are audited via
  `LocalAccountChangedAuditEvent` and gated by `*:local_accounts` scopes. See
  [ADR-0060](docs/decisions/0060-local-account-one-store-seam-and-dependency-inverted-authentication.md) and
  [ADR-0057](docs/decisions/0057-local-account-eager-provisioning-and-management-seam.md).
- New dependency-free **`NCode.Json`** package holding the shared `JsonElements` helper (`EmptyObject` /
  `OrEmptyObject` / `IsNullOrUndefined`, namespace `NCode.Json`), promoted out of `NCode.Identity.Abstractions` so the
  persistence layer (and any package beneath `NCode.Identity.*`) can reuse it without inheriting that package's ASP.NET
  Core framework reference. See [ADR-0055](docs/decisions/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md).
- Two new abstractions packages split out of `NCode.Identity.Abstractions`:
  **`NCode.Identity.AspNetCore.Abstractions`** (the ASP.NET Core HTTP surface — `UriDescriptor`, `HttpResultException`,
  and the `Results/*` contracts; it now carries the `Microsoft.AspNetCore.App` framework reference) and
  **`NCode.Identity.Settings.Abstractions`** (the `NCode.Identity.Settings.*` subsystem). Namespaces are unchanged, so
  the move is transparent to source. See
  [ADR-0056](docs/decisions/0056-abstractions-split-into-framework-free-tiered-packages.md).

### Fixed

- **The token endpoint now sends `Cache-Control: no-store` on every response (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** RFC 6749 §5.1/§5.2
  require the token endpoint to return `Cache-Control: no-store` (and `Pragma: no-cache`) so issued tokens are never
  cached by the client or an intermediary; the OP omitted them. Both headers are now set up front so the success and
  error responses carry them. Surfaced by `oidcc-refresh-token` (its `CheckTokenEndpointCacheHeaders` check) once the
  consent fix below let the refresh flow run to the refresh request.

- **The consent screen now grants the requested scopes instead of silently dropping them (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** The no-JS
  consent form and the SPA consent form rendered each requested scope's checkbox pre-checked only when it was
  _previously_ granted, so on a first-time consent every box was unchecked and clicking **Allow** posted an empty
  `scope` set — the recorded grant (and therefore the issued tokens) lost `openid`, `offline_access`, and every other
  requested scope, so the token response omitted the `id_token` and `refresh_token`. A consent screen now pre-selects
  all requested scopes (Allow authorizes the request; the subject unchecks a scope to down-scope). Surfaced by the
  OpenID Foundation Conformance Suite's `oidcc-refresh-token` Basic OP module (it uses `prompt=consent` +
  `offline_access`).

- **Authorization error responses created outside the command pipeline now echo `state` (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** The
  `prompt=none` → `login_required` and the no-consent-surface → `interaction_required` errors are built inline in the
  authorization endpoint (not via the `AuthenticateCommand` / `AuthorizeCommand`, whose post-processors stamp `state`),
  so they were returned to the client without the request's `state` — violating OIDC Core §3.1.2.6. Both now carry the
  request `state`. Surfaced by `oidcc-prompt-none-not-logged-in`.

- **`max_age` / `subject_max_age` authentication-age freshness is enforced only at the authorization endpoint, with no
  clock-skew leeway (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** Two defects compounded: (1) the freshness check added the client's `clock_skew`
  (default 5 min, intended for validating token time claims across parties) to the window, so a small `max_age` such as
  `max_age=1` never forced re-authentication; and (2) the same check was re-evaluated at the token endpoint (and on
  refresh) against a _later_ clock, so once the (un-inflated) window elapsed a valid authorization-code / refresh
  exchange was wrongly rejected with `access_denied`. The check now uses a pure OP-internal elapsed-time comparison (no
  skew) and runs only in the interactive authorization step via a new
  `ValidateSubjectAuthenticationCommand.EnforceAuthenticationAge` flag (`true` at the authorization endpoint; `false`
  for the authorization-code, refresh-token, and password grants). Surfaced by `oidcc-max-age-1`. **Public surface:**
  `ValidateSubjectAuthenticationCommand` gains a required `EnforceAuthenticationAge` parameter.

- **A value-type setting with no explicit default no longer materializes `default(TValue)` into the settings lattice,
  which had intermittently forced `prompt=none` to return `login_required` (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** `SettingDescriptor<TValue>`
  reported `HasDefault` as `DefaultOrNull is not null`, but for a value-type `TValue` (e.g. `TimeSpan`) the
  `notnull`-constrained `TValue?` is **not** `Nullable<TValue>`, so `DefaultOrNull` was never null and `HasDefault` was
  always `true`. The root settings data source therefore loaded `default(TValue)` for every value-type descriptor that
  declared no explicit default — most consequentially `subject_max_age`, which materialized as `TimeSpan.Zero`, a
  zero-second freshness window that rejected every just-completed authentication and returned `login_required` to a
  logged-in `prompt=none` request. `HasDefault` now tracks an explicit `_hasDefault` flag set only when a default is
  actually supplied, so an unset value-type setting stays genuinely absent (`TryGetValue` returns `false`) and imposes
  no constraint. Surfaced by the intermittent `oidcc-prompt-none-logged-in` / `oidcc-id-token-hint` conformance
  failures.

- **A rejected client request (4xx `OpenIdException`) is logged at Warning, not Error (issue [#131](https://github.com/NCodeGroup/Identity/issues/131)).** The OpenID
  exception handler logged every unhandled exception — including an expected client-side validation failure such as an
  unregistered `redirect_uri` (`unauthorized_client`, HTTP 400) — at Error level as "an unexpected error", polluting
  logs and tripping false alerts. A 4xx-status `OpenIdException` now logs at Warning with its error code; only genuine
  5xx / unexpected faults remain at Error. Surfaced by `oidcc-ensure-registered-redirect-uri`.

    "create client secret" endpoint generated a symmetric secret via `ISecretGenerator.GenerateSecret`, which stores the
    raw CSPRNG bytes and reveals their `base64url` form — but client authentication compares the **UTF-8 bytes of the
    presented secret** against the stored key material (and `client_secret_jwt` keys its HMAC from those same UTF-8
    octets), so `UTF8(base64url(raw)) ≠ raw` meant every generated secret failed with `invalid_client`. A new
    `ISecretGenerator.GenerateSharedSecret` persists the opaque secret string as its UTF-8 bytes (the representation a
    verifier compares against); DCR and the management client-secret path now use it, while asymmetric / server-side
    cryptographic keys keep `GenerateSecret` ([ADR-0110](docs/decisions/0110-shared-secret-vs-cryptographic-key-storage.md)).
    Surfaced by the OpenID Foundation Conformance Suite's Basic OP profile under Dynamic Client Registration.

- **Dynamic Client Registration deregistration (`DELETE /oauth2/register/{client_id}`) no longer fails for a client
  that still has a secret.** `ClientStore.RemoveAsync` guarded against orphaning secrets with a fresh
  `ClientSecrets.AnyAsync(…)` **database** query, which could not see the secret rows the same unit of work had already
  marked for deletion (the deregistration handler removes the client's secrets, then the client, then saves once) — so
  the guard wrongly reported the client "still has secrets" and the delete threw `server_error`. The guard is now
  change-tracker-aware: it ignores secrets already marked `Deleted` in the current `DbContext`. Surfaced by the OpenID
  Foundation Conformance Suite's Basic OP profile under Dynamic Client Registration.

- **`expires_in` is now serialized as a JSON number.** The token response is written in the OpenID wire format, where
  the generic value parsers emit every value as a JSON string — so `expires_in` (a `TimeSpan?`) was emitted as
  `"3600"` instead of `3600`, violating OAuth 2.0 §5.1. A new `DurationSecondsParser` (exposed as
  `CommonParameterParsers.DurationSeconds`) reads and writes a seconds-valued duration as a JSON number; `expires_in`
  now uses it. Surfaced by the OpenID Foundation Conformance Suite's Basic OP profile.

- **`prompt=login` / `prompt=select_account` / `prompt=create` no longer loop.** `DefaultAuthorizeHandler`
  unconditionally re-challenged for these prompts, so after the user completed the interactive step the resumed
  continuation challenged again indefinitely. They are now guarded by `!IsContinuation` — the same pattern
  `prompt=consent` already uses ([ADR-0081](docs/decisions/0081-granular-consent-and-authorized-applications.md)) — so the
  re-authentication happens exactly once (the conformance suite confirms the second id_token's `auth_time` advances).

- **Authorization error/success responses no longer crash when the request omitted `state`.** `AuthorizationResult`
  asserted `error.State`/`ticket.State is not null`, but `state` is echoed back only when the client sent it (OAuth 2.0
  §4.1.2.1) — so a request without `state` (several conformance `prompt`/negative tests) terminated the process in a
  Debug build. The assertions are removed; the result executor already omits a null `state` from the response. Surfaced
  by the OpenID Foundation Conformance Suite's Basic OP profile.

- **Discovery metadata no longer advertises the non-standard `discovery_endpoint` parameter.** The discovery endpoint
  was marked discoverable and so listed itself in its own `/.well-known/openid-configuration`, but `discovery_endpoint`
  is not defined by OpenID Connect Discovery 1.0 or RFC 8414. It is now marked non-discoverable. Surfaced by the OpenID
  Foundation Conformance Suite's `CheckForUnexpectedParametersInServerMetadata` (Config OP profile).

- **Discovery metadata no longer advertises the non-standard `backchannel_authentication_request_encryption_alg_values_supported`
  / `…_enc_values_supported` parameters.** CIBA Core defines only the **signing** alg metadata for the backchannel
  authentication request; there is no standardized encryption-metadata discovery parameter (unlike JAR's
  `request_object_encryption_*`). These two were mis-classified as standard-discoverable and are now non-discoverable,
  matching the already-non-discoverable `…_zip_values_supported` sibling — the encrypted-CIBA-request capability is
  still honored, just not advertised via a non-standard parameter. Also surfaced by the Config OP profile.

- **`client_credentials` (and `password`) token requests with an omitted `scope` no longer crash the token endpoint.**
  The `scope` parameter is OPTIONAL (RFC 6749 §3.3 / §4.4), but `DefaultClientCredentialsGrantHandler` and
  `DefaultPasswordGrantHandler` both asserted `scopes is not null` — an invariant that did not hold when the client sent
  no `scope`, terminating the process in a Debug build. Both handlers now default an omitted scope to an empty set at
  the point of use (issuing a no-scope token rather than failing). The fix is deliberately local to these two grants —
  which have no prior grant to inherit scopes from — so the meaningful null-vs-empty distinction is preserved for the
  refresh-token and token-exchange grants, where an omitted `scope` reuses the original grant's scopes. Surfaced by the
  in-house conformance probe runner's `client_credentials` probe.

- **Resource-owner-password tickets now carry the authentication's `AuthenticationProperties` from the start of the
  token-request flow instead of a fresh empty instance (completes issue [#62](https://github.com/NCodeGroup/Identity/issues/62)).** `DefaultAuthenticatePasswordGrantHandler`
  previously built its `SubjectAuthentication` with a freshly-constructed empty `new AuthenticationProperties()`, so the
  issued-authentication metadata the other entry points propagate (notably `IssuedUtc`, which drives the subject's
  `auth_time`) was dropped on the ROPC path. `DefaultPasswordGrantHandler` now captures the authentication instant once
  at the start of the flow, carries it as `AuthenticationProperties.IssuedUtc` through the new required
  `AuthenticatePasswordGrantCommand.AuthenticationProperties` parameter, and reuses the same timestamp for the issued
  token's `CreatedWhen`, so the ROPC ticket's `auth_time` and the token's `iat` are coherent and the leaf handler no
  longer materializes an empty `AuthenticationProperties`.
  With pairwise subject identifiers ([ADR-0093](docs/decisions/0093-pairwise-subject-identifiers.md)),
  the `sub` in an `id_token_hint` is a per-sector pseudonym produced by a one-way projection the OP cannot reverse. The
  end-session handler previously consumed that pseudonym as if it were the stable principal, so for a pairwise client it
  silently matched no grants during subject-keyed revocation and double-projected the back-channel `logout_token` `sub`
  (which no relying party could then match). `DefaultResolveEndSessionSubjectHandler` now reads only `sid` and
  `client_id` from the hint and recovers the stable subject **forward** from the persisted session grant (falling back
  to the OP login cookie), never reading the hint's `sub` — so no reverse map is introduced (a reversible
  pseudonym→principal mapping was rejected as a correlation liability), mirroring the one-way-hashed grant-key model
  where a presented value is matched forward, never reversed. See
  [ADR-0096](docs/decisions/0096-end-session-subject-from-server-state.md).
- **Client-aborted requests at the OpenID endpoint pipeline are no longer rendered as `server_error` or audited.**
  When a client disconnects mid-request (for example a browser/SPA cancelling an in-flight discovery fetch), the
  resulting `OperationCanceledException` whose token is `HttpContext.RequestAborted` is a normal disconnect, not a
  protocol failure. `OpenIdExceptionEndpointFilter` now rethrows request-abort cancellations so ASP.NET Core abandons
  the request, instead of invoking the OpenID exception handler to write an error response to a dead connection and
  record an `openid.error` audit row (whose own write would then fail against the already-cancelled token).
- **The UserInfo endpoint now authenticates the presented self-issued bearer access token in-environment, instead of a
  host-configured default authentication scheme that never existed.** `DefaultAuthenticateSubjectHandler` resolved the
  subject via `HttpContext.AuthenticateAsync()` (the host's default scheme), but per-tenant signing keys and issuer are
  only known after the request environment resolves the tenant, so no pre-routing scheme can validate the token — the
  reference host registered none, and every valid access token presented to `/userinfo` was rejected `401
invalid_token`. The handler now validates a presented `Authorization: Bearer` token through the same in-environment
  self-issued `AuthenticateAccessTokenCommand` path the management API uses ([ADR-0044](docs/decisions/0044-management-api-authentication-and-globaladmin-bootstrap.md)),
  requiring the OpenID identity audience (`urn:ncode:openid`); requests that instead carry an interactive session (for
  example device verification) have no bearer header and still fall back to the session cookie. Invalid/expired tokens
  keep their `401` semantics.
- **A stale or invalid end-user session cookie now self-heals at the authorization endpoint instead of dead-ending with
  `access_denied`.** When `AuthenticateAsync` returned a _failure_ (a present-but-invalid session cookie — for example
  undecryptable after a data-protection key change, expired, or tampered), the authorize flow surfaced
  `access_denied` ("Failed to authenticate the end-user") back to the client, so the user could never reach the login
  form. An invalid cookie is now treated as "no session": the authorize flow prompts for interactive login (or returns
  `login_required` under `prompt=none`), and a successful login re-issues a fresh cookie. Bearer-token subject
  authentication (userinfo/token) keeps its `401` semantics and is unchanged.
  response executor serialized every ticket parameter directly, so an internal, `$`-prefixed parameter
  (`$created_when`) surfaced in the redirect callback query string / form-post body sent back to the client. The
  executor now honors the same `ShouldSerialize(…, SerializationFormat.OpenId)` contract the JSON serializer uses — the
  identical mechanism that already marks `$created_when`/`$authorization_source_type` as JSON-only — so internal
  parameters are omitted from every on-the-wire authorization response (query, fragment, and form-post).
- **Management console client mirror referenced a nonexistent `displayName`.** The frontend `ClientResource` type (and
  the client list UI) read a `displayName` field that the server's `ClientResource` never emits — a client has no
  display name; it is identified by its server-generated id. The TypeScript mirror was corrected to the real shape
  (`{ clientId, tenantId, concurrencyToken, isDisabled }`) and the console now lists clients by id.
- **Multi-audience access tokens are no longer wrongly rejected.** JWT claim validation for a multi-valued claim (the
  `aud` audience validator) required _every_ token value to appear in the expected set, so a token minted with more than
  one audience (for example `["urn:ncode:management", "urn:ncode:openid"]`) failed validation at any resource that
  expected only one of them. Validation now follows RFC 7519 §4.1.3 audience semantics: the claim is valid when it
  contains **at least one** of the expected values, and extra audiences are permitted. This was the blocker that kept a
  signed-in Management console from calling the management API (it was rejected as `invalid_token`). Single-valued
  issuer validation is unchanged.
- **Access-token validation failures at protected endpoints are now observable.** The self-issued access-token
  authenticator silently swallowed both validation exceptions and invalid results, so a rejected token surfaced only as
  an opaque `401`. It now emits a debug log (with the triggering exception) explaining why the token was rejected
  (issuer, audience, lifetime, or signature), while still returning `invalid_token`.
- **Interactive authorization-code exchange no longer drops the authenticated subject.** The authorization-code grant
  was persisted as the authorization request alone, but the token endpoint resumes it as an authorization _grant_
  (request **plus** authenticated subject); the shape mismatch deserialized the request to `null` (a server error on
  exchange) and discarded the subject. The grant is now persisted in the shape the token endpoint consumes.
- **Repeated interactive logins no longer fail antiforgery validation.** The host ordered `UseAntiforgery` before
  `UseAuthentication`/`UseAuthorization`. Because the interactive surface registers a single cookie scheme — which
  ASP.NET Core promotes to the default authenticate scheme — the login page authenticates the user once a session
  cookie exists and mints a claims-bound antiforgery token, but the middleware validated the POST before authentication
  ran, i.e. against an anonymous user. Every login after the first failed with _"The provided antiforgery token was
  meant for a different claims-based user than the current user"_. The middleware is now ordered after authentication;
  an integration test (`InteractiveLoginAntiforgeryTests`) locks the sequence in.
- **Management console sign-in no longer dead-ends on the OAuth callback under React StrictMode.** `AuthGate` completed
  the authorization-code callback in an effect guarded by a cancellation flag, but StrictMode double-invokes effects in
  development: the first run consumed the single-use code and one-shot PKCE transaction, so the second run found no
  transaction and showed _"No in-flight authorization transaction was found."_ Completion is now guarded by a run-once
  ref so it executes exactly once.
- **Interactive login bundle is now produced by the full-stack dev launch.** The `apps/auth` login surface is served by
  the host from a prebuilt `dist`, which nothing in the F5 path built, so `/account/login` returned a 503 _"interactive
  login bundle is not available"_. The dev launch now build-watches every frontend app (see the full-stack dev launch
  entry above), so the login bundle is produced automatically.

### Changed

- **Comprehensive XML-documentation quality pass across the published packages.** The `///` doc comments that reach
  consumers through IntelliSense and the generated OpenAPI descriptions were swept for correctness and consistency:
  property summaries follow the Microsoft convention (an `init`-only property opens with "Gets", not "Gets or sets");
  mediator-command types carry substantive, purpose-first summaries with complete `<param>` documentation; ADR numbers
  and other internal citations were moved out of `///` comments into `//` code comments; and a batch of grammar/typo
  defects was corrected (including a copy-paste error where the access-token payload-claims handler's summary described
  an ID token). No API or behavior change.

- **Discovery metadata is now secure-by-default, operator-overridable — completes issue [#128](https://github.com/NCodeGroup/Identity/issues/128).** The OpenID Provider
  no longer advertises deprecated/insecure flows out of the box
  ([ADR-0083](docs/decisions/0083-secure-by-default-discovery-metadata.md)). `response_types_supported` defaults to
  `["code"]` (dropping the implicit/hybrid combinations the formatter expands), `grant_types_supported` drops
  `implicit`, `code_challenge_methods_supported` defaults to `["S256"]` only, and `subject_types_supported` now defaults
  to `["public"]` so the required OpenID Connect Discovery field is present. The cross-trust-boundary token-signing
  fields (`id_token`/`userinfo`/`access_token`) advertise **asymmetric** algorithms only — never symmetric `HS256`
  (`request_object` and `token_endpoint_auth` signing keep the full set for `client_secret_jwt`). Because discovery and
  request-time enforcement share one settings source, the implicit/hybrid authorization code paths are **fail-closed**
  by default; a deployment re-enables any of these — and simultaneously re-advertises them — by widening the setting in
  configuration (host `OpenId:Server:Settings` overrides are applied last and overwrite the baseline). The host
  `appsettings.json` no longer over-advertises `implicit`. Greenfield, pre-release — no compatibility shims
  ([ADR-0025](docs/decisions/0025-pre-release-posture-and-auth0-parity-plus.md)).

- **Developer-key registration no longer owns the Data Protection key-ring location (breaking,
  `NCode.Identity.Server`).** `AddDeveloperKeys(DeveloperKeyMode)` drops its `IHostEnvironment` parameter and
  `AddDeveloperSigningKey()` drops its `DirectoryInfo keyRingDirectory` parameter — the library now only seeds the
  signing key, and the **host** owns where Data Protection keys are persisted (the host persists them in its dev
  database, [ADR-0073](docs/decisions/0073-postgres-dev-database-and-aspire-migration-worker.md)). The `App_Data/dp-keys`
  file key ring and the `Microsoft.EntityFrameworkCore.SqlServer`/`.Sqlite` host provider references are removed.

- **Opt-in framework capabilities now register through compile-time marker builders
  ([ADR-0069](docs/decisions/0069-registration-library-compile-time-marker-nesting.md)).** The registration library's
  `IServiceBuilder<TMarker>` becomes the one way bolt-on capabilities attach, so a capability is **un-addable** to a
  host that lacks its prerequisite library (a compile error, not a runtime surprise). `AddInteractiveAuthentication(...)`
  now extends `IServiceBuilder<OpenIdAuthenticationLibrary>`, and the local-account capabilities extend their OpenId
  library builder and take the store as a **required parameter**
  (`AddLocalAccountAuthentication(IServiceBuilder<LocalAccountLibrary>)` /
  `AddLocalAccountInteractiveAuthentication(IServiceBuilder<LocalAccountLibrary>)` on
  `IServiceBuilder<OpenIdAuthenticationLibrary>`; `AddLocalAccountManagement(IServiceBuilder<LocalAccountLibrary>)` on
  `IServiceBuilder<OpenIdManagementLibrary>`), each returning its receiver builder for chaining, instead of a naked
  `IServiceCollection`. The `OpenIdManagementLibrary` marker moves from `NCode.Identity.OpenId.Management`
  into `NCode.Identity.OpenId.Management.Abstractions` so a capability can name it without an implementation reference. Hosts
  now chain these off the builders the composition root exposes — for example
  `AddIdentityServer().OpenIdAuthenticationLibraryBuilder.AddLocalAccountAuthentication(localAccountStore)` —
  rather than a flat `serviceCollection.AddX()` list. The persistence framework gains a `StoreManagerLibrary` marker
  (`NCode.Persistence.Abstractions`): `AddEntityFrameworkCorePersistence<TDbContext>()` mints it, and the local-account
  store slices now receive `IServiceBuilder<StoreManagerLibrary>` (not a naked collection) and return
  `IServiceBuilder<LocalAccountLibrary>`. Chain roots and generic utility helpers intentionally stay bare-collection
  extensions.
- **Foundational JOSE/JWT libraries now gate at compile time, completing the marker-nesting model
  ([ADR-0069](docs/decisions/0069-registration-library-compile-time-marker-nesting.md)).** `AddJoseLibrary()` now extends
  `IServiceBuilder<SecretsLibrary>` and `AddJsonWebTokensLibrary()` extends `IServiceBuilder<JoseLibrary>` (each
  returns its own library builder) instead of taking a naked `IServiceCollection` and asserting the prerequisite at
  runtime via `VerifyIsRegistered<…>` — so these libraries, like the rest of the chain, are compile-time un-addable
  without their prerequisite. A new **`NCode.Identity.JsonWebTokens.Abstractions`** package now holds the
  `JsonWebTokensLibrary` marker, and the `OpenIdCoreLibrary` marker moves from `NCode.Identity.OpenId.Core` into
  `NCode.Identity.OpenId.Abstractions`, so every library marker lives in its abstractions package. Hosts chain
  `AddSecretsLibrary().AddJoseLibrary().AddJsonWebTokensLibrary()` (the composition root already does this). The
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core` registration class was renamed `CoreRegistration` →
  `DefaultRegistration` for naming consistency. Cross-library registration helpers invoked from multiple library
  builders (`AddMessageFactory<TMessage>`, `AddEndpointProvider<T>`, `AddEndpointGroup<T>`) retain the non-generic
  `IServiceBuilder` receiver by design. The development-only `AddDeveloperKeys`, `AddEphemeralDeveloperKeys`,
  `AddDeveloperSigningKey`, and `AddDeveloperInteractiveSeed` helpers now extend `IServiceBuilder<IdentityServer>`
  (chained off `AddIdentityServer()`) instead of a naked `IServiceCollection`, because they replace the server's tenant
  factory and hook its command handlers and are meaningless without the composed server; `AddDeveloperCors` (a
  standalone CORS policy) stays a bare-collection extension.
- **Every registration prerequisite is compile-time via the receiver plus required `IServiceBuilder<…>` parameters;
  `VerifyIsRegistered` and the `DisplayName`/`ConfigureMethod` marker members were removed
  ([ADR-0069](docs/decisions/0069-registration-library-compile-time-marker-nesting.md),
  [ADR-0060](docs/decisions/0060-local-account-one-store-seam-and-dependency-inverted-authentication.md),
  [ADR-0061](docs/decisions/0061-local-account-management-is-an-opt-in-capability.md)).** A registration with one prerequisite
  receives `IServiceBuilder<TMarker>`; one with a **second** prerequisite takes it as a **required `IServiceBuilder<…>`
  parameter** — the builder that prerequisite's own registration returned — so the method is uncallable without it (a
  missing prerequisite is a compile error), and the receiver builder is returned so the chain continues on the primary
  library. A new `LocalAccountLibrary` marker (`NCode.Identity.OpenId.Accounts.Abstractions`) is minted and returned by
  every local-account store registration (`AddLocalAccountEntityFramework()`,
  `AddAspNetIdentityLocalAccountStore<TUser>()`, or a custom store), and `AddLocalAccountAuthentication()` /
  `AddLocalAccountInteractiveAuthentication()` (on `IServiceBuilder<OpenIdAuthenticationLibrary>`) and
  `AddLocalAccountManagement()` (on `IServiceBuilder<OpenIdManagementLibrary>`) take `IServiceBuilder<LocalAccountLibrary>`
  as a required parameter — a local-account capability cannot even be written without a store. Likewise
  `AddOpenIdAuthenticationLibrary()` (on `IServiceBuilder<IdentityLibrary>`) takes `IServiceBuilder<JoseLibrary>`. (A
  composite `IServiceBuilder<T1, T2>` receiver + `.And()` combinator was prototyped and dropped: a required parameter
  proves the same thing with less machinery and a cleaner call site, and a marker _value_ can't serve as the proof
  because an empty struct is forgeable.) The `NCode.Registration.ServiceCollectionExtensions.VerifyIsRegistered<TMarker>()`
  method is **removed** (the `IsRegistered<TMarker>()` query remains), and markers are now **empty `readonly struct`s**:
  the `DisplayName` and `ConfigureMethod` members and the `IMarker<TMarker>` base were removed, leaving
  `IRegistrationMarker<TMarker>` whose type identity is the whole signal.
- **The event bus is now a marker-bearing library
  ([ADR-0069](docs/decisions/0069-registration-library-compile-time-marker-nesting.md)).** `AddEventServices()` mints a new
  `EventsLibrary` marker (`NCode.Identity.Events.Abstractions`) and returns `IServiceBuilder<EventsLibrary>`; its opt-ins
  `AddEventHandler<TEvent, THandler>()` and `AddBackgroundEventDelivery()` (moved to a new `EventLibraryRegistration`
  class, since one static class may hold only one `extension` receiver shape) now receive that builder, so an event
  opt-in is compile-time un-addable without the event services.
- **The interactive login/consent backend moved out of the composition root into its own leaf package with a pluggable
  verifier ([ADR-0070](docs/decisions/0070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md)).** The
  cookie scheme, `/account/*` endpoints, antiforgery wiring, and static-file server move from
  `NCode.Identity.Server/InteractiveAuthentication/` into **`NCode.Identity.OpenId.Authentication.Interactive`**
  (+ **`.Abstractions`**), so `NCode.Identity.Server` only references the capability like any other. The surface is now
  backend-agnostic: it depends on an `IInteractiveCredentialVerifier` seam (in the `.Abstractions` package), whose
  local-account implementation lives in `NCode.Identity.OpenId.Accounts.Authentication` and is registered by
  `AddLocalAccountInteractiveAuthentication()`. The old `NCode.Identity.Server.InteractiveAuthentication.*` public
  surface is removed and reintroduced under `NCode.Identity.OpenId.Authentication.Interactive.*`. Resolves issue [#142](https://github.com/NCodeGroup/Identity/issues/142).

- **`IPersistedGrantService.AddAsync` now returns the server-generated grant id.** Its return type changes from
  `ValueTask` to `ValueTask<string>`, handing back the opaque public `GrantId` of the persisted grant so callers (such
  as the new initial-access-token management endpoint) can reference it for later revocation. Existing callers that
  ignore the result are unaffected. See [ADR-0063](docs/decisions/0063-dynamic-client-registration.md).

- **Local-account management is now an opt-in capability that plugs into the Management slice, not part of it.**
  Mirroring the authentication inversion, the local-account management endpoints, their validator
  (`ILocalAccountValidator` / `DefaultLocalAccountValidator`), the wire contracts, and the `LocalAccountChangedAuditEvent`
  move into two new account-owned packages — **`NCode.Identity.OpenId.Accounts.Management`** (`AddLocalAccountManagement()`)
  and **`NCode.Identity.OpenId.Accounts.Management.Abstractions`** — so `NCode.Identity.OpenId.Management` no longer
  references `NCode.Identity.OpenId.Accounts.Abstractions`. The shared endpoint base the capability reuses is promoted
  into `NCode.Identity.OpenId.Management.Abstractions`: a public `ManagementApiEndpointHandler` (the generic
  authorization / `GET` / collection helpers; the implementation keeps an internal `BaseApiEndpointHandler` deriving from
  it for its secret/settings helpers), plus the now-public `DefaultResourceValidator`, `Operations`, `TenantScopeResource`,
  and a new `ManagementEndpointGroups.Tenant` group-name constant a capability uses to attach a keyed `IEndpointProvider`
  to the tenant group. The management audit recorder drops its bespoke `RecordLocalAccountChangedAsync` for a generic
  `RecordResourceChangeAsync(HttpContext, ResourceChangeAuditEvent, ct)` seam, and `ManagementResourceTypes.LocalAccount`
  moves to `AccountManagementResourceTypes.LocalAccount` in the new package. The management wire contracts move from
  `NCode.Identity.OpenId.Management.Contracts.LocalAccounts` to `NCode.Identity.OpenId.Accounts.Management.Contracts`
  (JSON shape unchanged). The `LocalAccountFeature` marker and the per-request `501 Not Implemented` feature-detection
  are **removed**: a capability is enabled by registering its package, so the library no longer checks whether the
  library itself is wired up. See
  [ADR-0061](docs/decisions/0061-local-account-management-is-an-opt-in-capability.md).

- **Local accounts have one pluggable seam, and the password grant is dependency-inverted out of the authentication
  package.** `ILocalAccountSource` and `ILocalAccountProvisioner` are removed: `ILocalAccountStore` is the single,
  backing-store-agnostic seam, with credential handling expressed as first-class operations
  (`AddAsync(account, password?, ct)`, `SetPasswordAsync`, `VerifyCredentialAsync`) instead of a raw `PasswordHash` on
  `PersistedLocalAccount` (which is removed; `SecurityStamp` is store-managed). The runtime `LocalAccount` payload and
  the `LocalAccount*Request` DTOs are removed. The resource-owner password grant moves out of
  `NCode.Identity.OpenId.Authentication` into a new opt-in **`NCode.Identity.OpenId.Accounts.Authentication`** package
  (`AddLocalAccountAuthentication()`), so the authentication package no longer depends on the account package; without
  that capability the base package answers the grant with `unsupported_grant_type`. `CreatePasswordGrantSubjectCommand`
  moves to the new package, and the metadata projection claim types move from `AccountConstants` to
  `NCode.Identity.OpenId.Principals.SubjectMetadataClaimTypes`. Local-account management drops its owners sub-resource
  and the `local_account` resource-node type and authorizes against the tenant scope (`TenantScopeResource` is now a
  public authorization resource). The **ASP.NET Core Identity** adapter
  (`NCode.Identity.OpenId.Accounts.AspNetIdentity`) now implements `ILocalAccountStore` over `UserManager<TUser>`
  (`AddAspNetIdentityLocalAccountStore<TUser>()`), and the Entity Framework `EntityStoreManager` gained a
  DbContext-less store-factory fallback so a non-EF store resolves without referencing `OpenIdDbContext`.
  `IFederatedPrincipalStore` gains `UpdateAsync` and `RemoveAsync`; `IFederatedIdentityStore` gains `RemoveAsync`. See
  [ADR-0060](docs/decisions/0060-local-account-one-store-seam-and-dependency-inverted-authentication.md).

- **`NCode.Identity.Abstractions` is now framework-free.** Its ASP.NET Core HTTP surface
  (`Models/UriDescriptor`, `Exceptions/HttpResultException`, `Results/*`) moved to the new
  `NCode.Identity.AspNetCore.Abstractions` package and its Settings subsystem moved to
  `NCode.Identity.Settings.Abstractions`, so the core drops the `Microsoft.AspNetCore.App` framework reference and is
  referenceable by any lower-tier package (including the JOSE/Secrets crypto branch). The JOSE
  `JsonElementExtensions.TryGetPropertyValue<T>` helper folded into `NCode.Json.JsonElements`, leaving a single
  universal `JsonElement` helper home, and detached JSON parsing standardizes on `JsonElement.Parse(string)`.
  `NCode.Identity.OpenId.Abstractions` now references `NCode.Registration.AspNetCore` directly (it exposes
  `ReadOnlyEndpointDisposition` on its public surface) rather than inheriting it transitively from the core. Namespaces
  are unchanged. See [ADR-0056](docs/decisions/0056-abstractions-split-into-framework-free-tiered-packages.md).

- **Subject metadata (`ProfileMetadata` / `SystemMetadata`) is now a principal-level concept, resolved fresh at
  issuance.** The two bags moved off `LocalAccount` (and `PersistedLocalAccount` / `LocalAccountCreationRequest`) onto
  `FederatedPrincipal` / `PersistedFederatedPrincipal`, and are resolved at token/UserInfo issuance through a new
  pluggable `IPrincipalMetadataProvider` (default over `IFederatedPrincipalStore`) keyed by the resolved `PrincipalId`.
  They are therefore projected identically for every grant flow (ROPC, interactive, refresh) and every connection kind
  (local **or** external IdP), where before only ROPC-authenticated local accounts received them. The provider reads
  fresh per issuance (cluster-consistent); there is no built-in cache — host-side caching is a replaceable-provider
  concern that must be distributed. The ROPC subject builder no longer stamps metadata onto the subject. See
  [ADR-0055](docs/decisions/0055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) (amends
  [ADR-0054](docs/decisions/0054-local-account-metadata-projection.md)).

- Entity Framework persistence was split into a reusable **framework** package
  (`NCode.Identity.OpenId.Persistence.EntityFramework`) plus domain **slice** packages that plug into the shared,
  now-`DbSet`-less `OpenIdDbContext` via `IOpenIdModelContributor`. The core OpenID schema moved to
  **`NCode.Identity.OpenId.Persistence.EntityFramework.Core`** and the local-account schema moved from
  `NCode.Identity.OpenId.Accounts.EntityFramework` to **`NCode.Identity.OpenId.Persistence.EntityFramework.Accounts`**;
  each slice's root namespace matches its package id (`…EntityFramework.Core.*` / `…EntityFramework.Accounts.*`). The
  framework's store base (`BaseStore`, `BaseStoreWithResourceId`), the `AddStore` registration helper, and the
  `UseIdGenerator` attribute are now public so a slice reuses them instead of re-implementing the boilerplate. Hosts
  register the framework plus each slice (`AddEntityFrameworkCorePersistence<TDbContext>()`), and a slice may expose
  typed `DbSet` accessors for its own entities as extension members. See
  [ADR-0052](docs/decisions/0052-entity-framework-persistence-is-a-framework-plus-slices.md).
- All persisted OpenID data is now **tenant-scoped except the server root**. `FederatedPrincipal`/`FederatedIdentity`
  (previously global) and the new `LocalAccount` are tenant-owned: their persisted DTOs gained a required `TenantId`,
  and their server-generated ids and natural keys are now unique **per tenant** rather than globally — the same human
  authenticating in two tenants is two principals. The tenant-scope global query filter is navigation-free: the
  `ISupportTenantEntity` contract now carries a denormalized `NormalizedTenantId` discriminator (its former `TenantId`
  foreign-key member was removed), so the filter is a single indexed-column comparison and the framework stays
  decoupled from the concrete tenant entity. See
  [ADR-0035](docs/decisions/0035-federated-principals-and-identity-resolution.md) (amended) and
  [ADR-0018](docs/decisions/0018-tenant-scoped-data-access-at-the-persistence-layer.md).

- The `NCode.Identity.OpenId.Host` host adopted the first-party .NET 10 OpenAPI stack: `Swashbuckle.AspNetCore`
  was replaced by `Microsoft.AspNetCore.OpenApi` (`AddOpenApi()`/`MapOpenApi()`) for document generation and
  `Scalar.AspNetCore` (`MapScalarApiReference()`) for the UI. `Microsoft.OpenApi` was aligned to the 2.x line that the
  net10 OpenAPI stack targets, and the two OpenID endpoint operation builders (`DefaultAuthorizationEndpointHandler`,
  `DefaultTokenEndpointProvider`) now use the 2.x `OpenApiMediaType` model.
- The `NCode.Identity.OpenId.Host` host migrated to the modern ASP.NET Core minimal-hosting model: the
  `Startup` class and the `Host.CreateDefaultBuilder`/`UseStartup` bootstrap were replaced by a single top-level
  `Program.cs` using `WebApplication.CreateBuilder(...)`. The integration tests are unaffected — they still boot
  through `WebApplicationFactory<HostApiMarker>`.
- The **bootstrap administrator** `ClientSecret` is now supplied **Base64Url-encoded** rather than as plaintext: the
  seed handler decodes it into a pinned, zeroed buffer (threaded as `ReadOnlyMemory<byte>`) so the plaintext is not left
  on the GC heap, and the OpenAPI example and Scalar prefill that surface the presentable secret are gated to the
  development environment. See [ADR-0044](docs/decisions/0044-management-api-authentication-and-globaladmin-bootstrap.md).
- The development-only signing-key opt-ins moved out of the `NCode.Identity.OpenId.Host` host and into the
  `NCode.Identity.Server` composition root, so any host can consume them rather than each app re-implementing them
  (the host is just the EXE host that wires them in). `AddEphemeralDeveloperKeys()`,
  `AddDeveloperSigningKey(DirectoryInfo)`, and the `EphemeralOpenIdTenantFactory` /
  `DeveloperSigningKeyOpenIdTenantFactory` types now live under the `NCode.Identity.Server` namespace. See
  [ADR-0002](docs/decisions/0002-ephemeral-development-keys.md) and
  [ADR-0021](docs/decisions/0021-developer-signing-keys-seeded-through-persistence.md).
- The hierarchical endpoint-group mechanism (`IEndpointGroup`, `IEndpointGroupBuilder`, `IEndpointProvider`, the
  route-tree builder, and the endpoint/operation disposition types) moved out of `NCode.Identity.Abstractions` into a new
  cross-cutting package, **`NCode.Registration.AspNetCore`**, that layers on `NCode.Registration` and the ASP.NET Core
  shared framework. `IEndpointGroupBuilder` now **is** an `IServiceBuilder` (it inherits it) rather than exposing a bare
  `IServiceCollection`, so a group's `ConfigureServices` registers its endpoints, child groups, and the services those
  endpoints depend on — including `AddMessageFactory<T>()` — through one registration surface. The separate per-family
  `AddXxxEndpoint()` chain is gone: each endpoint family registers its own provider and services, and the group invokes
  each family once, so no endpoint is listed twice. Public types moved namespace
  (`NCode.Identity.Endpoints` → `NCode.Registration.AspNetCore`) and three symbols were renamed
  (`IIdentityEndpointRouteBuilder` → `IEndpointTreeRouteBuilder`, `MapIdentityEndpoints()` → `MapEndpointGroups()`, and
  the route-builder default implementation). See
  [ADR-0043](docs/decisions/0043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md).
- Endpoint route-group composition is now declarative, hierarchical, and unified across the OpenID protocol and
  management surfaces. A named `IEndpointGroup` **owns its contents**: it exposes a `Name` and `Prefix`, an optional
  `ConfigureRoutes` that attaches shared route-group filters when routes are mapped, and a
  `ConfigureServices(IEndpointGroupBuilder)` that registers at startup the endpoints (`AddEndpoint<T>()`) and child
  groups (`AddGroup<T>()`) it contains, plus the services those endpoints depend on (via the builder's
  `ServiceCollection`). Registering a single root group (`AddEndpointGroup<TRoot>()`) walks that declaration and
  registers the whole subtree; the route builder materializes
  each group with its prefix and filters, maps the group's endpoints, and recurses into its child groups. The
  management surface composes a hierarchy — `api` (`/api`) → `api/tenant` (`/tenants/{tenantId}`) → `api/tenant/client`
  (`/clients/{clientId}`) — so tenant-bound families (clients, grants, resource servers, client grants) nest under the
  tenant and map only their relative sub-routes, and the protocol surface is a single `openid` group. This replaces the
  imperative `IEndpointGroupProvider` tier, the per-family marker interfaces, and the separate keyed
  `AddEndpointProvider<T>(groupName)` / `AddOpenIdEndpointProvider<T>()` registration calls, which were removed. See
  [ADR-0042](docs/decisions/0042-management-api-route-driven-tenant-scope.md).
- The management `/api` surface derives its ambient tenant scope from the route's `{tenantId}` segment rather than the
  request's protocol-resolved tenant, so a central-admin caller can administer a tenant by id while the persistence-layer
  query filter confines the read fail-closed ([ADR-0018](docs/decisions/0018-tenant-scoped-data-access-at-the-persistence-layer.md));
  absent a route `{tenantId}` (control-plane routes) it falls back to the resolved tenant, a no-op for the unscoped
  server/tenant rows those routes act on. The tenant effective-settings endpoint is consequently addressed by id —
  `GET /api/tenants/{tenantId}/effective-settings` (was `GET /api/tenant/effective-settings`, the request's current
  tenant) — reconstructing the view from the server baseline merged with the addressed tenant's persisted overrides.
  Nesting the remaining tenant-bound families (clients, grants, resource servers) under `/api/tenants/{tenantId}/…`
  follows. See [ADR-0042](docs/decisions/0042-management-api-route-driven-tenant-scope.md).

### Added

- Authenticated **effective-settings** preview endpoints that return the resolved, merged settings view (server
  baseline + tenant/client overrides + descriptor defaults) as a flat `name → value` JSON object — using the same value
  representation as the discovery document but including settings that are not discoverable — for each settings-bearing
  entity: `GET /api/tenants/{tenantId}/effective-settings`, `GET /api/clients/{clientId}/
effective-settings`, and `GET /api/servers/{serverId}/effective-settings`. Each is gated by the same `Read`
  authorization as reading that entity's raw settings and carries no `ETag` (a merged view spans multiple persisted
  entities). This is the gated replacement for the removed anonymous discovery `showAll` override. New wire contracts
  `TenantEffectiveSettingsResource`, `ClientEffectiveSettingsResource`, and `ServerEffectiveSettingsResource`. See
  [ADR-0040](docs/decisions/0040-effective-settings-preview-endpoints.md) (the tenant endpoint is addressed by id per
  [ADR-0042](docs/decisions/0042-management-api-route-driven-tenant-scope.md)).
- End-to-end support for the OpenID Connect `claims` request parameter
  ([OIDC Core 5.5](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter)), now advertised as
  `claims_parameter_supported: true` in discovery. Requested `claims.id_token` claims are added on top of the
  scope-driven claims during ID-token issuance, and requested `claims.userinfo` claims are honored at the UserInfo
  endpoint: when an access token is issued for a request that carried `claims.userinfo`, the requested claims are
  persisted as a short-lived stateful grant (`OpenIdConstants.PersistedGrantTypes.UserInfoClaims`) keyed by the access
  token's `jti` and bounded by its lifetime, then resolved at UserInfo — the access token itself is never inflated.
  `value`/`values` are advisory and `essential` is best-effort; a host can impose stricter semantics with a
  higher-priority claims handler. See [ADR-0039](docs/decisions/0039-claims-request-parameter.md).
- Data-driven resource ownership: persisted `(principal, role, resource-node)` role assignments
  (`PersistedRoleAssignment` / `IRoleAssignmentStore`), an additive `OwnershipHandler` that grants a principal every
  operation except `Create` on a resource it owns (or an ancestor node), and creator-as-owner on client, resource
  server, and tenant creation. Each of those resources gains owner-management endpoints
  (`GET`/`POST`/`DELETE /{resource}/{id}/owners`) returning the rich `OwnerResource`, with a one-owner invariant that
  refuses to orphan a resource (`409`). The ownership service and resource-node abstraction (`IResourceOwnershipService`,
  `IResourceNode`, `ResourceNode`, `ResourceNodeTypes`) are public so applications can own and enforce their own
  resource types. See [ADR-0034](docs/decisions/0034-persisted-role-assignments-and-ownership.md).
- A single management resource server (`urn:ncode:management`) that gates the whole management API through granular
  per-leaf scopes composed as `{verb}:{family}` (for example `read:clients`, `update:tenant_settings`,
  `create:server_secrets`). Its control-plane scope families (`servers`, `server_settings`, `server_secrets`,
  `tenants`) are seeded only into the root (control-plane) tenant, while its tenant-plane families (a tenant's own
  settings and secrets, plus its clients, client secrets, resource servers, client grants, and grants) are seeded into
  every tenant. A `SystemResourceServerDescriptor` now declares a `Plane` (`SystemResourceServerPlane.Tenant` (default)
  or `Control`); the system-resource-server seeder is plane-aware and merges every provider that contributes to the same
  audience into one resource server per tenant, seeding `Control`-plane families only into the root tenant, whose
  identifier is `TenantResolutionOptions.RootTenantId` (default `"root"`). The static single-tenant strategy provisions
  the root tenant lazily alongside its workload tenant, so a single-tenant deployment gains its control plane with no
  extra configuration. See [ADR-0033](docs/decisions/0033-authorization-model-scopes-roles-and-ownership.md) and
  [ADR-0031](docs/decisions/0031-control-plane-management-resource-server-and-root-tenant-seeding.md).
- An `OpenID Connect` UserInfo endpoint (`GET`/`POST /oauth2/userinfo`,
  [OpenID Connect Core 5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo), auto-advertised as
  `userinfo_endpoint` in discovery). The endpoint is a thin shell over two mediator seams: a reusable
  `AuthenticateSubjectCommand` whose default handler authenticates the caller via the host's ASP.NET Core authentication
  scheme (the same mechanism the management API uses) and maps it to `SubjectAuthentication`; and a
  `GetUserInfoClaimsCommand` enricher pipeline whose default handler supplies only the required `sub` claim, with
  applications registering additional `ICommandHandler<GetUserInfoClaimsCommand>` handlers to contribute profile/email/
  other claims. The response is an extensible `[JsonExtensionData]` result (`UserInfoResult`). See
  [ADR-0030](docs/decisions/0030-userinfo-endpoint-and-subject-authentication-seam.md).
- An `OAuth 2.0` token introspection endpoint (`POST /oauth2/introspect`,
  [RFC 7662](https://datatracker.ietf.org/doc/html/rfc7662), auto-advertised as `introspection_endpoint` in discovery).
  An authenticated client may probe a token's active state: the default handler validates a JWT access token against the
  tenant signing keys, issuer, and lifetime, otherwise resolves a refresh-token grant, and reports `{"active": false}`
  for anything else. The response is a discovery-style, extensible result (`IntrospectionResult` with
  `[JsonExtensionData]` claims) that additional `ICommandHandler<IntrospectTokenCommand>` handlers may enrich. The
  endpoint is a thin `IOpenIdEndpointProvider` that authenticates the client and parses a typed
  `ITokenIntrospectionRequest`, then delegates to the `IntrospectTokenCommand` mediator handler. See
  [ADR-0029](docs/decisions/0029-token-introspection-endpoint.md).
- An `OAuth 2.0` token revocation endpoint (`POST /oauth2/revoke`,
  [RFC 7009](https://datatracker.ietf.org/doc/html/rfc7009), auto-advertised as `revocation_endpoint` in discovery). An
  authenticated client may revoke one of its own refresh tokens; an unknown or non-revocable token (including a stateless
  JWT access token) is a no-op, and the endpoint always responds `200 OK` so it never reveals token state. The endpoint
  is a thin `IOpenIdEndpointProvider` that authenticates the client and parses a typed `ITokenRevocationRequest`, then
  delegates to the `RevokeTokenCommand` mediator handler. See
  [ADR-0028](docs/decisions/0028-token-revocation-refresh-tokens-only.md).
- Shared tenant-resolution abstraction (`ITenantResolver` / `ITenantStrategy` / `TenantResolutionOptions` in
  `NCode.Identity.OpenId.Abstractions`, default implementation in `NCode.Identity.OpenId.Core`) so the OpenID runtime
  and the management API select the ambient tenant from a single source of truth. See
  [ADR-0018](docs/decisions/0018-tenant-scoped-data-access-at-the-persistence-layer.md).
- Tenant-scoped data access for the management API: an `IAmbientTenantAccessor`
  (`NCode.Identity.OpenId.Persistence.Abstractions`) plus an `OpenIdDbContext` global query filter on tenant-child
  entities so that, when tenancy is request-derived (dynamic-by-host / dynamic-by-path), a resource outside the
  request's tenant is never materialized (a cross-tenant read returns `404`, closing the fetch-then-reject leak
  window). A reusable `TenantScopeEndpointFilter` establishes the scope; tenant and server administration remain
  central-admin (unscoped). See
  [ADR-0018](docs/decisions/0018-tenant-scoped-data-access-at-the-persistence-layer.md).
- Collection `GET` endpoints for the management API — `GET api/clients`, `GET api/servers`, `GET api/tenants` — that
  return a page of resources (`CollectionResource<T>` = `{ items, continuationToken }`) using keyset (cursor)
  pagination (`?limit=` clamped to 100, `?cursor=` opaque continuation token). The client list is scoped to the
  request's tenant; the server and tenant lists are central-admin. There is deliberately no query protocol (no OData);
  see [ADR-0019](docs/decisions/0019-collection-endpoints-keyset-pagination.md).
- Grant administration for the management API — a tenant-scoped, read-only surface: `GET api/grants` (paged, with
  optional `?subjectId=` / `?clientId=` filters), `GET api/grants/{grantId}`, `DELETE api/grants/{grantId}` (single
  soft-revoke), and `DELETE api/grants?subjectId=&clientId=` (bulk soft-revoke of every active match, requires at least
  one filter, returns `GrantRevocationResult { revoked }`). Revocation sets `RevokedWhen` and keeps the row for audit;
  it is idempotent. Grants are addressed by a new opaque, server-generated `GrantId` (`PersistedGrant.GrantId` /
  `GrantEntity.GrantId`), and `GrantResource` projects metadata only — the internal `HashedKey` and the grant
  `PayloadJson` are never exposed. The shared keyset-pagination mechanics move from `BaseStoreWithResourceId` to
  `BaseStore` so a grant store not keyed by a resource id can paginate on its chronological surrogate id. See
  [ADR-0020](docs/decisions/0020-grant-management-read-surface.md) and
  [ADR-0022](docs/decisions/0022-grant-filters-and-bulk-revocation.md).
- Scope and resource management (Auth0-aligned): a tenant-owned **resource server** (an API/audience) owns its
  **scopes** (permissions), and a **client grant** authorizes a client to a resource server with a subset of its
  scopes. Adds the persistence model (`ResourceServerEntity` / `ScopeEntity` / `ClientGrantEntity`, DTOs,
  `IResourceServerStore` / `IClientGrantStore`), seeds a reserved OpenID Connect system resource server
  (`urn:ncode:openid`) with the standard scopes into each tenant via `ISystemResourceServerProvider`, and the
  management surface `GET/POST/GET/PATCH/DELETE api/resource-servers` plus the `/{id}/scopes` sub-resource, and the
  nested `GET/POST/GET/PUT/DELETE api/clients/{clientId}/grants[/{resourceServerId}]` client-grant surface. Granted
  scopes are validated (write-time) against the target resource server's scopes. Core preconditions for both surfaces
  are asserted up-front by replaceable validators (`IResourceServerValidator` / `IClientGrantValidator`), consistent
  with the existing client/server/tenant validators. A reserved tenant-plane management resource server
  (`urn:ncode:management`) is also seeded into each tenant, whose `{verb}:{family}` scopes (for example `read:clients`,
  `create:resource_servers`, `delete:grants`) gate the per-tenant management families; control-plane families
  (servers, tenants) are intentionally excluded and belong to the root tenant's control plane. System
  resource servers and scopes cannot be deleted. See [ADR-0023](docs/decisions/0023-scopes-and-resources-management-model.md)
  and [ADR-0024](docs/decisions/0024-control-plane-and-per-tenant-planes.md).
- A persistent developer-keys opt-in for the host, `AddDeveloperSigningKey(...)`, that seeds an `RSA` signing key
  through the tenant secret store (so the runtime, the JWKS endpoint, and the management API share one source of truth)
  instead of overriding the read path. Paired with a local SQLite database and a persistent Data Protection key ring
  (DPAPI-protected on Windows), the key survives restarts and self-heals if the key ring is lost; the existing
  `AddEphemeralDeveloperKeys()` stays the hermetic path for tests. Selected via `DeveloperKeys:Mode=PersistentSigningKey`
  (set in `launchSettings.json`). See [ADR-0021](docs/decisions/0021-developer-signing-keys-seeded-through-persistence.md).

### Changed

- Setting merge is now a first-class policy expressed through a public `SettingMerge` vocabulary of lattice dual pairs
  (`Min`/`Max`, `And`/`Or`, `Intersect`/`Union`, `Keep`/`Replace`) in `NCode.Identity.Abstractions`, so host-authored
  descriptors reuse it. Each built-in setting is mapped to a **ceiling** (a parent caps the child), a **floor** (a
  parent sets an un-loosenable minimum), or an **override**, chosen by the setting's value polarity. A more-specific
  scope (tenant/client) can no longer loosen a broader scope's security-relevant setting: token/code lifetimes,
  `clock_skew`, and `subject_max_age` are ceilings a child may only shorten; `require_pkce`, the `*_encryption_required`
  flags, `claims_supported_is_strict`, `refresh_token_rotation_enabled`, and the `federated_identity_*` requirement
  flags are floors; `allow_*` and `request(_uri)_parameter_supported` are ceilings; `allowed_identity_providers` and
  `subject_types_supported` merge as intersect ceilings; and `tenant_issuer` is parent-owned (`Keep`).
  `allowed_identity_providers` is now fail-closed — unset means unrestricted, but an explicit (or narrowed-to-empty)
  allowlist denies every IdP. See [ADR-0038](docs/decisions/0038-setting-merge-lattice-duals.md).
- Relocated the default `claims_supported` list from the descriptor literal into the replaceable
  `IDefaultSettingsProvider` baseline, so a host that enables `claims_supported_is_strict` can extend the allow-list
  instead of being frozen by the library's defaults; the advertised default is unchanged
  ([ADR-0010](docs/decisions/0010-supported-settings-unset-means-unrestricted.md)).
- Probe the tenant cache before reading the tenant store on the resolution hot path. `ITenantResolver` and
  `ITenantStrategy` gain `TryGetTenantId(httpContext, out tenantId)`, a store-free identity probe satisfied by the
  static-single (configured id) and dynamic-by-path (route value) strategies; `DefaultOpenIdTenantFactory` uses it to
  serve a warm tenant with no tenant-store round-trip. Dynamic-by-host still performs a full resolve because mapping a
  domain to a tenant requires the store. A cache hit no longer re-reads the store, so a tenant disabled after it was
  cached serves until its cache entry expires. See
  [ADR-0037](docs/decisions/0037-tenant-cache-probe-before-store-load.md).
- Renamed the authorization endpoint's subject pipeline to drop the redundant `Subject` noun (the namespace already
  supplies the context) now that a general subject-authentication seam exists: `AuthenticateSubjectCommand` →
  `AuthenticateCommand`, `AuthorizeSubjectCommand`/`AuthorizeSubjectDisposition` → `AuthorizeCommand`/
  `AuthorizeDisposition`, `ChallengeSubjectCommand` → `ChallengeCommand` (and the matching `Default*` handlers). The
  cross-slice `ValidateSubjectCommand`/`DefaultValidateSubjectHandler` were renamed to
  `ValidateSubjectAuthenticationCommand`/`DefaultValidateSubjectAuthenticationHandler` to reflect that they validate an
  existing login-session `SubjectAuthentication` (tenant/`max_age`/`acr`), not the generic authenticate pipeline. The
  root `Subject` capability keeps the `Subject` noun. See
  [ADR-0030](docs/decisions/0030-userinfo-endpoint-and-subject-authentication-seam.md).
- Collapsed the per-strategy OpenID tenant providers, their selector, and the thin tenant factory into a single
  `DefaultOpenIdTenantFactory` that composes `ITenantResolver` for selection and performs only tenant materialization.
  Tenant-selection option types moved from `NCode.Identity.OpenId.Authentication` to
  `NCode.Identity.OpenId.Abstractions` (`NCode.Identity.OpenId.Tenants` namespace); `OpenIdTenantOptions` now carries
  only materialization settings.
- A managed client is always owned by the request's tenant, so `POST api/clients` no longer accepts a tenant
  identifier: `CreateClientRequest.TenantId` is removed and the server derives the owning tenant from the request
  (resolved for every tenant strategy, static-single included). This removes the redundant, error-prone echo of a
  tenant the server already resolved.
- The management API endpoints are now served under a shared top-level `/api` route group (`/api/clients`,
  `/api/servers`, `/api/tenants`, …) via a `ManagementEndpointGroupProvider` ([ADR-0009](docs/decisions/0009-endpoint-families-own-their-route-group.md)),
  keeping them distinct from the OpenID protocol endpoints at the root.
- Grants are now strictly tenant-scoped: the previously nullable `TenantId` on the grant vertical slice
  (`PersistedGrantId`, `PersistedGrant`, `PersistedGrant<TPayload>`, `GrantEntity`, `GrantResource`) and the
  `IPersistedGrantService.CreateGrantId` parameter are now required (non-nullable). Every grant is created against the
  resolved request tenant, so the unused non-tenant (global) key space is removed; a future server-level grant type, if
  needed, would be modeled explicitly rather than by an absent tenant.
- Runtime scope enforcement is now driven by the resource-server / client-grant model instead of a `scopes_supported`
  setting, which is retired (its `SettingKey`, descriptor, and default are removed). A replaceable `IClientScopeService`
  resolves a client's allowed scopes — every scope of a system resource server (`IsSystem`) is implicitly available to
  all clients, while a non-system API is available only through a client grant intersected with the API's current
  scopes (read-time intersection) — and the authorization/token validators reject out-of-scope requests. Discovery's
  `scopes_supported` is derived from the tenant's resource-server catalog. `offline_access` availability follows from the
  system identity resource server; refresh-token issuance remains governed by the request and the `refresh_token` grant
  type. See [ADR-0026](docs/decisions/0026-scope-enforcement-via-resource-servers-and-client-grants.md).
- An access token's `aud` is now bound to the resource servers that own its effective scopes (resolved via
  `IClientScopeService.ResolveAudiencesAsync`) instead of the requesting client, so a resource server can validate that a
  token was minted for it; a token spanning several resource servers carries a multi-valued `aud`, and a scopeless token
  falls back to the client id. The ID token's `aud` remains the client. See
  [ADR-0027](docs/decisions/0027-access-token-audience-from-resource-servers.md).

### Security

- **Dependencies move to the .NET 10.0.13 servicing band, and `MessagePack` is pinned to 2.5.307, clearing newly
  published advisories.** Every `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*`, `Microsoft.Extensions.*`,
  `System.Text.Json`, and `System.Security.Cryptography.Xml` pin (and the `dotnet-ef` tool) moves from 10.0.12 to
  10.0.13, which pulls `System.Security.Cryptography.Pkcs` 10.0.13 transitively, clearing GHSA-2fmv-rv2c-59x7 and
  GHSA-846f-f764-76mx (`Pkcs`) and GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj, and GHSA-r955-8q5j-3h83 (`Xml`). A new
  transitive pin moves `MessagePack` (pulled into the Aspire-only `LocalDev` by `Aspire.Hosting` /
  `StreamJsonRpc`) from 2.5.302 to 2.5.307, clearing GHSA-qhrr-8q5h-9q3h.

- Removed the undocumented `showAll` query-string override from the discovery endpoint
  (`/.well-known/openid-configuration`). It was unauthenticated and, when set, bypassed both the per-setting
  `IsDiscoverable` filter and the per-endpoint discoverability filter, letting any anonymous caller enumerate every
  non-advertised setting and endpoint. Discovery now always returns only the settings and endpoints explicitly marked
  discoverable. An authenticated management view of a tenant's effective settings is tracked as a separate follow-up.

### Fixed

- The unique index on a tenant's domain name is now a filtered index (`WHERE NormalizedDomainName IS NOT NULL`), so
  multiple tenants may omit the optional domain name without colliding on relational providers.

- Adopted repository-wide engineering conventions: Central Package Management, Nerdbank.GitVersioning lockstep
  versioning, SourceLink-to-GitHub provenance with portable symbols, a scripted Definition of Done, CSharpier
  formatting, and scoped coding-convention instruction files. See [`docs/decisions/`](docs/decisions/).

[Unreleased]: https://github.com/NCodeGroup/Identity/commits/dev
