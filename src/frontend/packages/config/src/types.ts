/**
 * Runtime configuration contracts for the tier-agnostic frontend platform.
 *
 * A single bundle runs unchanged in sandbox/test/production: no tier value is baked at build time.
 * On boot, the app fetches a bootstrap document from the serving host and the OpenID Provider's
 * discovery document, and merges them into a {@link RuntimeConfig}. See docs/adr/0064.
 */

/**
 * Which presentation surface a bundle represents. An open vocabulary (ADR-0150): the host maps any surface name to its
 * coordinates (`FrontendBootstrapOptions.Surfaces`), so a new surface needs no change here. The listed names are the
 * surfaces this repository ships.
 */
export type SurfaceKind = 'mgmt' | 'auth' | 'playground' | 'conformance' | (string & {});

/**
 * The bootstrap document the serving host publishes at a well-known path. It carries only the
 * tier-specific coordinates the bundle cannot know at build time.
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

/** Brandable theme tokens injected at runtime (see docs/adr/0064 branding seam). */
export type BrandingConfig = {
  readonly displayName?: string;
  readonly logoUrl?: string;
  readonly primaryColor?: string;
  readonly secondaryColor?: string;
  readonly mode?: 'light' | 'dark';
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

/** The fully-resolved, validated runtime configuration the app renders against. */
export type RuntimeConfig = {
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
