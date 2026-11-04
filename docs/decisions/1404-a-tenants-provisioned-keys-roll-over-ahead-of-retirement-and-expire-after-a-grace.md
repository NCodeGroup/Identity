# 1404. A tenant's provisioned keys roll over ahead of retirement and expire after a grace

- **Status:** Accepted
- **Date:** 2026-11-04
- **Deciders:** NCode Group
- **Amends:** [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md), decision 3's key lifetime and
  decisions 4, 8 and 9's rule that the server never removes, replaces or rotates a key: a provisioned key has a
  lifetime, its successor is generated ahead of its retirement, a key the tenant's settings no longer use is retired,
  and an expired key the server generated is removed; [ADR-1221](1221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md):
  `OpenIdTenantKeyRolloverOptions.Interval` is a timer period validated when the host starts

## Context

Every tenant is provisioned with a persisted key for each algorithm its settings use
([ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md)), and a stored key has an active window inside
its validity window ([ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md)).
A key that signs forever is a key whose compromise is never bounded, and NIST SP 800-57 bounds the cryptoperiod of a
signing key, so the major providers roll their signing keys over on a schedule: Microsoft Entra ID about every six
weeks, Okta about four times a year, Keycloak and Auth0 on the operator's command. ASP.NET Core Data Protection shows
the shape a self-managed key ring takes: a key is created ahead of its activation, activates on a fixed delay, expires
after a lifetime, and the ring is walked periodically to create the next key before the current one expires.

Rollover has to be correct across replicas that share one store, with no replica in charge: the key every verifier
accepts must be published before the first token it signs, a tenant must never be left without a key that signs, two
replicas must not both generate a successor, and a tenant that no request reaches must roll over too. The server
already converges a tenant's keys on two paths, the seed that runs when the tenant is materialized and the pass the
server's settings reconciliation runs in the background ([ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md)),
and a change an administrator makes to a key is an audit event, so a change the server makes to a key on its own
needs a record of the same standing.

## Decision

**The keys the server provisions for a tenant have a lifetime: a key is published for an activation delay, signs or
encrypts for its lifetime, and stays published for a retirement grace that covers every token it protected. One
planner decides, from the keys a tenant holds and its effective settings, what changes at an instant, and one
rollover applies it, both when the tenant is seeded and in a background pass one replica runs per interval. Every
change the server makes to a tenant's key on its own is an audit event.**

1. **Three tenant settings shape the lifecycle.** `tenant_key_lifetime` (default 90 days, at least one hour, a
   ceiling: `Min`) is how long a key signs or encrypts; `tenant_key_activation_delay` (default two days, a floor:
   `Max`) is how long a key is published before it activates; `tenant_key_retirement_grace` (default seven days, a
   floor: `Max`) is how long a retired key stays published and verifies or decrypts. The grace is raised to the
   longest lifetime of the tokens the tenant protects with its keys plus its `clock_skew`, so no token outlives the
   key that verifies it; `OpenId:Tenant:KeyRollover:TokenLifetimeSettings` names those lifetime settings, by default
   `id_token_lifetime`, `access_token_lifetime`, `authorization_response_lifetime` and `request_object_max_lifetime`,
   and a package that signs or encrypts another token with the tenant's keys adds its own. None is advertised.
2. **The plan, for each provisioned key the tenant's settings use.** The held keys that satisfy the specification and
   have neither retired nor expired serve it. When none of them is active, a published key activates at once (the
   earliest to activate), or, when there is none, a key is generated that activates at once and retires after the
   lifetime: nothing can be handed over from, and the delay would only prolong the outage. When the last serving key
   retires within the activation delay plus the pass interval, its successor is generated, published from now and
   activating at that retirement, so exactly one key of the algorithm signs at every instant and a verifier has held
   the successor for the whole delay by the hand-over; the interval is added because the pass that generates the
   successor runs up to one interval after the predecessor enters the window. A successor already published yields no
   second one. A generated key expires at its retirement plus the grace.
3. **Retirement and removal.** A key the server generated for a provisioned algorithm the tenant's settings no longer
   use retires now and expires at the earlier of its own expiry and the end of the grace: the keys it signed stay
   verifiable, and nothing signs with it. An expired key the server generated is removed. A key an operator supplied
   is never retired or removed: it is the operator's to keep, and the operator's `DELETE` is the way to retire a key at
   once on suspicion of compromise, after which the next seed or pass replaces it. A key bound to no algorithm is
   never retired for an unused algorithm, because it serves any.
4. **Two paths, one planner.** The seed contributor applies the plan whenever the tenant is materialized, so a tenant
   that is served gets its first key, its successors, and the retirement and removal of what it no longer needs, in
   the seed's unit of work. `TenantKeyRolloverService` applies it in the background: once the replica's server is
   materialized and then every `OpenId:Tenant:KeyRollover:Interval` (default one hour), one replica takes a lease on
   the shared-state port for the interval and walks every seeded tenant, changing each in its own unit of work under
   the tenant's optimistic concurrency, so a pass that overlaps a seed or another pass loses the race for that tenant
   and leaves it to the winner. A tenant that holds no key is left to its first materialization, which seeds it in
   full. A tenant the pass changed is announced through `TenantChangedCommand` without a settings change, so every
   replica evicts it and serves its keys as they are rather than at its next secrets poll. The server's settings
   reconciliation counts a tenant as lacking a key when no serving key satisfies the specification, so a tenant whose
   only key for an algorithm retired is seeded like one that never held it. The pass runs only while key provisioning
   is enabled; a deployment that brings its own keys manages their lifecycle. It never runs on the in-process job
   dispatcher, whose bounded workers a pass over every tenant would hold.
5. **Audited as the server's own changes.** Every change is a `TenantKeyAuditEvent`: `tenant.key.generated`,
   `tenant.key.activated`, `tenant.key.retired` and `tenant.key.removed`, in the `key` category with the `system`
   actor, the key's secret and its tenant as resources, and the reason, key identifier, type, algorithm, use and the
   three instants as details. The events carry no request, because the operation is the server's own whether a seed
   in a request or the pass performed it, and never key material.

## Options considered

- **Rotate on the Dispatch port.** Rejected: dispatch is at-least-once execution of one job with retries, and a pass
  over every tenant would hold its bounded workers; a periodic pass under a lease is the shape every background
  pass of the server takes.
- **Rotate only on access, in the seed.** Rejected: a tenant no request reaches for a lifetime would find its key
  retired at the next request and generate a successor that no verifier has fetched.
- **Rotate only in the background pass.** Rejected: the pass runs hourly on one replica, and a tenant that is being
  served must sign at once when its key was deleted; the seed is the path every served tenant passes through.
- **Hand over with an overlap of two active keys.** Rejected: two replicas with a clock skew would already pick
  different keys at the hand-over instant; a longer overlap adds nothing a verifier needs, since both keys are
  published for the delay before and the grace after, and the selector's preference for the later-expiring key
  would make the overlap one key wide anyway.
- **Keep expired keys for the record.** Rejected: an expired key verifies nothing a relying party could still accept,
  and the audit trail records its removal with its identifier and instants; the keys an operator supplied are kept,
  because the operator put them there.
- **A fixed grace of seven days.** Rejected: a tenant whose access tokens live longer than the grace would refuse
  valid tokens; the grace is raised to the longest token lifetime plus the clock skew instead.
- **Record `activated` when the clock passes the activation.** Rejected: no act happens, and no replica owns the
  instant; the `generated` event states when the key activates. The `activated` event records the one case where
  the server moves the activation forward.

## Consequences

- A provisioned key signs for 90 days by default and is replaced without an outage: its successor is published two
  days ahead and takes over the instant it retires, and the retired key verifies for seven days more.
- A tenant whose settings stop using an algorithm keeps verifying with the key it signed with for the grace and then
  holds no key for it; a tenant that starts using one again is seeded with a new key.
- `OpenIdTenantOptions` gains `KeyRollover`, the tenant key seed handler takes the planner and the rollover, the
  reconciliation handler takes the planner and a `TimeProvider`, and `NCode.Identity.OpenId.Core` references the
  events abstractions to publish the audit events.
- The host walks every tenant once an hour on one replica; the walk reads each tenant's settings projection and keys
  and loads a tenant's master key only when it generates a key.

## References

- [`DefaultTenantKeyRolloverPlanner`](../../src/NCode.Identity.OpenId.Core/Tenants/Keys/DefaultTenantKeyRolloverPlanner.cs),
  [`DefaultTenantKeyRollover`](../../src/NCode.Identity.OpenId.Core/Tenants/Keys/DefaultTenantKeyRollover.cs),
  [`TenantKeyRolloverService`](../../src/NCode.Identity.OpenId.Core/Tenants/Keys/TenantKeyRolloverService.cs),
  [`TenantKeySeedHandler`](../../src/NCode.Identity.OpenId.Core/Tenants/Keys/TenantKeySeedHandler.cs),
  [`TenantKeyLifetimes`](../../src/NCode.Identity.OpenId.Core/Tenants/Keys/TenantKeyLifetimes.cs),
  [`TenantKeyAuditEvent`](../../src/NCode.Identity.OpenId.Core/Tenants/Keys/TenantKeyAuditEvent.cs),
  [`OpenIdTenantKeyRolloverOptions`](../../src/NCode.Identity.OpenId.Core/Options/OpenIdTenantKeyRolloverOptions.cs)
- [Audit records](../audit-records.md), the `tenant.key.*` events
- [Key rollover](../capabilities/key-rollover.md), the capability page
- [OpenID Connect Core 1.0 §10.1.1](https://openid.net/specs/openid-connect-core-1_0.html#RotateSigKeys),
  [NIST SP 800-57 Part 1 §5.3](https://csrc.nist.gov/pubs/sp/800/57/pt1/r5/final)
- [ADR-1169](1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1219](1219-server-settings-converge-on-every-replica-through-one-reconciliation.md),
  [ADR-1403](1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md)
- Issue [#166](https://github.com/NCodeGroup/Identity/issues/166)
