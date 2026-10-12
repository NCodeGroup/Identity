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

/** Mirror of `TenantResource`. */
export type TenantResource = {
  readonly tenantId: string;
  readonly concurrencyToken: string;
  readonly domainName: string | null;
  readonly isDisabled: boolean;
  readonly displayName: string;
};

/**
 * Mirror of `CreateTenantRequest`. The tenant id is server-generated (not supplied), and `settings`
 * is the initial JSON settings object (empty by default; settings have their own PATCH endpoint).
 */
export type CreateTenantRequest = {
  readonly domainName: string | null;
  readonly displayName: string;
  readonly isDisabled: boolean;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable tenant metadata a JSON Patch may modify (mirror of `UpdateTenantRequest`). The store
 * turns this into a `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateTenantFields = {
  readonly domainName: string | null;
  readonly displayName: string;
  readonly isDisabled: boolean;
};

/** Mirror of `ServerResource` (global scope). */
export type ServerResource = {
  readonly serverId: string;
  readonly concurrencyToken: string;
  readonly displayName: string;
};

/**
 * Mirror of `CreateServerRequest`. The server id is server-generated (not supplied), and `settings`
 * is the initial JSON settings object (empty by default; settings have their own PATCH endpoint).
 */
export type CreateServerRequest = {
  readonly displayName: string;
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * The mutable server metadata a JSON Patch may modify (mirror of `UpdateServerRequest`). The store
 * turns this into a `replace`-only JSON Patch document; keys match the server's wire property names.
 */
export type UpdateServerFields = {
  readonly displayName: string;
};

/** Mirror of `ClientResource` (tenant scope). A client has no display name; it is identified by id. */
export type ClientResource = {
  readonly clientId: string;
  readonly tenantId: string;
  readonly concurrencyToken: string;
  readonly isDisabled: boolean;
};

/**
 * Mirror of `CreateClientRequest`. The client id is server-generated (not supplied), and `settings`
 * is the initial JSON settings object (empty by default; settings have their own PATCH endpoint).
 */
export type CreateClientRequest = {
  readonly isDisabled: boolean;
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
 * returned by a read path; only {@link CreatedSecretResource} carries it, once, at creation (ADR-0050).
 * Timestamps are ISO-8601 strings on the wire.
 */
export type SecretResource = {
  readonly secretId: string;
  readonly concurrencyToken: string;
  readonly use: string | null;
  readonly algorithm: string | null;
  readonly createdWhen: string;
  readonly expiresWhen: string;
  readonly secretType: string;
  readonly keySizeBits: number;
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
 * `secretType` is one of `symmetric`, `rsa`, or `ecc`; `expiresWhen` is an ISO-8601 string.
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
 * Mirror of `CreatedSecretResource` — the create response. It carries the same metadata as
 * {@link SecretResource} plus `secretMaterial`: the one-time, base64url-encoded key material revealed only
 * here and never again (`null` for asymmetric secrets, which have no externally-held material). See ADR-0050.
 */
export type CreatedSecretResource = SecretResource & {
  readonly secretMaterial: string | null;
};

/**
 * Mirror of `PublicKeyResource` — the public key material of an asymmetric secret, in two representations:
 * `jwk` (an RFC 7517 JSON Web Key carrying only the public members, the same shape the JWKS endpoint publishes)
 * and `pem` (a `SubjectPublicKeyInfo` PEM string). No private material is ever included; only asymmetric secrets
 * (`rsa`, `ecc`) expose this sub-resource. See ADR-0058.
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
 * Mirror of `AlgorithmCapabilityResource` — one signature or key-management algorithm the server supports, with
 * its implied use, compatible secret types, and legal key sizes, so the create-secret form can drive its
 * algorithm, use, and key-size inputs from server discovery rather than hard-coded defaults.
 */
export type AlgorithmCapability = {
  readonly code: string;
  /** The key use the algorithm implies: `sig` for signature, `enc` for key-management. */
  readonly use: string;
  /** The secret-type discriminators (e.g. `rsa`, `ecc`) whose key material is compatible with this algorithm. */
  readonly secretTypes: readonly string[];
  readonly keyBitSizes: readonly KeyBitSizes[];
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
 * Mirror of a `*EffectiveSettingsResource` wrapper. `settings` is the read-only merged (server baseline +
 * persisted overrides + descriptor defaults) flat JSON object the authorization server resolves at runtime.
 */
export type EffectiveSettingsResource = {
  readonly settings: Readonly<Record<string, unknown>>;
};

/**
 * Mirror of `CreateInitialAccessTokenRequest` (RFC 7591). Mints a token that authorizes its bearer to register a new
 * client at the protected dynamic client registration endpoint. `description` is an optional administrative note;
 * `lifetimeSeconds` is an optional positive lifetime (`null` means the token does not expire).
 */
export type CreateInitialAccessTokenRequest = {
  readonly description: string | null;
  readonly lifetimeSeconds: number | null;
};

/**
 * Mirror of `CreatedInitialAccessTokenResource` — the mint response. The `initialAccessToken` plaintext is revealed
 * exactly once here and never again (the grant store persists only its salted hash, like ADR-0050 secret material).
 * `grantId` addresses the backing grant for later revocation; `expiresWhen` is `null` for a non-expiring token.
 * Timestamps are ISO-8601 strings on the wire.
 */
export type CreatedInitialAccessTokenResource = {
  readonly initialAccessToken: string;
  readonly grantId: string;
  readonly tenantId: string;
  readonly description: string | null;
  readonly createdWhen: string;
  readonly expiresWhen: string | null;
};
