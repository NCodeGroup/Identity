/**
 * Runtime configuration contracts for the tier-agnostic frontend platform.
 *
 * A single bundle runs unchanged in sandbox/test/production: no tier value is baked at build time.
 * On boot, an OpenID surface fetches a bootstrap document from the serving host and the OpenID Provider's
 * discovery document, and merges them into an {@link OpenIdRuntimeConfig}. A surface its own BFF host serves
 * fetches only the bootstrap document, which names its BFF, and resolves it into a {@link BffRuntimeConfig}.
 * See docs/decisions/1064.
 */

/**
 * Which presentation surface a bundle represents. An open vocabulary (ADR-1150): the host maps any surface name to its
 * coordinates (`FrontendBootstrapOptions.Surfaces`), so a new surface needs no change here. The listed names are the
 * surfaces this repository ships.
 */
export type SurfaceKind = 'mgmt' | 'auth' | 'playground' | 'conformance' | (string & {});

/**
 * The bootstrap document an OpenID surface's serving host publishes at a well-known path. It carries only the
 * tier-specific coordinates the bundle cannot know at build time, and names the public client the surface
 * authenticates as. A BFF-served surface's document is a {@link BffBootstrapDocument}.
 */
export type BootstrapDocument = {
  /** Absolute or root-relative base URL of the Management/OpenID REST API (no trailing slash). */
  readonly apiBaseUrl: string;
  /** The OpenID issuer whose `/.well-known/openid-configuration` describes the auth endpoints. */
  readonly issuer: string;
  /** The public OAuth client id this SPA authenticates as (authorization-code + PKCE). */
  readonly clientId?: string;
  /** Scopes requested at login; at least one management scope binds the `urn:ncode:management` audience. */
  readonly scopes?: readonly string[];
  /** Optional feature flags toggled per tier/deployment. */
  readonly features?: Readonly<Record<string, boolean>>;
  /** Optional branding overrides resolved per deployment/tenant. */
  readonly branding?: BrandingConfig;
};

/**
 * The tenant's non-production environment. Present only when the host embeds it in the branding; a production tenant
 * has none.
 */
export type EnvironmentConfig = {
  /** The environment's type, such as `development`. */
  readonly type: string;
  /** The environment's display name, when it has one. */
  readonly name?: string;
};

/** Brandable theme tokens injected at runtime (see docs/decisions/1064 branding seam). */
export type BrandingConfig = {
  readonly displayName?: string;
  readonly logoUrl?: string;
  readonly primaryColor?: string;
  readonly secondaryColor?: string;
  readonly mode?: 'light' | 'dark';
  /** The tenant's non-production environment; absent for a production tenant. */
  readonly environment?: EnvironmentConfig;
};

/** The subset of OpenID discovery metadata the frontend relies on. */
export type OpenIdDiscovery = {
  readonly issuer: string;
  readonly authorization_endpoint: string;
  readonly token_endpoint: string;
  readonly end_session_endpoint?: string;
  readonly userinfo_endpoint?: string;
  readonly jwks_uri?: string;
};

/** The request header a BFF-served surface sends on every call to its BFF, which rejects a request without it. */
export type AntiforgeryHeader = {
  /** The header name. */
  readonly name: string;
  /** The header value. */
  readonly value: string;
};

/** Where a BFF-served surface reaches its BFF. */
export type BffConfig = {
  /** The root-relative path the BFF's endpoints are served under (no trailing slash). */
  readonly basePath: string;
  /** The anti-forgery header every call to the BFF carries. */
  readonly antiforgeryHeader: AntiforgeryHeader;
};

/**
 * The bootstrap document a BFF-served surface's host publishes at a well-known path. It names the surface's BFF and
 * carries no token-related value: the browser holds no token.
 */
export type BffBootstrapDocument = {
  /** Absolute or root-relative base URL of the Management REST API (no trailing slash). */
  readonly apiBaseUrl: string;
  /** Where the surface reaches its BFF. */
  readonly bff: BffConfig;
  /** Optional feature flags toggled per tier/deployment. */
  readonly features?: Readonly<Record<string, boolean>>;
  /** Optional branding overrides resolved per deployment/tenant. */
  readonly branding?: BrandingConfig;
};

/** The fully-resolved, validated runtime configuration of an OpenID surface (a public client). */
export type OpenIdRuntimeConfig = {
  readonly kind: 'openid';
  readonly apiBaseUrl: string;
  readonly issuer: string;
  readonly authorizationEndpoint: string;
  readonly tokenEndpoint: string;
  readonly endSessionEndpoint?: string;
  readonly userInfoEndpoint?: string;
  /** The public OAuth client id this SPA authenticates as; absent until a deployment configures it. */
  readonly clientId?: string;
  /** Scopes requested at login (defaults applied when the bootstrap document omits them). */
  readonly scopes: readonly string[];
  readonly features: Readonly<Record<string, boolean>>;
  readonly branding?: BrandingConfig;
};

/** The fully-resolved, validated runtime configuration of a BFF-served surface. */
export type BffRuntimeConfig = {
  readonly kind: 'bff';
  readonly apiBaseUrl: string;
  /** Where the surface reaches its BFF. */
  readonly bff: BffConfig;
  readonly features: Readonly<Record<string, boolean>>;
  readonly branding?: BrandingConfig;
};

/** The fully-resolved, validated runtime configuration the app renders against. */
export type RuntimeConfig = OpenIdRuntimeConfig | BffRuntimeConfig;
