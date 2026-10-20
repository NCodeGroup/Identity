# 129. The tenant's issuer is the single authoritative origin

- **Status:** Accepted (extended by [ADR-0160](0160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md):
  the issuer's form and host uniqueness under the host strategy are validated wherever a tenant is written)
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

An OpenID Provider has to answer one question in two very different places, and it had been answering it twice from two
different sources.

**Where am I published?** Every absolute URL the server hands out — the discovery document's `authorization_endpoint`,
`token_endpoint` and `jwks_uri`, the RFC 7592 `registration_client_uri`, the device flow's `verification_uri`, the
session-management `check_session_iframe`, the interaction continue URL — names an origin a client will later connect
back to.

**Who am I routed as?** A multi-tenant host must map an inbound request to a tenant, and the host-based strategy does
so by matching the request's host against a stored value.

The first was answered from the **live `HttpContext`**, via `LinkGenerator.GetUriByName(httpContext, …)`. The second
was answered from a stored **`DomainName`** column. The tenant's **`Issuer`** — the identity it asserts in the `iss`
claim of every token it mints — was a third, independent value, and nothing reconciled the three.

Both divergences are exploitable or protocol-breaking:

- **Request-derived publication is a credential-exfiltration primitive.** A request carrying a foreign `Host` produced
  a discovery document whose `issuer` was the correct, configured value while its `token_endpoint` pointed at the
  attacker. A client validating discovery sees the issuer it expects, accepts the document, and then posts its client
  secret and authorization code to the advertised `token_endpoint`. The correct `issuer` is precisely what makes the
  forgery convincing.

- **A stored routing host drifts from the published identity.**
  [OIDC Discovery §4.3](https://openid.net/specs/openid-connect-discovery-1_0.html#ProviderConfigurationValidation)
  requires the `issuer` returned in the document to be *identical* to the prefix the document was fetched from. A
  tenant reachable at `op.example` but publishing `https://other.example` is rejected outright by conforming clients.
  Two independently-writable columns make that state reachable through ordinary administration, with no attacker.

## Decision

**The tenant's `Issuer` is the single authoritative origin.** Everything else is derived from it.

1. **Published URLs are anchored to the tenant, never to the request.** All absolute-URL generation goes through one
   seam that builds from the tenant's base address, which is parsed from the tenant's issuer. The incoming request
   contributes nothing to any URL the server publishes.

2. **`DomainName` is removed, not validated.** The host-based strategy routes on the **host component of the issuer**,
   derived on write into an indexed column that is never set independently. A second stored copy of a derivable value
   is a drift hazard whatever guards it; the only way to guarantee the routed origin matches the published one is for
   there to be one origin.

3. **The issuer is a first-class property of the tenant, not a setting.** It had been `tenant_issuer`, a merged
   setting. That was a historical accident — `tenant_issuer` landed 2023-11-17, five months before `PersistedTenant`
   existed (2024-04-27) — and it showed: `tenant_issuer` was the *only* descriptor in the merge lattice that had to
   opt out of merging entirely, through the parent-wins `Keep` projection, because a server-level issuer inherited by
   every tenant is meaningless. A value that cannot participate in the merge lattice is not a setting.

4. **Two indexes, with deliberately different uniqueness.**

   - A **filtered UNIQUE** index on the normalized issuer. Two tenants sharing an issuer is a protocol-level
     collision: the issuer *is* the tenant's identity and is asserted in every token it mints, so a shared issuer
     makes two tenants' tokens mutually indistinguishable to a relying party. This invariant was previously enforced
     nowhere.

   - A deliberately **NON-unique** index on the derived host. Path-based tenants legitimately share a host
     (`https://op.example/tenant-a`, `https://op.example/tenant-b`). A unique host index would make the path strategy
     unusable beyond a single tenant. Host uniqueness is a constraint of the *host* strategy alone, so it is enforced
     by validation when that strategy is in use — not by the schema, which serves every strategy.

A tenant with **no** issuer still derives its origin from the request. That is the convenience that lets a developer's
machine answer under `localhost`, a container name and a LAN address at once, and it is the behaviour a deployment
must not depend on. It is a fallback for the unconfigured case, never a production posture.

## Consequences

Deployments must configure an issuer per tenant; the single-tenant host reads it from
`OpenId:TenantResolution:StaticSingle:Issuer`, and multi-tenant hosts set it through the management API. A deployment
that leaves it unset silently inherits the development fallback, which is why refusing to boot in Production without a
configured issuer is tracked as follow-up work.

Because the issuer determines routing, **changing a tenant's issuer changes the host it answers on** — correctly, and
as a single operation rather than two that can be half-applied.

Tests that assert on published URLs must pin an issuer explicitly. Without that pin, a test proving "a spoofed `Host`
cannot reach the published URLs" passes vacuously on a tenant that has no issuer to begin with, since the request-derived
fallback and the spoofed value coincide.

The migration **clears** the carried-over domain values rather than copying them into the new column. Copying would
preserve exactly the drift this decision eliminates: a tenant whose old `DomainName` disagreed with its issuer would
emerge still routable by a host it does not claim.
