/**
 * TypeScript mirrors of the Management REST contracts
 * (NCode.Identity.OpenId.Management.Abstractions / Contracts). These are hand-modeled from the
 * server resources; keep them in sync with the C# contracts.
 */

/** Mirror of `CollectionResource<T>`. */
export type CollectionResource<T> = {
  readonly items: readonly T[];
  readonly continuationToken?: string;
};

/**
 * Mirror of an RFC 9457 problem details response and the extension members that
 * `ManagementProblemMembers` names. `retryAfterSeconds` is the whole number of seconds to wait,
 * the same value as the `Retry-After` header.
 */
export type ManagementProblem = {
  readonly type?: string;
  readonly title?: string;
  readonly status?: number;
  readonly detail?: string;
  readonly retryAfterSeconds?: number;
};

/** Mirror of `TenantResource`. */
export type TenantResource = {
  readonly tenantId: string;
  /** The team the tenant belongs to; it never changes. */
  readonly teamId: string;
  readonly concurrencyToken: string;
  readonly issuer: string | null;
  /** The https origin of the tenant's RFC 8705 mutual-TLS endpoint aliases, or `null` when it has none. */
  readonly mtlsEndpointAliasAuthority: string | null;
  readonly isDisabled: boolean;
  readonly displayName: string;
  /**
   * The tenant's effective environment, or absent when it cannot be determined (its settings need repair, or name a
   * type that is not registered).
   */
  readonly environment?: TenantEnvironmentResource;
};

/**
 * Mirror of `TenantEnvironmentResource`: a tenant's effective `environment_type` and `environment_name`. The
 * vocabulary of types is open, so tell production from every other environment by `isProduction`, not by the name.
 */
export type TenantEnvironmentResource = {
  readonly type: string;
  readonly name: string | null;
  readonly isProduction: boolean;
};

/**
 * Mirror of `CreateTenantRequest`. The tenant id is chosen by its creator and cannot change: 1 to 63
 * lowercase letters, digits, or hyphens that starts and ends with a letter or digit. `settings` is
 * the initial JSON settings object (empty by default; settings have their own PATCH endpoint). A `null`
 * issuer or mTLS endpoint alias takes the one the server's tenant strategy derives for the tenant.
 */
export type CreateTenantRequest = {
  readonly tenantId: string;
  readonly issuer: string | null;
  readonly mtlsEndpointAliasAuthority: string | null;
  readonly displayName: string;
  readonly isDisabled: boolean;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable tenant metadata a JSON Patch may modify (mirror of `UpdateTenantRequest`). The store
 * turns this into a `replace`-only JSON Patch document; keys match the server's wire property names.
 * A `null` issuer or mTLS endpoint alias restores the one the server's tenant strategy derives for the
 * tenant.
 */
export type UpdateTenantFields = {
  readonly issuer: string | null;
  readonly mtlsEndpointAliasAuthority: string | null;
  readonly displayName: string;
  readonly isDisabled: boolean;
};

/** Mirror of `TenantAddressFailure`: one reason a tenant cannot be served at a proposed address. */
export type TenantAddressFailure = {
  readonly detail: string;
  /** A conflict with another tenant (the write would answer `409`) rather than an invalid value (`400`). */
  readonly isConflict: boolean;
};

/**
 * Mirror of `TenantAddressResource`: the preview of a tenant's address (its id, issuer, and mTLS
 * endpoint alias) before it is created or either changes, judged by the same rules as the write itself.
 */
export type TenantAddressResource = {
  readonly tenantId: string;
  /** The proposed issuer, else the tenant's current one, else the one its strategy derives; or `null`. */
  readonly issuer: string | null;
  /** Whether {@link TenantAddressResource.issuer} is the one the tenant strategy derives. */
  readonly isIssuerDerived: boolean;
  /** The proposed mTLS endpoint alias, else the tenant's current one, else the one its strategy derives; or `null`. */
  readonly mtlsEndpointAliasAuthority: string | null;
  /** Whether {@link TenantAddressResource.mtlsEndpointAliasAuthority} is the one the tenant strategy derives. */
  readonly isMtlsEndpointAliasAuthorityDerived: boolean;
  /** Whether a tenant with the id already exists, so a new tenant cannot take it. */
  readonly exists: boolean;
  readonly failures: readonly TenantAddressFailure[];
};

/**
 * Mirror of `TenantPromotionEnvironmentSettings`: the environment-bound setting values for a resource a promotion
 * writes, addressed by its id in the target tenant.
 */
export type TenantPromotionEnvironmentSettings = {
  readonly resourceType: string;
  readonly resourceId: string;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * Mirror of `PromoteTenantRequest`: the configuration of one tenant of a team promoted into another tenant of the
 * same team. `environmentSettings` supplies values that belong to the target's environment.
 */
export type PromoteTenantRequest = {
  readonly sourceTenantId: string;
  readonly targetTenantId: string;
  readonly environmentSettings: readonly TenantPromotionEnvironmentSettings[];
};

/**
 * Mirror of `TenantPromotionChangeResource`: one resource a promotion creates, updates or leaves unchanged. `action`
 * is one of `created`, `updated` or `unchanged`.
 */
export type TenantPromotionChange = {
  readonly resourceType: string;
  readonly resourceId: string;
  readonly action: string;
};

/**
 * Mirror of `TenantPromotionNoteResource`: something to know about a promoted resource, such as a credential the
 * target must be given (`credential_required`), an environment-bound setting left unset
 * (`environment_setting_unset`) or a host-managed client the promotion leaves alone (`host_managed`).
 */
export type TenantPromotionNote = {
  readonly resourceType: string;
  readonly resourceId: string;
  readonly code: string;
  readonly detail: string;
  readonly settingName?: string | null;
};

/** Mirror of `TenantPromotionErrorResource`: a reason the promotion cannot be applied. */
export type TenantPromotionError = {
  readonly resourceType: string;
  readonly detail: string;
  readonly resourceId?: string | null;
};

/**
 * Mirror of `TenantPromotionResource`: the report of a promotion preview (`isApplied` is `false`) or of an applied
 * promotion (`isApplied` is `true`).
 */
export type TenantPromotionResource = {
  readonly teamId: string;
  readonly sourceTenantId: string;
  readonly targetTenantId: string;
  readonly isApplied: boolean;
  readonly changes: readonly TenantPromotionChange[];
  readonly notes: readonly TenantPromotionNote[];
  readonly errors: readonly TenantPromotionError[];
};

/** Mirror of `TeamResource`: a team groups tenants under one administrative boundary. */
export type TeamResource = {
  readonly teamId: string;
  readonly concurrencyToken: string;
  readonly displayName: string;
  readonly isDisabled: boolean;
  /** The team directly above, or null for a top-level team; it never changes. */
  readonly parentTeamId?: string | null;
};

/** Mirror of `RoleAssignmentResource`: one role a principal holds at one node of the resource tree. */
export type RoleAssignmentResource = {
  readonly assignmentId: string;
  readonly principalId: string;
  readonly roleName: string;
  readonly resourceType: string;
  readonly resourceId: string;
  readonly tenantId: string;
};

/** Mirror of `RoleHolderResource`: a principal that holds one role at a node. */
export type RoleHolderResource = {
  readonly assignmentId: string;
  readonly principalId: string;
  readonly roleName: string;
};

/** Mirror of `RoleListResource`: the principals that hold one role at a node and whether the caller may grant and revoke it there (ADR-1465). */
export type RoleListResource = {
  readonly items: readonly RoleHolderResource[];
  readonly canChange: boolean;
};

/** Mirror of `AddRoleHolderRequest`: the body that grants a principal the role of a role list at its node. */
export type AddRoleHolderRequest = {
  readonly principalId: string;
};

/**
 * Mirror of `CallerResource`: where the signed-in caller holds authority (ADR-1420). `principalId` is
 * `null` when the caller resolves to no principal.
 */
export type CallerResource = {
  readonly principalId: string | null;
  readonly isPlatformAdministrator: boolean;
  readonly assignments: readonly RoleAssignmentResource[];
};

/**
 * Mirror of `CreateTeamRequest`. The team id is chosen by its creator and cannot change. `settings` is
 * the initial JSON settings object (empty by default; settings have their own PATCH endpoint).
 */
export type CreateTeamRequest = {
  readonly teamId: string;
  readonly displayName: string;
  readonly isDisabled: boolean;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable team metadata a JSON Patch may modify (mirror of `UpdateTeamRequest`). The store
 * turns this into a `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateTeamFields = {
  readonly displayName: string;
  readonly isDisabled: boolean;
};

/**
 * Mirror of `ClientResource`. A client has no display name; it is identified by id.
 * `resourceServerId` names the resource server the client acts for — its credential at the token
 * introspection endpoint, accepted nowhere else — or is `null` for an ordinary client.
 */
export type ClientResource = {
  readonly clientId: string;
  readonly tenantId: string;
  readonly concurrencyToken: string;
  readonly isDisabled: boolean;
  readonly resourceServerId: string | null;
  /** The client's principal, which role assignments name to grant the client authority. */
  readonly principalId: string;
  /** The type of the node that owns this machine client — `platform` or `team` — or `null` for an ordinary client (ADR-1466). */
  readonly owningNodeType: string | null;
  /** The identifier of the node that owns this machine client: the team's id, or `platform`; `null` for an ordinary client. */
  readonly owningNodeId: string | null;
};

/**
 * Mirror of `CreateClientRequest`. The client id is server-generated (not supplied), and `settings`
 * is the initial JSON settings object (empty by default; settings have their own PATCH endpoint).
 * `resourceServerId` binds the new client to the resource server it acts for; the binding cannot
 * change after the client is created.
 */
export type CreateClientRequest = {
  readonly isDisabled: boolean;
  readonly resourceServerId?: string | null;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable client metadata a JSON Patch may modify (mirror of `UpdateClientRequest`). The store
 * turns this into a `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateClientFields = {
  readonly isDisabled: boolean;
};

/** Mirror of `ScopeResource` — a scope (a permission, in Auth0 terms) owned by a resource server. */
export type ScopeResource = {
  readonly value: string;
  readonly description: string | null;
  readonly isSystem: boolean;
};

/**
 * Mirror of `ResourceServerResource` (an API, in Auth0 terms; tenant scope). The server embeds the
 * owned {@link ScopeResource} list inline, so browsing resource servers already yields their scopes.
 * `isSystem` marks a reserved, system-owned resource server that cannot be deleted. The opaque
 * JSON `settings` object is managed through its own surface and is not modeled here.
 */
export type ResourceServerResource = {
  readonly tenantId: string;
  readonly resourceServerId: string;
  readonly identifier: string;
  readonly concurrencyToken: string;
  readonly name: string;
  readonly isSystem: boolean;
  readonly isDisabled: boolean;
  readonly scopes: readonly ScopeResource[];
};

/**
 * Mirror of `CreateResourceServerRequest`. The resource-server id is server-generated (not supplied);
 * `identifier` is the operator-chosen audience and is immutable after creation. `settings` is the
 * initial JSON settings object (empty by default; settings have their own surface).
 */
export type CreateResourceServerRequest = {
  readonly identifier: string;
  readonly name: string;
  readonly isDisabled: boolean;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable resource-server metadata a JSON Patch may modify (subset of `UpdateResourceServerRequest`;
 * `identifier` is immutable and `settings` is managed separately). The store turns this into a
 * `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateResourceServerFields = {
  readonly name: string;
  readonly isDisabled: boolean;
};

/**
 * Mirror of `CreateScopeRequest`. The scope `value` is the operator-chosen permission string and is
 * immutable after creation (it is the resource identifier on the wire).
 */
export type CreateScopeRequest = {
  readonly value: string;
  readonly description: string | null;
};

/**
 * Mirror of `UpdateScopeRequest`. The server binds scope updates as a whole-resource `PUT` (not a
 * JSON Patch), and the scope value is immutable, so only the description is sent.
 */
export type UpdateScopeRequest = {
  readonly description: string | null;
};

/**
 * Mirror of `GrantResource` (tenant scope) — a metadata-only projection of a persisted grant. Sensitive
 * grant material (hashed keys, payload contents) is never exposed. Grants are read-only administratively:
 * the only mutation is revocation (a delete). `revokedWhen`/`consumedWhen`/`expiresWhen` are `null` until
 * the grant reaches that state. Timestamps are ISO-8601 strings on the wire.
 */
export type GrantResource = {
  readonly grantId: string;
  readonly grantType: string;
  readonly tenantId: string;
  readonly clientId: string | null;
  readonly subjectId: string | null;
  readonly createdWhen: string;
  readonly expiresWhen: string | null;
  readonly revokedWhen: string | null;
  readonly consumedWhen: string | null;
};

/** Mirror of `GrantRevocationResult` — the count returned by a bulk (filtered) grant revocation. */
export type GrantRevocationResult = {
  readonly revoked: number;
};

/**
 * Mirror of `SecretResource` — metadata-only projection of a persisted secret. The key material is never
 * returned by a read path; only {@link CreatedSecretResource} carries it, once, at creation (ADR-1050).
 * Timestamps are ISO-8601 strings on the wire.
 */
export type SecretResource = {
  readonly secretId: string;
  /** The key identifier (`kid`) the key is selected by: the one it was imported with, or `secretId` when none. */
  readonly keyId: string;
  readonly concurrencyToken: string;
  readonly use: string | null;
  readonly algorithm: string | null;
  readonly createdWhen: string;
  // See ADR-1403.
  /** When the secret starts to sign or encrypt; a key published ahead of its use carries a future instant. */
  readonly activatesWhen: string;
  /** When the secret stops signing or encrypting while it still verifies or decrypts until `expiresWhen`. */
  readonly retiresWhen: string;
  readonly expiresWhen: string;
  readonly secretType: string;
  readonly keySizeBits: number;
  // See ADR-1404.
  /**
   * The component that manages the secret's lifecycle on its own, such as `tenant_key_rollover` for a key the server
   * provisioned, or `null` when the secret keeps the lifecycle its creator gave it.
   */
  readonly managedBy: string | null;
  // See ADR-1391.
  /**
   * Whether the server holds only a one-way verifier of the shared secret: it authenticates a method that compares the
   * secret, never one that uses it as a key, such as `client_secret_jwt` or an `HS256` request object.
   */
  readonly isVerifier: boolean;
  // See ADR-1475.
  /**
   * The remote key provider whose key management service holds the key's private half, such as `aws-kms`, or `null`
   * for a key the server holds.
   */
  readonly remoteKeyProvider: string | null;
};

/**
 * Mirror of a `*SecretsResource` wrapper (e.g. `ClientSecretsResource`). The owner embeds its secret list
 * inline along with the owner-level concurrency token.
 */
export type SecretsResource = {
  readonly concurrencyToken: string;
  readonly secrets: readonly SecretResource[];
};

/**
 * Mirror of `CreateSecretRequest`. The key material is generated server-side; the caller never supplies it.
 * `secretType` is one of the secret types the capabilities list (e.g. `symmetric`, `rsa`, `ecc`, `okp`); `curve` is the
 * JWK `crv` name for a secret type with curves and `null` for one without; `expiresWhen` is an ISO-8601 string.
 */
export type CreateSecretRequest = {
  readonly secretType: string;
  readonly keySizeBits: number;
  readonly curve?: string | null;
  readonly use: string | null;
  readonly algorithm: string | null;
  readonly expiresWhen: string;
};

/**
 * Mirror of `RotateSecretRequest`. The server adds a replacement of the rotated client secret's type, use, and
 * algorithm and keeps the rotated secret, so both authenticate while its holder switches. `expiresWhen` is the
 * replacement's ISO-8601 expiry and `rotatedExpiresWhen` the rotated secret's, never later than its current one.
 * See ADR-1320.
 */
export type RotateSecretRequest = {
  readonly expiresWhen: string;
  readonly rotatedExpiresWhen?: string;
};

/**
 * Mirror of `CreatedSecretResource` — the create response. It carries the same metadata as
 * {@link SecretResource} plus `secretMaterial`: the one-time, base64url-encoded key material revealed only
 * here and never again (`null` for asymmetric secrets, which have no externally-held material). See ADR-1050.
 */
export type CreatedSecretResource = SecretResource & {
  readonly secretMaterial: string | null;
};

/**
 * Mirror of `PublicKeyResource` — the public key material of an asymmetric secret, in two representations:
 * `jwk` (an RFC 7517 JSON Web Key carrying only the public members, the same shape the JWKS endpoint publishes)
 * and `pem` (a `SubjectPublicKeyInfo` PEM string). No private material is ever included; only asymmetric secrets
 * (`rsa`, `ecc`) expose this sub-resource. See ADR-1058.
 */
export type PublicKeyResource = {
  readonly secretId: string;
  readonly secretType: string;
  readonly jwk: Readonly<Record<string, unknown>>;
  readonly pem: string;
};

/**
 * Mirror of `SecretTypeCapabilityResource` — one secret type the server understands and the capabilities it
 * exposes. Drives capability-based UI (for example which types expose an exportable public key) from server
 * discovery rather than hard-coded assumptions.
 */
export type SecretTypeCapability = {
  readonly secretType: string;
  readonly isAsymmetric: boolean;
  readonly supportsPublicKeyExport: boolean;
};

/** Mirror of `EccCurveCapabilityResource` — an elliptic curve the server supports for generating ECC secrets. */
export type EccCurveCapability = {
  readonly curveName: string;
  readonly curveSizeBits: number;
};

/** Mirror of `KeyBitSizesResource` — an inclusive range of legal key sizes (bits). `skipBits` 0 means fixed size. */
export type KeyBitSizes = {
  readonly minBits: number;
  readonly maxBits: number;
  readonly skipBits: number;
};

/**
 * Mirror of `AlgorithmCurveCapabilityResource` — one curve an algorithm accepts keys of a secret type on, with the size
 * of its keys.
 */
export type AlgorithmCurveCapability = {
  /** The JWK `crv` name of the curve (e.g. `P-384`, `secp256k1`, `X25519`). */
  readonly curveName: string;
  readonly curveSizeBits: number;
};

/**
 * Mirror of `AlgorithmKeyTypeCapabilityResource` — one secret type an algorithm accepts, with the key sizes and
 * curves legal for keys of that type. An algorithm such as `ECDH-ES` lists one per secret type it serves (ADR-1389).
 */
export type AlgorithmKeyTypeCapability = {
  /** The secret-type discriminator (e.g. `rsa`, `ecc`, `okp`) whose key material is compatible with the algorithm. */
  readonly secretType: string;
  readonly keyBitSizes: readonly KeyBitSizes[];
  /**
   * The curves keys of the secret type must be on for the algorithm, in its order of preference (e.g. `secp256k1`
   * for `ES256K`), each with its key size; empty when the type has no curve.
   */
  readonly curves: readonly AlgorithmCurveCapability[];
};

/**
 * Mirror of `AlgorithmCapabilityResource` — one signature or key-management algorithm the server supports, with
 * its implied use and each compatible secret type with its legal key sizes and curves, so the create-secret form can
 * drive its algorithm, use, key-size and curve inputs from server discovery rather than hard-coded defaults.
 */
export type AlgorithmCapability = {
  readonly code: string;
  /** The key use the algorithm implies: `sig` for signature, `enc` for key-management. */
  readonly use: string;
  /** The compatible secret types, each with its legal key sizes and curves, in the algorithm's order of preference. */
  readonly keyTypes: readonly AlgorithmKeyTypeCapability[];
};

/**
 * Mirror of `ManagementCapabilitiesResource` — the server-wide management capabilities discovery document
 * returned by `GET /api/capabilities`. Intentionally extensible with additional capability sections over time.
 */
export type ManagementCapabilitiesResource = {
  readonly secretTypes: readonly SecretTypeCapability[];
  readonly eccCurves?: readonly EccCurveCapability[];
  readonly algorithms?: readonly AlgorithmCapability[];
};

/**
 * Mirror of a `*SettingsResource` wrapper (e.g. `ClientSettingsResource`). `settings` is the opaque persisted
 * JSON settings object (a map of setting name to value); the owner-level `concurrencyToken` guards edits.
 */
export type SettingsResource = {
  readonly concurrencyToken: string;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * Mirror of a `*EffectiveSettingsResource` wrapper. `settings` is the read-only merged (platform and team baseline +
 * persisted overrides + descriptor defaults) flat JSON object the authorization server resolves at runtime.
 */
export type EffectiveSettingsResource = {
  readonly settings: Readonly<Record<string, unknown>>;
};

/** Mirror of `SettingLimitsResource`: the limits an owner's settings may carry and whether the caller may change them (ADR-1448). */
export type SettingLimitsResource = {
  readonly names: readonly string[];
  readonly canChange: boolean;
};

/**
 * Mirror of `TenantUsageEntry` — one row of the durable usage ledger (ADR-1199): a tenant's `count` of a `meter` (an
 * open vocabulary, e.g. `requests` or `token_requests`) on a UTC calendar `day` (`YYYY-MM-DD` on the wire).
 */
export type TenantUsageEntry = {
  readonly day: string;
  readonly meter: string;
  readonly count: number;
};

/**
 * Mirror of `TenantUsageResource` — a tenant's usage ledger for the inclusive range of UTC days `from`..`to`
 * (`YYYY-MM-DD`), ordered by day then meter. The server defaults the range to the last 30 days and caps it at 366.
 */
export type TenantUsageResource = {
  readonly tenantId: string;
  readonly from: string;
  readonly to: string;
  readonly entries: readonly TenantUsageEntry[];
};

/**
 * Mirror of `TeamUsageEntry` — one row of a team's usage ledger: a tenant's `count` of a `meter` (an open
 * vocabulary, e.g. `requests` or `token_requests`) on a UTC calendar `day` (`YYYY-MM-DD` on the wire).
 */
export type TeamUsageEntry = {
  readonly teamId: string;
  readonly tenantId: string;
  readonly day: string;
  readonly meter: string;
  readonly count: number;
};

/**
 * Mirror of `TeamUsageResource` — the usage of a team's tenants for the inclusive range of UTC days `from`..`to`
 * (`YYYY-MM-DD`), ordered by day then meter. The server defaults the range to the last 30 days and caps it at 366.
 */
export type TeamUsageResource = {
  readonly teamId: string;
  readonly from: string;
  readonly to: string;
  readonly entries: readonly TeamUsageEntry[];
};

/**
 * Mirror of `CreateInitialAccessTokenRequest` (RFC 7591). Mints a token that authorizes its bearer to register a new
 * client at the protected dynamic client registration endpoint. `description` is an optional administrative note;
 * `lifetimeSeconds` is an optional lifetime (`null` means a one-day lifetime; at most 30 days). `resourceServerId`
 * scopes the token to a resource server, so the client it registers is bound to it and introspects for it (ADR-1192).
 */
export type CreateInitialAccessTokenRequest = {
  readonly description: string | null;
  readonly lifetimeSeconds: number | null;
  readonly resourceServerId?: string | null;
};

/**
 * Mirror of `CreatedInitialAccessTokenResource` — the mint response. The `initialAccessToken` plaintext is revealed
 * exactly once here and never again (the grant store persists only its salted hash, like ADR-1050 secret material).
 * `grantId` addresses the backing grant for later revocation; `expiresWhen` is when the single-use token expires.
 * `resourceServerId` is the resource server the token is scoped to, if any. Timestamps are ISO-8601 strings on the
 * wire.
 */
export type CreatedInitialAccessTokenResource = {
  readonly initialAccessToken: string;
  readonly grantId: string;
  readonly tenantId: string;
  readonly description: string | null;
  readonly resourceServerId?: string | null;
  readonly createdWhen: string;
  readonly expiresWhen: string | null;
};

// See ADR-1407.
/**
 * Mirror of `EncryptionKeyResource` — one master key of a tenant or of the platform. It carries no key material, wrapped
 * key or key check.
 */
export type EncryptionKeyResource = {
  readonly keyId: string;
  /** The version of the key within its owner, starting at one. */
  readonly version: number;
  /** `active` for the key that protects new values, `retired` for an older one; an unknown state counts as retired. */
  readonly state: string;
  readonly createdWhen: string;
  readonly retiredWhen: string | null;
  /** The name of the root key provider that wraps the key. */
  readonly rootKeyProvider: string;
  readonly rootKeyId: string;
  /** The identifier of the recovery key that also wraps the key, or `null` when no recovery copy exists. */
  readonly recoveryKeyId: string | null;
  /**
   * The reference of the tenant's own root key that wraps the key (ADR-1414), or `null` when the deployment's root key
   * wraps it.
   */
  readonly rootKeyReference: string | null;
  /**
   * The identity the server reaches the tenant's own root key through (ADR-1414), or `null` when it reaches the key
   * through the deployment's own identity or the deployment's root key wraps the key.
   */
  readonly rootKeyAccessIdentity: string | null;
};

// See ADR-1414.
/**
 * Mirror of `EncryptionRootKeyResource` — the root key a tenant brings: the key wrapper that reaches it, the reference
 * that names it and its last validation. It carries no key material.
 */
export type EncryptionRootKeyResource = {
  /** The name of the key wrapper that reaches the key, such as `aws-kms`. */
  readonly provider: string;
  /** The reference that names the key at its provider, such as a KMS key ARN. */
  readonly keyReference: string;
  /**
   * The identity the server reaches the key through, such as an IAM role ARN, a service account email or a Microsoft
   * Entra tenant ID, or `null` when it reaches the key through the deployment's own identity.
   */
  readonly accessIdentity: string | null;
  /** `active`, `unavailable` or `removing`; an unknown state counts as unavailable. */
  readonly state: string;
  readonly createdWhen: string;
  readonly stateChangedWhen: string;
  /** When the last validation succeeded, or `null` when none has. */
  readonly validatedWhen: string | null;
  /** When the last validation failed, or `null` when none has. */
  readonly failedWhen: string | null;
  /** Why the last validation failed, or `null` when it did not. */
  readonly failure: string | null;
};

/** Mirror of `EncryptionKeysResource` — the master keys of an owner, newest version first. */
export type EncryptionKeysResource = {
  readonly keys: readonly EncryptionKeyResource[];
  /** Whether new keys are also wrapped under a recovery key. */
  readonly recoveryConfigured: boolean;
  /** The root key the tenant brings, or `null` when the deployment's root key wraps its keys. */
  readonly rootKey: EncryptionRootKeyResource | null;
  /** The key wrapper names a tenant may choose a root key from; empty on the platform. */
  readonly rootKeyProviders: readonly string[];
  /**
   * The value an identity the tenant grants can require of the server, such as the external ID of an AWS role's trust
   * policy; `null` when the owner cannot bring a key.
   */
  readonly rootKeyAccessBinding: string | null;
};

/** Mirror of `EncryptionKeyRotationResource` — the result of a rekey. */
export type EncryptionKeyRotationResource = {
  readonly activeKey: EncryptionKeyResource;
  /** The key the rekey retired, or `null` when the owner had no active key. */
  readonly retiredKey: EncryptionKeyResource | null;
};

/** Mirror of `EncryptionRootKeyRequest` — the root key a tenant sets. */
export type EncryptionRootKeyRequest = {
  readonly provider: string;
  readonly keyReference: string;
  /**
   * The identity the server takes on to reach the key, such as an IAM role ARN, a service account email or a Microsoft
   * Entra tenant ID; omitted, the deployment's own identity reaches it.
   */
  readonly accessIdentity?: string | null;
};

/** Mirror of `EncryptionRootKeyChangeResource` — the result of setting a root key. */
export type EncryptionRootKeyChangeResource = {
  readonly rootKey: EncryptionRootKeyResource;
  /** The key the root key change created, or `null` when it created none. */
  readonly activeKey: EncryptionKeyResource | null;
  /** The key the root key change retired, or `null` when it retired none. */
  readonly retiredKey: EncryptionKeyResource | null;
  /** How many master keys were rewrapped under the root key. */
  readonly rewrappedKeys: number;
  /** How many master keys could not be rewrapped yet. */
  readonly failedKeys: number;
};
